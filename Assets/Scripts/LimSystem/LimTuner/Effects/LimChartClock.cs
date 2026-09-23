using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Two readings of the chart at a moment, shared by the UiTweak features:
/// the combo reached (the hit effects' counter and the header's score) and
/// where in its beat the moment falls (the judge line's glow and the wave).
/// </summary>
public static class LimChartClock
{
    /// <summary>
    /// The combo at a moment, every note being a perfect hit: every tap
    /// already passed, and every hold's head, its ticks every half beat (30 /
    /// bpm, the bpm where the hold starts, as Flowaria's plugin counted them)
    /// and its end once passed. <see cref="float.MaxValue"/> gives the
    /// chart's whole count.
    /// </summary>
    public static int Combo(LimTunerManager Tuner, float Now)
    {
        int Count = 0;
        foreach (Lanotalium.Chart.LanotaTapNote Note in Tuner.TapNoteManager.TapNote) if (Note.Time <= Now) ++Count;
        foreach (Lanotalium.Chart.LanotaHoldNote Note in Tuner.HoldNoteManager.HoldNote)
        {
            if (Note.Time > Now) continue;
            float End = Note.Time + Note.Duration;
            float Elapsed = Mathf.Min(Now, End) - Note.Time;
            float Bpm = Tuner.BpmManager.CalculateBpm(Note.Time);
            float Step = Bpm > 0 ? 30f / Bpm : 0;
            // The plugin counted k = 0, 1, 2... while k steps fell short of
            // the time held by more than 0.0003 s; this is that count.
            int Ticks = Step > 0 ? Mathf.Max(1, Mathf.CeilToInt((Elapsed - 0.0003f) / Step)) : 1;
            if (Now > End) ++Ticks;
            Count += Ticks;
        }
        return Count;
    }

    /// <summary>
    /// Where in its beat a moment of the chart falls, 0 on the beat. The first
    /// bpm's beats count from 0 whatever its own time, as the beatlines do;
    /// every later one's from where it starts.
    /// </summary>
    public static float BeatPhase(LimBpmManager Bpms, float Time)
    {
        List<Lanotalium.Chart.LanotaChangeBpm> List = Bpms != null ? Bpms.Bpm : null;
        if (List == null || List.Count == 0) return 0;
        int Index = 0;
        for (int i = 1; i < List.Count; ++i)
        {
            if (List[i].Time <= Time) Index = i;
            else break;
        }
        float Start = Index == 0 ? 0 : List[Index].Time;
        float Bpm = List[Index].Bpm;
        if (Bpm <= 0) return 0;
        return Mathf.Repeat((Time - Start) * Bpm / 60f, 1f);
    }
}
