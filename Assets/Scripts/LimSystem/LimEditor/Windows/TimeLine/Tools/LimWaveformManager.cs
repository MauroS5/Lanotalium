using System;
using UnityEngine;

/// <summary>
/// The waveform strip along the bottom of the TimeLine window.
///
/// It used to keep the whole song in memory as loose samples, two lists of
/// floats, one per channel: some eighty megabytes for a four minute song, and
/// the reason for the "waveform disabled by the 32-bit memory limit" warning.
/// It also redrew every frame by taking one sample in every N, which is not a
/// waveform but decimated noise, and it only ever drew from the playhead
/// forward, as far as a separator that had to be dragged about by hand.
///
/// What is kept now is an envelope: the quietest and loudest value in every
/// millisecond of the song, worked out once while the song loads by reading
/// the audio in small blocks. A five minute song comes to a couple of
/// megabytes whatever its sample rate, which fits comfortably in a 32-bit
/// process, and drawing is a matter of reading back the part on screen.
///
/// The strip itself is now one waveform rather than a left and a right one,
/// it is always in place instead of following the playhead, and it has its
/// own zoom: the wheel over it goes from the whole song down to a second or
/// so, without touching the TimeLine's own scale. Clicking or dragging on it
/// moves the playhead there, which is what makes it useful as a map of the
/// song. It can be switched off in Preferences.
///
/// Sync Both, the switch beside Waveform in the title bar, ties the two zooms
/// together instead: the strip then shows the same stretch of song the
/// TimeLine shows, starting at the playhead, and the wheel over either of
/// them moves both. See LimTimeLineSyncZoom.
/// </summary>
public class LimWaveformManager : MonoBehaviour
{
    public LimTimeLineManager TimeLineManager;
    public LimTunerManager TunerManager;
    /// <summary>The waveform, and the line marking where the playhead is.</summary>
    public LineRenderer LineL, LineR;
    /// <summary>The old draggable separator, kept only so it can be put away.</summary>
    public RectTransform Blocker;

    /// <summary>
    /// Where the strip starts, measured down from the top of the window. Four
    /// rows of motions fit above it: three are in the scene and the fourth,
    /// transparency, is the row this leaves empty for it.
    /// </summary>
    public const float StripTop = 150f;
    /// <summary>How tall it is: what is left of the window below those rows.</summary>
    public const float StripHeight = 70f;
    /// <summary>The label column down the left, shared with the rest of the TimeLine.</summary>
    private const float LabelColumn = 200f;

    /// <summary>One envelope entry per millisecond of song.</summary>
    private const int BucketsPerSecond = 1000;
    /// <summary>Read this many frames of audio at a time while building it.</summary>
    private const int ReadBlockFrames = 1 << 16;
    /// <summary>The closest the wheel can zoom in, in seconds across the strip.</summary>
    private const float MinVisibleSeconds = 1f;
    /// <summary>The limits the TimeLine puts on its own scale, kept the same here.</summary>
    private const float TimeLineScaleFloor = 10f;
    private const float TimeLineScaleCeiling = 10000f;
    private const float ZoomStep = 1.3f;
    /// <summary>One column a pixel, up to a limit no window reaches.</summary>
    private const int MaxColumns = 2048;
    private const float Margin = 4f;

    /// <summary>How thick the waveform and the playhead line are, in pixels.</summary>
    private const float WaveformPixels = 1.5f;
    private const float MarkerPixels = 2f;
    /// <summary>How wide the pin at the foot of the playhead line is.</summary>
    private const float MarkerPinPixels = 10f;
    /// <summary>
    /// The playhead line is drawn as several points rather than two, so that
    /// the width curve that shapes the pin has somewhere to be sampled.
    /// </summary>
    private const int MarkerSteps = 16;

    private float[] EnvelopeMin, EnvelopeMax;
    private int EnvelopeCount;

    private Vector3[] Points = new Vector3[0];
    private Vector3[] MarkerPoints = new Vector3[MarkerSteps + 1];
    private RectTransform Strip, Marker;
    /// <summary>The material the scene gave these lines, kept as the one to tint from.</summary>
    private Material Paper;
    private bool LayoutReady;
    private bool Visible = true;

    private float VisibleSeconds;
    private bool Seeking;
    private float FrozenStart;
    /// <summary>Where a drag left the view, kept so that letting go moves nothing.</summary>
    private bool Parked;
    private float ParkedStart;

    private float DrawnStart = float.NaN, DrawnSpan, DrawnWidth, DrawnAmplitude;
    private int DrawnColumns;
    private Color DrawnColor;

