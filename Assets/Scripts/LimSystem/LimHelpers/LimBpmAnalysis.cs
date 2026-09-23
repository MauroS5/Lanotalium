using System;
using System.Collections.Generic;

/// <summary>
/// Finds a song's tempo, where its first beat falls, and where the tempo
/// changes or the beat jumps, from the samples alone.
///
/// Plain C# with no Unity in it, so it runs on a worker thread and can be
/// checked outside the editor against charts whose tempo is known.
///
/// The song is reduced to an onset envelope (how much new sound starts in
/// each few milliseconds). The tempo is guessed from how that envelope
/// repeats, then pinned down by folding stretches of the song onto one beat:
/// only the exact period stacks every beat's onset on the same spot. Short
/// windows along the song then say which tempo and which phase each part
/// keeps, and a part that keeps another one becomes a section of its own.
/// </summary>
public static class LimBpmAnalysis
{
    public class Section
    {
        public double Time;
        public double Bpm;
    }

    public class Result
    {
        /// <summary>The song's tempo: the one most of it keeps.</summary>
        public double Bpm;
        /// <summary>The first beat on which the song plays, on that tempo's grid.</summary>
        public double FirstBeat;
        /// <summary>
        /// One entry per stretch with its own tempo or its own beat, the first
        /// at its own first beat. More than one when the song changes.
        /// </summary>
        public List<Section> Sections = new List<Section>();
        public bool HasChanges { get { return Sections.Count > 1; } }
    }

    private const int FftSize = 1024, Hop = 128;
    private const double MinBpm = 60, MaxBpm = 300;

    // Tunables, public so the checks outside Unity can try other values.
    public static double Latency = 0;
    public static double PriorCenter = 155, PriorWidth = 0.9;
    public static double WindowSeconds = 10, WindowHop = 2.5;
    public static double ChangeRatio = 1.25, MinSectionSeconds = 15;
    public static double SectionGain = 2;
    public static double SilenceShare = 0.005;

    private class Envelope
    {
        /// <summary>Onsets across the spectrum: what the tempo is read from.</summary>
        public double[] O;
        /// <summary>Onsets in the bass alone, where the beat itself is struck.</summary>
        public double[] L;
        public double Rate;
        public double T0;
        public double Background;
        /// <summary>When the song first rises out of silence, in seconds.</summary>
        public double MusicStart;
        public int Length { get { return O.Length; } }
        public double Time(double Index) { return T0 + Index / Rate; }
        public double Index(double Time) { return (Time - T0) * Rate; }
    }

    private class Part
    {
        public int From, To;
        public double Bpm, Phase;
    }

    public static Result Analyze(float[] Mono, int SampleRate, Action<float> Progress = null)
    {
        if (Mono == null || SampleRate <= 0) return null;
        Envelope Env = BuildEnvelope(Mono, SampleRate, Progress);
        if (Env == null || Env.Length < Env.Rate * 4) return null;

        double Coarse = CoarseTempo(Env.O, 0, Env.Length, Env.Rate);
        double Main = FitTempo(Env, 0, Env.Length, Coarse, true);
        if (Progress != null) Progress(0.9f);

        List<Part> Parts = Sections(Env, Main);
        if (Progress != null) Progress(0.97f);

        Result Res = new Result();
        Res.Bpm = Main;
        // The grid kept by the first stretch at the song's own tempo, carried
        // back to wherever the music starts.
        Part Anchor = Parts.Find(p => p.Bpm == Main) ?? Parts[0];
        double AnchorPhase = Parts.Count == 1 ? Phase(Env, 0, Env.Length, Main)
            : Anchor.Bpm == Main ? Anchor.Phase : Phase(Env, Anchor.From, Anchor.To, Main);
        Res.FirstBeat = FirstBeat(Env, Main, AnchorPhase);

        Res.Sections.Add(new Section
        {
            Time = Parts.Count == 1 ? Res.FirstBeat : FirstBeat(Env, Parts[0].Bpm, Parts[0].Phase),
            Bpm = Parts[0].Bpm
        });
        for (int i = 1; i < Parts.Count; ++i)
        {
            double Switch = SwitchTime(Env, Parts[i - 1], Parts[i]);
            if (Switch <= Res.Sections[Res.Sections.Count - 1].Time + 1) continue;
            Res.Sections.Add(new Section { Time = Switch, Bpm = Parts[i].Bpm });
        }
        if (Progress != null) Progress(1f);
        return Res;
    }