    /// <summary>
    /// Walks the song once and keeps the loudest and quietest value in every
    /// millisecond. Read in blocks: the point of this is never to hold the
    /// whole song in memory at once.
    /// </summary>
    public void OnMusicLoaded()
    {
        EnvelopeCount = 0;
        EnvelopeMin = null;
        EnvelopeMax = null;
        if (LimSystem.ChartContainer == null) return;
        if (LimSystem.ChartContainer.ChartMusic == null) return;

        AudioClip Clip = LimSystem.ChartContainer.ChartMusic.Music;
        if (Clip == null || Clip.samples <= 0 || Clip.frequency <= 0) return;

        try
        {
            int Channels = Mathf.Max(1, Clip.channels);
            int Frequency = Clip.frequency;
            int Frames = Clip.samples;
            int Buckets = (int)(((long)Frames * BucketsPerSecond) / Frequency) + 1;

            float[] Low = new float[Buckets];
            float[] High = new float[Buckets];
            // Worked out once: this multiply happens for every sample of the
            // song, several million of them.
            double Step = (double)BucketsPerSecond / Frequency;

            float[] Block = new float[ReadBlockFrames * Channels];
            int Frame = 0;
            while (Frame < Frames)
            {
                int Wanted = Mathf.Min(ReadBlockFrames, Frames - Frame);
                // The last block is shorter: asking for more than is left
                // wraps round to the beginning of the song.
                if (Wanted != ReadBlockFrames) Block = new float[Wanted * Channels];
                if (!Clip.GetData(Block, Frame)) break;

                for (int i = 0; i < Wanted; ++i)
                {
                    float Sum = 0;
                    int Base = i * Channels;
                    for (int c = 0; c < Channels; ++c) Sum += Block[Base + c];
                    float Value = Sum / Channels;

                    int Bucket = (int)((Frame + i) * Step);
                    if (Bucket < 0 || Bucket >= Buckets) continue;
                    if (Value < Low[Bucket]) Low[Bucket] = Value;
                    if (Value > High[Bucket]) High[Bucket] = Value;
                }
                Frame += Wanted;
            }

            EnvelopeMin = Low;
            EnvelopeMax = High;
            EnvelopeCount = Buckets;
            Block = null;
            GC.Collect();
        }
        catch (Exception)
        {
            // A clip that cannot be read is no reason to interrupt the
            // session: the strip simply stays empty.
            EnvelopeMin = null;
            EnvelopeMax = null;
            EnvelopeCount = 0;
        }
        Redraw();
    }

    private void Update()
    {
        if (LimSystem.ChartContainer == null) return;
        SyncVisibility();
        if (!EnsureLayout()) return;
        if (!Visible) return;
        DetectZoom();
        DetectSeek();
        UpdateWaveform();
        UpdateMarker();
    }

    /// <summary>
    /// Follows the preference wherever it is changed from: the switch on the
    /// TimeLine and the one in Preferences are the same setting, and either
    /// of them may have moved since the last frame.
    /// </summary>
    private void SyncVisibility()
    {
        if (LimSystem.Preferences.Waveform == Visible) return;
        if (LimSystem.Preferences.Waveform) Show(); else Hide();
    }

    /// <summary>
    /// Puts the two old rows together into one strip the first time round.
    /// The right channel's line is kept: it draws the playhead now, and it
    /// already has the material and the sorting the strip needs.
    /// </summary>
    private bool EnsureLayout()
    {
        if (LayoutReady) return true;
        if (LineL == null || LineR == null) return false;

        Strip = LineL.transform.parent as RectTransform;
        if (Strip == null) return false;

        RectTransform OldRight = LineR.transform.parent as RectTransform;
        Marker = LineR.transform as RectTransform;
        RectTransform Source = LineL.transform as RectTransform;
        Marker.SetParent(Strip, false);
        Marker.anchorMin = Source.anchorMin;
        Marker.anchorMax = Source.anchorMax;
        Marker.pivot = Source.pivot;
        Marker.anchoredPosition = Source.anchoredPosition;
        Marker.sizeDelta = Source.sizeDelta;
        if (OldRight != null && OldRight != Strip) OldRight.gameObject.SetActive(false);

        Strip.anchoredPosition = new Vector2(Strip.anchoredPosition.x, -StripTop);
        Strip.sizeDelta = new Vector2(Strip.sizeDelta.x, StripHeight);
        ShapeMarker();
        // Tints are made from this, never from whatever is on the line now:
        // tinting an already tinted copy would leave a new material behind
        // every time the colour changed.
        Paper = LineL.sharedMaterial;
        // Nothing is blocked off any more: the strip runs the whole width.
        if (Blocker != null) Blocker.gameObject.SetActive(false);

        LayoutReady = true;
        // The zoom is remembered between sessions, and the preferences have
        // certainly been read by the time a song is open.
        VisibleSeconds = LimSystem.Preferences.WaveformVisibleSeconds;
        // Whether it should be on screen at all was decided before this ran.
        Strip.gameObject.SetActive(Visible);
        return true;
    }

    /// <summary>
    /// Line widths are in world units and this canvas is a good deal wider
    /// than one unit to the pixel, which is why a width of 1 came out six
    /// pixels thick. The scale of the strip turns a width in pixels into the
    /// number the renderer wants, at whatever resolution the editor runs.
    /// </summary>
    private float InPixels(float Pixels)
    {
        float Scale = Strip != null ? Mathf.Abs(Strip.lossyScale.x) : 0f;
        if (Scale <= 0.0001f) return Pixels * 0.16f;
        return Pixels * Scale;
    }

    /// <summary>
    /// Gives the playhead line the shape ArcCreate uses: a hair-thin line
    /// down the strip with a pin at its foot, wide enough to see and to aim
    /// at. It is one line rather than a line and a separate marker, so the
    /// two can never be drawn a pixel apart or in the wrong order.
    ///
    /// The width along the line is a curve, flat for most of the way down and
    /// flaring out near the bottom. The tangents are set by hand because a
    /// curve left to smooth itself would swell the whole line.
    /// </summary>
    private void ShapeMarker()
    {
        if (LineR == null) return;
        float Thin = MarkerPixels / MarkerPinPixels;
        float Rise = (1f - Thin) / 0.1f;
        float Fall = (0.05f - 1f) / 0.1f;
        LineR.widthCurve = new AnimationCurve(
            new Keyframe(0f, Thin, 0f, 0f),
            new Keyframe(0.8f, Thin, 0f, Rise),
            new Keyframe(0.9f, 1f, Rise, Fall),
            new Keyframe(1f, 0.05f, Fall, 0f));
    }

    private float SongLength
    {
        get
        {
            if (TunerManager == null || TunerManager.MediaPlayerManager == null) return 0;
            return TunerManager.MediaPlayerManager.Length;
        }
    }
    private float StripWidth
    {
        get
        {
            if (TimeLineManager == null || TimeLineManager.ViewRect == null) return 0;
            return TimeLineManager.ViewRect.sizeDelta.x - LabelColumn;
        }
    }
    /// <summary>True while the strip is tied to the TimeLine's own zoom.</summary>
    private bool Synced
    {
        get { return LimSystem.Preferences.WaveformSyncZoom && TimeLineManager != null && TimeLineManager.Scale > 0; }
    }

    /// <summary>
    /// How many seconds are on screen. Tied to the TimeLine, that is however
    /// many seconds its own scale puts across the same width, so the two read
    /// as one picture; on its own, the zoom the wheel was left at, clamped to
    /// a song that may be shorter than it.
    /// </summary>
    private float Span
    {
        get
        {
            float Length = SongLength;
            if (Length <= 0) return 0;
            if (Synced) return Mathf.Max(0.05f, StripWidth / TimeLineManager.Scale);
            if (VisibleSeconds <= 0) return Length;
            return Mathf.Clamp(VisibleSeconds, Mathf.Min(MinVisibleSeconds, Length), Length);
        }
    }
    /// <summary>
    /// The playhead sits in the middle of the strip while it is zoomed in,
    /// except during a drag, where the view is held still so that the pointer
    /// keeps hold of the same moment in the song.
    /// </summary>
    private float ViewStart
    {
        get
        {
            // Held still while the pointer is down, or the song would slide
            // out from under it as the playhead follows the drag.
            if (Seeking) return FrozenStart;
            float Length = SongLength;
            float Width = Span;
            // Tied to the TimeLine, the strip starts where the TimeLine
            // starts: the playhead, at the left edge of both.
            if (Synced) return TunerManager.ChartTime;
            if (Length <= 0 || Width >= Length) return 0;

            // A stretch being worked on stays put: letting go of a drag would
            // otherwise slide the picture across to re-centre it. Playing the
            // song takes the view back, since following the playhead smoothly
            // is the point of it while it runs, and so does sending the
            // playhead somewhere off the strip.
            if (Parked)
            {
                bool Playing = TunerManager.MediaPlayerManager != null && TunerManager.MediaPlayerManager.IsPlaying;
                float Kept = Mathf.Clamp(ParkedStart, 0, Length - Width);
                if (!Playing && TunerManager.ChartTime >= Kept && TunerManager.ChartTime <= Kept + Width) return Kept;
                Parked = false;
            }
            return Mathf.Clamp(TunerManager.ChartTime - Width / 2f, 0, Length - Width);
        }
    }