    // ---- Onset envelope ----------------------------------------------------

    private static Envelope BuildEnvelope(float[] Mono, int SampleRate, Action<float> Progress)
    {
        // Down to 16-24 kHz: nothing above that marks a beat, and it halves
        // the work for the usual 44.1 and 48 kHz songs.
        int Factor = Math.Max(1, (int)Math.Round(SampleRate / 22050.0));
        int Rate = SampleRate / Factor;
        int Length = Mono.Length / Factor;
        float[] X = new float[Length];
        double Energy = 0;
        for (int i = 0; i < Length; ++i)
        {
            float Sum = 0;
            int Base = i * Factor;
            for (int k = 0; k < Factor; ++k) Sum += Mono[Base + k];
            X[i] = Sum / Factor;
            Energy += X[i] * X[i];
        }
        double Rms = Math.Sqrt(Energy / Math.Max(1, Length));
        if (Rms < 1e-6) return null;

        // Silence is anything under half a per cent of the song's own level,
        // measured over 10 ms so a click in the lead-in does not count.
        double MusicStart = 0;
        int Block = Rate / 100;
        for (int b = 0; b + Block <= Length; b += Block)
        {
            double Sum = 0;
            for (int i = b; i < b + Block; ++i) Sum += X[i] * X[i];
            if (Math.Sqrt(Sum / Block) < Rms * SilenceShare) continue;
            MusicStart = (double)b / Rate;
            break;
        }
        float Gain = (float)(0.1 / Rms);

        int Frames = (Length - FftSize) / Hop;
        if (Frames < 8) return null;
        int Bins = Math.Min(FftSize / 2, (int)(11000.0 * FftSize / Rate));
        int LowBins = Math.Max(2, (int)(150.0 * FftSize / Rate));
        double[] Window = new double[FftSize];
        for (int i = 0; i < FftSize; ++i) Window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / FftSize);

        double[] Re = new double[FftSize], Im = new double[FftSize];
        // The last three frames of log magnitude, for a difference taken two
        // frames apart: one frame is 6 ms, too short for a soft attack to rise.
        double[][] Past = { new double[Bins], new double[Bins], new double[Bins] };
        double[] Flux = new double[Frames], LowFlux = new double[Frames];
        for (int f = 0; f < Frames; ++f)
        {
            int Start = f * Hop;
            for (int i = 0; i < FftSize; ++i)
            {
                Re[i] = X[Start + i] * Gain * Window[i];
                Im[i] = 0;
            }
            Fft(Re, Im);
            double[] Now = Past[f % 3];
            double[] Before = Past[(f + 1) % 3];
            double Sum = 0, Low = 0;
            for (int b = 1; b < Bins; ++b)
            {
                double Mag = Math.Sqrt(Re[b] * Re[b] + Im[b] * Im[b]);
                Now[b] = Math.Log(1 + Mag);
                if (f >= 2)
                {
                    double Rise = Now[b] - Before[b];
                    if (Rise > 0)
                    {
                        Sum += Rise;
                        if (b < LowBins) Low += Rise;
                    }
                }
            }
            Flux[f] = Sum;
            LowFlux[f] = Low;
            if (Progress != null && (f & 1023) == 0) Progress(0.75f * f / Frames);
        }