    /// <summary>
    /// Whether the pointer is over the strip. The TimeLine asks before
    /// zooming, since the wheel there belongs to the waveform; with the
    /// waveform switched off that part of the window is the TimeLine's again.
    /// </summary>
    public bool IsMouseOverStrip()
    {
        if (!Visible) return false;
        if (TimeLineManager == null || TimeLineManager.ViewRect == null) return false;
        RectTransform View = TimeLineManager.ViewRect;
        Vector3 Mouse = LimMousePosition.MousePosition;
        if (Mouse.x < View.anchoredPosition.x + LabelColumn) return false;
        if (Mouse.x > View.anchoredPosition.x + View.sizeDelta.x) return false;
        if (Mouse.y > View.anchoredPosition.y - StripTop) return false;
        if (Mouse.y < View.anchoredPosition.y - StripTop - StripHeight) return false;
        return true;
    }

    /// <summary>
    /// The wheel over the strip zooms the strip, from the whole song down to
    /// a second across. The TimeLine's own scale is left alone, so the two
    /// can be read at different sizes at the same time.
    /// </summary>
    private void DetectZoom()
    {
        if (!IsMouseOverStrip()) return;
        float Scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Scroll == 0) return;
        float Length = SongLength;
        if (Length <= 0) return;

        if (Synced)
        {
            // One zoom between them: the wheel over the strip moves the
            // TimeLine's scale, and the strip follows it back.
            float Scale = Scroll > 0 ? TimeLineManager.Scale * ZoomStep : TimeLineManager.Scale / ZoomStep;
            TimeLineManager.Scale = Mathf.Clamp(Scale, TimeLineScaleFloor, TimeLineScaleCeiling);
            TimeLineManager.ApplyScale();
            return;
        }

        Parked = false;
        float Current = Span;
        Current = Scroll > 0 ? Current / ZoomStep : Current * ZoomStep;
        VisibleSeconds = Mathf.Clamp(Current, Mathf.Min(MinVisibleSeconds, Length), Length);
        // Zoomed all the way out means the whole song, however long the next
        // song turns out to be.
        LimSystem.Preferences.WaveformVisibleSeconds = VisibleSeconds >= Length ? 0 : VisibleSeconds;
    }

    /// <summary>
    /// Clicking or dragging on the strip moves the playhead to that point of
    /// the song. Alt belongs to box selection, and is left alone.
    /// </summary>
    private void DetectSeek()
    {
        if (TunerManager == null || TunerManager.MediaPlayerManager == null) return;
        if (Input.GetMouseButtonUp(0) && Seeking)
        {
            Seeking = false;
            ParkedStart = FrozenStart;
            Parked = true;
        }
        if (Input.GetMouseButtonDown(0) && IsMouseOverStrip())
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return;
            // Read before the flag is raised: ViewStart answers with the
            // frozen value once Seeking is true, and freezing it first left
            // the strip pinned to the start of the song, which is where every
            // drag ended up.
            FrozenStart = ViewStart;
            Seeking = true;
        }
        if (!Seeking) return;
        if (!Input.GetMouseButton(0))
        {
            Seeking = false;
            ParkedStart = FrozenStart;
            Parked = true;
            return;
        }

        float Width = StripWidth;
        float Length = SongLength;
        if (Width <= 0 || Length <= 0) return;
        float Left = TimeLineManager.ViewRect.anchoredPosition.x + LabelColumn;
        float Percent = Mathf.Clamp01((LimMousePosition.MousePosition.x - Left) / Width);
        TunerManager.MediaPlayerManager.Time = Mathf.Clamp(ViewStart + Percent * Span, 0, Length);
    }

    /// <summary>
    /// Draws the part of the envelope on screen as one column per pixel, each
    /// column a stroke from its quietest value to its loudest. Redrawn only
    /// when something has actually moved: zoomed all the way out, which is how
    /// it starts, the picture is the same from one frame to the next.
    /// </summary>
    private void UpdateWaveform()
    {
        if (LineL == null) return;
        float Width = StripWidth;
        if (EnvelopeCount <= 0 || Width <= 0 || SongLength <= 0)
        {
            if (LineL.positionCount != 0) LineL.positionCount = 0;
            return;
        }

        int Columns = Mathf.Clamp(Mathf.RoundToInt(Width), 32, MaxColumns);
        float Start = ViewStart;
        float Seconds = Span;
        float Amplitude = StripHeight / 2f - Margin;
        Color Paint = LimWaveformPalette.Get(LimSystem.Preferences.WaveformColor);

        bool Same = Columns == DrawnColumns && Mathf.Approximately(Start, DrawnStart) &&
                    Mathf.Approximately(Seconds, DrawnSpan) && Mathf.Approximately(Width, DrawnWidth) &&
                    Mathf.Approximately(Amplitude, DrawnAmplitude) && Paint == DrawnColor;
        if (Same) return;

        if (Points.Length != Columns * 2) Points = new Vector3[Columns * 2];
        for (int c = 0; c < Columns; ++c)
        {
            float From = Start + Seconds * c / Columns;
            float To = Start + Seconds * (c + 1) / Columns;
            int First = (int)(From * BucketsPerSecond);
            int Last = (int)(To * BucketsPerSecond);

            float Low = 0, High = 0;
            // Columns past either end of the song stay flat. Tied to the
            // TimeLine the view can run off the end, and reading the last
            // bucket over and over would draw a block there instead.
            if (First < EnvelopeCount && Last > 0)
            {
                First = Mathf.Clamp(First, 0, EnvelopeCount - 1);
                Last = Mathf.Clamp(Last, First + 1, EnvelopeCount);
                for (int b = First; b < Last; ++b)
                {
                    if (EnvelopeMin[b] < Low) Low = EnvelopeMin[b];
                    if (EnvelopeMax[b] > High) High = EnvelopeMax[b];
                }
            }

            float X = Width * c / Columns;
            Points[c * 2] = new Vector3(X, High * Amplitude, 0);
            Points[c * 2 + 1] = new Vector3(X, Low * Amplitude, 0);
        }

        LineL.positionCount = Points.Length;
        LineL.SetPositions(Points);
        LineL.widthMultiplier = InPixels(WaveformPixels);
        LimLineColor.Apply(LineL, Paper, Paint);

        DrawnColumns = Columns;
        DrawnStart = Start;
        DrawnSpan = Seconds;
        DrawnWidth = Width;
        DrawnAmplitude = Amplitude;
        DrawnColor = Paint;
    }

    /// <summary>Where the playhead is, since the strip itself does not move.</summary>
    private void UpdateMarker()
    {
        if (LineR == null) return;
        float Width = StripWidth;
        float Length = SongLength;
        if (EnvelopeCount <= 0 || Width <= 0 || Length <= 0)
        {
            if (LineR.positionCount != 0) LineR.positionCount = 0;
            return;
        }

        float Height = StripHeight / 2f - Margin / 2f;
        float Percent = Mathf.Clamp01((TunerManager.ChartTime - ViewStart) / Mathf.Max(0.0001f, Span));
        float X = Width * Percent;
        // Several points down the same line, so the width curve has room to
        // widen into the pin at the foot of it.
        for (int i = 0; i <= MarkerSteps; ++i)
        {
            MarkerPoints[i] = new Vector3(X, Mathf.Lerp(Height, -Height, i * 1f / MarkerSteps), 0);
        }
        if (LineR.positionCount != MarkerPoints.Length) LineR.positionCount = MarkerPoints.Length;
        LineR.SetPositions(MarkerPoints);
        LineR.widthMultiplier = InPixels(MarkerPinPixels);
        LimLineColor.Apply(LineR, Paper, new Color(0.95f, 0.95f, 0.95f));
    }

    /// <summary>Draws again next frame whatever the cached state says.</summary>
    public void Redraw()
    {
        DrawnStart = float.NaN;
        DrawnColumns = 0;
    }

    public void Show()
    {
        Visible = true;
        VisibleSeconds = LimSystem.Preferences.WaveformVisibleSeconds;
        if (Strip != null) Strip.gameObject.SetActive(true);
        Redraw();
    }
    public void Hide()
    {
        Visible = false;
        if (LineL != null) LineL.positionCount = 0;
        if (LineR != null) LineR.positionCount = 0;
        if (Strip != null && LayoutReady) Strip.gameObject.SetActive(false);
    }
}