        double FrameRate = (double)Rate / Hop;
        double[] O = Detrend(Flux, FrameRate);
        double[] L = Detrend(LowFlux, FrameRate);
        Envelope Env = new Envelope { O = O, L = L, MusicStart = MusicStart, Rate = FrameRate, T0 = (FftSize / 2.0) / Rate + Latency };
        double Background = 0;
        int Count = 0;
        for (int i = 0; i < Frames; i += 5) { Background += OnsetAt(Env, Env.Time(i)); ++Count; }
        Env.Background = Background / Math.Max(1, Count);
        return Env;
    }

    /// <summary>
    /// Only what stands out from its surroundings is an onset: a loud passage
    /// raises everything, and a quiet one should still count. Scaled so the
    /// song's typical onset is 1, whatever its loudness.
    /// </summary>
    private static double[] Detrend(double[] Flux, double FrameRate)
    {
        int Frames = Flux.Length;
        int Half = (int)(0.25 * FrameRate);
        double[] O = new double[Frames];
        double Running = 0;
        int Lo = 0, Hi = -1;
        for (int f = 0; f < Frames; ++f)
        {
            while (Hi < Math.Min(Frames - 1, f + Half)) Running += Flux[++Hi];
            while (Lo < f - Half) Running -= Flux[Lo++];
            O[f] = Math.Max(0, Flux[f] - Running / (Hi - Lo + 1));
        }
        double Norm = 0;
        for (int f = 0; f < Frames; ++f) Norm += O[f] * O[f];
        Norm = Math.Sqrt(Norm / Frames);
        if (Norm > 0) for (int f = 0; f < Frames; ++f) O[f] /= Norm;
        return O;
    }

    private static void Fft(double[] Re, double[] Im)
    {
        int N = Re.Length;
        for (int i = 1, j = 0; i < N; ++i)
        {
            int Bit = N >> 1;
            for (; (j & Bit) != 0; Bit >>= 1) j ^= Bit;
            j ^= Bit;
            if (i < j)
            {
                double T = Re[i]; Re[i] = Re[j]; Re[j] = T;
                T = Im[i]; Im[i] = Im[j]; Im[j] = T;
            }
        }
        for (int Len = 2; Len <= N; Len <<= 1)
        {
            double Angle = -2 * Math.PI / Len;
            double WRe = Math.Cos(Angle), WIm = Math.Sin(Angle);
            for (int i = 0; i < N; i += Len)
            {
                double CRe = 1, CIm = 0;
                for (int k = 0; k < Len / 2; ++k)
                {
                    int A = i + k, B = i + k + Len / 2;
                    double TRe = Re[B] * CRe - Im[B] * CIm;
                    double TIm = Re[B] * CIm + Im[B] * CRe;
                    Re[B] = Re[A] - TRe; Im[B] = Im[A] - TIm;
                    Re[A] += TRe; Im[A] += TIm;
                    double NRe = CRe * WRe - CIm * WIm;
                    CIm = CRe * WIm + CIm * WRe;
                    CRe = NRe;
                }
            }
        }
    }

    // ---- Tempo -------------------------------------------------------------

    /// <summary>The envelope's autocorrelation, normalised so lag 0 is 1.</summary>
    private static double[] Autocorrelation(double[] O, int From, int To, int MaxLag)
    {
        int N = To - From;
        double Mean = 0;
        for (int i = From; i < To; ++i) Mean += O[i];
        Mean /= Math.Max(1, N);
        double[] Ac = new double[MaxLag + 1];
        for (int Lag = 0; Lag <= MaxLag && Lag < N; ++Lag)
        {
            double Sum = 0;
            for (int i = From; i + Lag < To; ++i) Sum += (O[i] - Mean) * (O[i + Lag] - Mean);
            Ac[Lag] = Sum / (N - Lag);
        }
        if (Ac[0] > 0) for (int Lag = MaxLag; Lag >= 0; --Lag) Ac[Lag] /= Ac[0];
        return Ac;
    }

    private static int MaxLag(double Rate) { return (int)(4 * 60.0 / MinBpm * Rate) + 2; }

    private static double At(double[] A, double X)
    {
        if (X < 0 || X >= A.Length - 1) return 0;
        int I = (int)X;
        double W = X - I;
        return A[I] * (1 - W) + A[I + 1] * W;
    }

    /// <summary>
    /// How strongly the envelope repeats every beat of a tempo, counting the
    /// next few beats and the half beat too, so a tempo whose every beat
    /// lands is preferred to one that only lands on alternate ones.
    /// </summary>
    private static double Strength(double[] Ac, double Rate, double Bpm)
    {
        double Lag = 60.0 / Bpm * Rate;
        return At(Ac, Lag) + At(Ac, 2 * Lag) / 2 + At(Ac, 3 * Lag) / 3 + At(Ac, 4 * Lag) / 4
            + At(Ac, Lag / 2) / 2;
    }

    /// <summary>
    /// Songs repeat at the beat, at half and at double the beat alike, so
    /// the choice between them leans on where tempos usually sit.
    /// </summary>
    private static double Prior(double Bpm)
    {
        double D = Math.Log(Bpm / PriorCenter, 2) / PriorWidth;
        return Math.Exp(-0.5 * D * D);
    }

    private static double CoarseTempo(double[] O, int From, int To, double Rate)
    {
        double[] Ac = Autocorrelation(O, From, To, MaxLag(Rate));
        double Best = 0, BestScore = double.MinValue;
        for (double B = MinBpm; B <= MaxBpm; B += 0.1)
        {
            double S = Strength(Ac, Rate, B) * Prior(B);
            if (S > BestScore) { BestScore = S; Best = B; }
        }
        return Best;
    }

    // ---- Folding -----------------------------------------------------------

    private const int FoldBins = 120;

    /// <summary>
    /// Folds a stretch of an envelope onto one beat of the given tempo.
    /// Answers the height of the tallest spot per beat, where in the beat it
    /// is, and how far it stands above the average.
    /// </summary>
    private static double Fold(Envelope Env, double[] O, int From, int To, double Bpm, out double Phase)
    {
        double[] S = Histogram(Env, O, From, To, Bpm);
        int Best = 0;
        for (int b = 1; b < FoldBins; ++b) if (S[b] > S[Best]) Best = b;
        Phase = Refine(S, Best) * 60.0 / Bpm / FoldBins;
        double Beats = (To - From) / Env.Rate / (60.0 / Bpm);
        return S[Best] / Math.Max(1, Beats);
    }

    /// <summary>A stretch of an envelope folded onto one beat, lightly smoothed.</summary>
    private static double[] Histogram(Envelope Env, double[] O, int From, int To, double Bpm)
    {
        double Period = 60.0 / Bpm;
        double[] H = new double[FoldBins];
        double Scale = FoldBins / Period;
        From = Math.Max(0, From); To = Math.Min(O.Length, To);
        for (int i = From; i < To; ++i)
        {
            double V = O[i];
            if (V <= 0) continue;
            double X = Env.Time(i) * Scale;
            X -= Math.Floor(X / FoldBins) * FoldBins;
            int B = (int)X;
            double W = X - B;
            H[B % FoldBins] += V * (1 - W);
            H[(B + 1) % FoldBins] += V * W;
        }
        double[] S = new double[FoldBins];
        for (int b = 0; b < FoldBins; ++b) S[b] = H[(b + FoldBins - 1) % FoldBins] + 2 * H[b] + H[(b + 1) % FoldBins];
        return S;
    }

    /// <summary>A histogram peak's position between bins, from the parabola through it and its neighbours.</summary>
    private static double Refine(double[] S, int Peak)
    {
        double L = S[(Peak + FoldBins - 1) % FoldBins], C = S[Peak], R = S[(Peak + 1) % FoldBins];
        double Den = L - 2 * C + R;
        return Peak + (Den < 0 ? 0.5 * (L - R) / Den : 0);
    }

    public static double PhaseCandidateShare = 0.6, BassLatency = 0.07;

    /// <summary>
    /// Where in the beat a stretch's beats fall.
    ///
    /// The onsets across the whole spectrum place an attack to a few ms, but
    /// the offbeat of a busy song often stacks up nearly as high as the beat,
    /// and which one wins is then a coin toss. The bass is what strikes the
    /// beat, so its onsets decide between the tallest candidates; they are
    /// not used for the position itself, because a low note's onset is only
    /// seen some 40-50 ms after its attack, and loosely.
    /// </summary>
    private static double Phase(Envelope Env, int From, int To, double Bpm)
    {
        double Period = 60.0 / Bpm;
        double[] S = Histogram(Env, Env.O, From, To, Bpm);
        double[] Bass = Histogram(Env, Env.L, From, To, Bpm);
        double Top = 0, BassTop = 0;
        for (int b = 0; b < FoldBins; ++b) { Top = Math.Max(Top, S[b]); BassTop = Math.Max(BassTop, Bass[b]); }
        if (Top <= 0) return 0;
        int Behind = (int)Math.Round(0.01 / Period * FoldBins), Ahead = (int)Math.Round(BassLatency / Period * FoldBins);

        int Best = 0;
        double BestScore = double.MinValue;
        for (int b = 0; b < FoldBins; ++b)
        {
            if (S[b] < Top * PhaseCandidateShare) continue;
            if (S[b] < S[(b + FoldBins - 1) % FoldBins] || S[b] < S[(b + 1) % FoldBins]) continue;
            double Support = 0;
            for (int k = -Behind; k <= Ahead; ++k) Support = Math.Max(Support, Bass[((b + k) % FoldBins + FoldBins) % FoldBins]);
            double Score = S[b] / Top + (BassTop > 0 ? Support / BassTop : 0);
            if (Score > BestScore) { BestScore = Score; Best = b; }
        }
        return Refine(S, Best) * Period / FoldBins;
    }

    /// <summary>
    /// A tempo's score over a stretch, folded in chunks that are added up
    /// rather than folded as one: a beat that jumps halfway through the song
    /// then costs nothing, where one long fold would pull the tempo off to
    /// meet both halves.
    /// </summary>
    private static double ChunkedScore(Envelope Env, int From, int To, double Bpm)
    {
        int Chunk = (int)(20 * Env.Rate);
        double Sum = 0;
        for (int S = From; S < To; S += Chunk)
        {
            int E = Math.Min(To, S + Chunk);
            if (E - S < Chunk / 2 && S != From) E = To;
            double Beats = (E - S) / Env.Rate * Bpm / 60.0;
            Sum += Fold(Env, Env.O, S, E, Bpm, out double _) * Beats;
            if (E == To) break;
        }
        return Sum;
    }

    /// <summary>
    /// The exact tempo of a stretch, rounded to a whole or half bpm when the
    /// song fits that as well: songs are made at round tempos, and a tempo
    /// off by a hundredth drifts across a whole song.
    /// </summary>
    private static double FitTempo(Envelope Env, int From, int To, double Center, bool Chunked)
    {
        Func<double, double> Score = B => Chunked ? ChunkedScore(Env, From, To, B) : Fold(Env, Env.O, From, To, B, out double _);
        double Best = Center, BestScore = double.MinValue;
        double Range = Center * 0.015;
        for (double B = Center - Range; B <= Center + Range; B += 0.02)
        {
            double S = Score(B);
            if (S > BestScore) { BestScore = S; Best = B; }
        }
        double Fine = Best;
        for (double B = Fine - 0.03; B <= Fine + 0.03; B += 0.002)
        {
            double S = Score(B);
            if (S > BestScore) { BestScore = S; Best = B; }
        }
        foreach (double Round in new[] { Math.Round(Best), Math.Round(Best * 2) / 2 })
        {
            if (Math.Abs(Round - Best) > RoundDistance) continue;
            if (Score(Round) >= BestScore * RoundTolerance) return Round;
        }
        return Math.Round(Best, 2);
    }

    public static double RoundTolerance = 0.97, RoundDistance = 0.15;

    // ---- Sections ----------------------------------------------------------

    /// <summary>
    /// Stretches of the song each keeping one tempo and one beat. The song
    /// is looked at through a sliding window; a window only counts as keeping
    /// another tempo when that tempo beats the song's own clearly, and only
    /// as keeping another beat when its own beat stands out clearly. Anything
    /// shorter than a section is swallowed by what surrounds it, and a
    /// section is only kept if its own grid fits it clearly better than the
    /// grid it would otherwise inherit.
    /// </summary>
    private static List<Part> Sections(Envelope Env, double Main)
    {
        int N = Env.Length;
        int W = (int)(WindowSeconds * Env.Rate), H = (int)(WindowHop * Env.Rate);
        List<Part> Windows = new List<Part>();
        for (int S = 0; S + W <= N; S += H)
        {
            double[] Ac = Autocorrelation(Env.O, S, S + W, MaxLag(Env.Rate));
            double Own = Strength(Ac, Env.Rate, Main);
            double Best = Main, BestScore = Own;
            for (double B = Main * 0.6; B <= Main * 1.67; B += 0.1)
            {
                if (Math.Abs(B / Main - 1) < 0.02) continue;
                double Sc = Strength(Ac, Env.Rate, B);
                if (Sc > BestScore) { BestScore = Sc; Best = B; }
            }
            if (Best != Main) Best = NearestOctave(Ac, Env.Rate, Best, Main);
            double Tempo = Math.Abs(Best / Main - 1) >= 0.02 && BestScore > Math.Max(Own, 0.05) * ChangeRatio ? Best : Main;
            Windows.Add(new Part { From = S, To = S + W, Bpm = Tempo });
        }

        List<Part> Parts = new List<Part>();
        // Runs of windows that agree, each window standing for its middle.
        foreach (Part Win in Windows)
        {
            int Mid = (Win.From + Win.To) / 2;
            Part Last = Parts.Count > 0 ? Parts[Parts.Count - 1] : null;
            if (Last != null && Agree(Last, Win)) { Last.To = Mid + H / 2; continue; }
            Parts.Add(new Part { From = Last == null ? 0 : Mid - H / 2, To = Mid + H / 2, Bpm = Win.Bpm });
        }
        if (Parts.Count == 0) Parts.Add(new Part { From = 0, To = N, Bpm = Main });
        Parts[Parts.Count - 1].To = N;

        // Too short to be a section: swallowed by the longer neighbour.
        int MinFrames = (int)(MinSectionSeconds * Env.Rate);
        while (Parts.Count > 1)
        {
            int Shortest = -1;
            for (int i = 0; i < Parts.Count; ++i)
            {
                if (Parts[i].To - Parts[i].From >= MinFrames) continue;
                if (Shortest < 0 || Parts[i].To - Parts[i].From < Parts[Shortest].To - Parts[Shortest].From) Shortest = i;
            }
            if (Shortest < 0) break;
            int Into = Shortest == 0 ? 1 : Shortest == Parts.Count - 1 ? Shortest - 1
                : (Parts[Shortest - 1].To - Parts[Shortest - 1].From >= Parts[Shortest + 1].To - Parts[Shortest + 1].From ? Shortest - 1 : Shortest + 1);
            Parts[Into].From = Math.Min(Parts[Into].From, Parts[Shortest].From);
            Parts[Into].To = Math.Max(Parts[Into].To, Parts[Shortest].To);
            Parts.RemoveAt(Shortest);
            MergeAgreeing(Parts);
        }

        // Each section pinned down on its own stretch.
        foreach (Part P in Parts)
        {
            double Tempo = Main;
            if (Math.Abs(P.Bpm / Main - 1) >= 0.02)
            {
                double[] Ac = Autocorrelation(Env.O, P.From, P.To, MaxLag(Env.Rate));
                Tempo = FitTempo(Env, P.From, P.To, NearestOctave(Ac, Env.Rate, P.Bpm, Main), false);
            }
            P.Bpm = Tempo;
            P.Phase = Phase(Env, P.From, P.To, Tempo);
        }
        MergeAgreeing(Parts);

        // A boundary has to earn its line: on each side, that side's own grid
        // must fit clearly better than the other side's grid carried over.
        // The weakest boundary goes first and the merged stretch is fitted
        // again, since one false section skews the check of its neighbours.
        while (Parts.Count > 1)
        {
            int Weakest = -1;
            double WeakestMargin = double.MaxValue;
            for (int i = 1; i < Parts.Count; ++i)
            {
                double Fit = Math.Min(Margin(Env, Parts[i], Parts[i - 1]), Margin(Env, Parts[i - 1], Parts[i]));
                if (Fit < WeakestMargin) { WeakestMargin = Fit; Weakest = i; }
            }
            if (WeakestMargin >= SectionGain) break;
            Part A = Parts[Weakest - 1], B = Parts[Weakest];
            // The merged stretch keeps whichever grid fits all of it better.
            double FitA = GridScore(Env, A.Bpm, A.Phase, A.From, B.To);
            double FitB = GridScore(Env, B.Bpm, B.Phase, A.From, B.To);
            A.Bpm = FitA >= FitB ? A.Bpm : B.Bpm;
            A.To = B.To;
            A.Phase = Phase(Env, A.From, A.To, A.Bpm);
            Parts.RemoveAt(Weakest);
        }
        return Parts;
    }

    /// <summary>
    /// How many times better a section's own grid fits it than a
    /// neighbour's grid carried into it. Grids below the background count as
    /// a little above it, so a section with nothing to keep time by never
    /// earns a line of its own.
    /// </summary>
    private static double Margin(Envelope Env, Part Own, Part Other)
    {
        double Mine = GridScore(Env, Own.Bpm, Own.Phase, Own.From, Own.To);
        double Theirs = GridScore(Env, Other.Bpm, Other.Phase, Own.From, Own.To);
        return Mine / Math.Max(Theirs, 0.1);
    }

    /// <summary>Same tempo, or the same pulse counted at half or double speed.</summary>
    private static bool Agree(Part A, Part B)
    {
        double Ratio = A.Bpm / B.Bpm;
        return Math.Abs(Ratio - 1) < 0.02 || Math.Abs(Ratio - 2) < 0.04 || Math.Abs(Ratio - 0.5) < 0.01;
    }

    /// <summary>
    /// Of a tempo and its half and double, the one a charter would write:
    /// whichever the stretch repeats at almost as strongly and that sits
    /// nearest the song's own tempo.
    /// </summary>
    private static double NearestOctave(double[] Ac, double Rate, double Bpm, double Main)
    {
        double Score = Strength(Ac, Rate, Bpm), Best = Bpm;
        double Top = Score;
        foreach (double Octave in new[] { Bpm * 2, Bpm / 2 }) Top = Math.Max(Top, Strength(Ac, Rate, Octave));
        foreach (double Octave in new[] { Bpm, Bpm * 2, Bpm / 2 })
        {
            if (Octave < MinBpm || Octave > MaxBpm) continue;
            if (Strength(Ac, Rate, Octave) < Top * 0.85) continue;
            if (Math.Abs(Math.Log(Octave / Main)) < Math.Abs(Math.Log(Best / Main)) || Strength(Ac, Rate, Best) < Top * 0.85) Best = Octave;
        }
        return Best;
    }

    private static void MergeAgreeing(List<Part> Parts)
    {
        for (int i = Parts.Count - 1; i > 0; --i)
        {
            if (!Agree(Parts[i - 1], Parts[i])) continue;
            Parts[i - 1].To = Parts[i].To;
            Parts.RemoveAt(i);
        }
    }

    // ---- Beats -------------------------------------------------------------

    /// <summary>How much onset sits on a moment, allowing for a few ms of play.</summary>
    private static double OnsetAt(Envelope Env, double Time)
    {
        int C = (int)Math.Round(Env.Index(Time));
        double Max = 0;
        for (int i = C - 3; i <= C + 3; ++i)
            if (i >= 0 && i < Env.O.Length && Env.O[i] > Max) Max = Env.O[i];
        return Max;
    }

    /// <summary>How far a grid's beats stand above the song's background, per beat.</summary>
    private static double GridScore(Envelope Env, double Bpm, double Phase, int From, int To)
    {
        double Period = 60.0 / Bpm;
        double Start = Env.Time(From), End = Env.Time(To);
        double Sum = 0;
        int Count = 0;
        for (double T = Phase + Math.Ceiling((Start - Phase) / Period) * Period; T < End; T += Period)
        {
            Sum += OnsetAt(Env, T) - Env.Background;
            ++Count;
        }
        return Count > 0 ? Sum / Count : 0;
    }

    /// <summary>
    /// The first beat of a grid at or after the moment the song starts
    /// sounding, soft intro or not: a charter puts the first line where the
    /// music begins. The allowance keeps a beat struck right at the
    /// start from being skipped: the onset is seen from the first sign of
    /// the attack, the level only once the attack has risen.
    /// </summary>
    private static double FirstBeat(Envelope Env, double Bpm, double Phase)
    {
        double Period = 60.0 / Bpm;
        double From = Math.Max(0, Env.MusicStart - 0.15 * Period);
        return Phase + Math.Ceiling((From - Phase) / Period) * Period;
    }

    /// <summary>
    /// Where one section hands over to the next: the beat of the new grid
    /// that best splits the stretch around the boundary into beats of the
    /// old grid before it and beats of the new one after it.
    /// </summary>
    private static double SwitchTime(Envelope Env, Part A, Part B)
    {
        double Boundary = Env.Time(B.From);
        double Lo = Boundary - WindowSeconds, Hi = Boundary + WindowSeconds;
        double PA = 60.0 / A.Bpm, PB = 60.0 / B.Bpm;

        List<double> BeatsB = new List<double>();
        for (double T = B.Phase + Math.Ceiling((Lo - B.Phase) / PB) * PB; T <= Hi; T += PB) BeatsB.Add(T);
        List<double> BeatsA = new List<double>();
        for (double T = A.Phase + Math.Ceiling((Lo - A.Phase) / PA) * PA; T <= Hi; T += PA) BeatsA.Add(T);
        double[] OnA = BeatsA.ConvertAll(T => OnsetAt(Env, T) - Env.Background).ToArray();
        double[] OnB = BeatsB.ConvertAll(T => OnsetAt(Env, T) - Env.Background).ToArray();

        double BestTime = BeatsB.Count > 0 ? BeatsB[0] : Boundary, BestScore = double.MinValue;
        for (int k = 0; k < BeatsB.Count; ++k)
        {
            double S = 0;
            for (int j = 0; j < BeatsA.Count && BeatsA[j] < BeatsB[k] - 0.001; ++j) S += OnA[j];
            for (int j = k; j < BeatsB.Count; ++j) S += OnB[j];
            if (S > BestScore) { BestScore = S; BestTime = BeatsB[k]; }
        }
        return BestTime;
    }
}
