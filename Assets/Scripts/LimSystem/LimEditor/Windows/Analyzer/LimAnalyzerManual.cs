using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Manual analyzer: a window with a large pad to tap the beat on while
/// the song plays, a readout of the tempo the taps keep, and fields to write
/// that tempo into the chart from a chosen moment.
///
/// The window is a copy of the Preferences window with its contents taken
/// out, so it moves, sorts and closes like the others; its buttons and
/// fields are copies of the Media Player's, so they wear the same theme.
/// </summary>
public partial class LimAnalyzer
{
    private const float ManualWidth = 420, ManualHeight = 540, TitleHeight = 30;
    private const float TapGapReset = 2f;

    private GameObject ManualWindow;
    private LimWindowManager ManualFrame;
    private Image Pad;
    private Text PadTitle, PadHint, Readout, BpmLabel, TimingLabel, Status;
    private Button PauseButton, ApplyButton;
    private InputField BpmField, TimingField;

    private LimMediaPlayerManager Media;
    private readonly List<double> Taps = new List<double>();
    private System.Diagnostics.Stopwatch Clock;
    private double SessionStart;
    private float SessionPitch = 1;
    private float Flash;

    private static readonly Color PadColor = new Color(0.22f, 0.42f, 0.72f);
    private static readonly Color PadFlashColor = new Color(0.45f, 0.68f, 1f);
    private static readonly Color InvalidStatusColor = new Color(1f, 0.55f, 0.5f);
    private static readonly Color StatusColor = new Color(0.8f, 0.8f, 0.8f);

    private static T FindInScene<T>() where T : Component
    {
        foreach (T Candidate in Resources.FindObjectsOfTypeAll<T>())
            if (Candidate.gameObject.scene.IsValid()) return Candidate;
        return null;
    }

    public void OpenManual()
    {
        if (LimSystem.ChartContainer == null) return;
        if (!EnsureManualWindow()) return;
        SetManualTexts();
        ManualWindow.SetActive(true);
        // Brought to the front the way grabbing a title bar does it.
        LimWindowArranger Arranger = FindInScene<LimWindowArranger>();
        if (Arranger != null) Arranger.SortWindows(ManualFrame);
    }

    private void CloseManual()
    {
        if (Clock != null && Media != null && Media.IsPlaying) Media.IsPlaying = false;
        EndSession();
        if (ManualWindow != null) ManualWindow.SetActive(false);
    }

    // ---- Building ----------------------------------------------------------

    private bool EnsureManualWindow()
    {
        if (ManualWindow != null) return true;
        LimPreferencesManager Preferences = FindInScene<LimPreferencesManager>();
        Media = FindInScene<LimMediaPlayerManager>();
        if (Preferences == null || Media == null || Media.PauseBtn == null || Media.ProgressInputField == null) return false;

        GameObject Source = Preferences.gameObject;
        bool WasActive = Source.activeSelf;
        // Copied inactive, so nothing of the Preferences window runs in the copy.
        Source.SetActive(false);
        ManualWindow = Instantiate(Source, Source.transform.parent);
        Source.SetActive(WasActive);
        LimThemeManager.Adopt(Source, ManualWindow);
        ManualWindow.name = "AnalyzerManual";
        DestroyImmediate(ManualWindow.GetComponent<LimPreferencesManager>());
        StripClone(ManualWindow);

        ManualFrame = ManualWindow.GetComponent<LimWindowManager>();
        // The copy has to be one of the arranger's windows, or it keeps the
        // Preferences window's place in the order and others cover it.
        LimWindowArranger WindowArranger = FindInScene<LimWindowArranger>();
        if (WindowArranger != null && ManualFrame != null && !WindowArranger.Windows.Contains(ManualFrame))
        {
            WindowArranger.Windows.Add(ManualFrame);
            ManualFrame.OnWindowSorting.AddListener(WindowArranger.SortWindows);
        }
        RectTransform Root = ManualWindow.GetComponent<RectTransform>();
        Root.sizeDelta = new Vector2(ManualWidth, ManualHeight);

        // The preference rows go; the frame, the title bar and Close stay.
        ScrollRect View = ManualWindow.GetComponentInChildren<ScrollRect>(true);
        if (View != null)
        {
            if (View.content != null) DestroyImmediate(View.content.gameObject);
            View.enabled = false;
        }
        foreach (Button Candidate in ManualWindow.GetComponentsInChildren<Button>(true))
        {
            if (Candidate.name != "Close") continue;
            Candidate.onClick = new Button.ButtonClickedEvent();
            Candidate.onClick.AddListener(CloseManual);
        }

        // Everything new hangs from one rect under the title bar, drawn above
        // the window's background and below its title bar and Close.
        RectTransform Body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
        Body.SetParent(Root, false);
        Body.anchorMin = Vector2.zero;
        Body.anchorMax = Vector2.one;
        Body.offsetMin = Vector2.zero;
        Body.offsetMax = new Vector2(0, -TitleHeight);
        if (View != null) Body.SetSiblingIndex(View.transform.GetSiblingIndex() + 1);

        Font Face = Media.PauseText != null ? Media.PauseText.font : null;

        Pad = new GameObject("TapPad", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        Place(Pad.rectTransform, Body, 0, -15, 250, 250);
        Pad.sprite = CircleSprite();
        Pad.color = PadColor;
        Pad.gameObject.AddComponent<LimTapPad>().OnDown = OnPadDown;

        PadTitle = LimUiBuilder.CreateLabel(Pad.rectTransform, "Title", Face, 26, Color.white, TextAnchor.MiddleCenter);
        Center(PadTitle.rectTransform, 22, 220, 40);
        PadTitle.fontStyle = FontStyle.Bold;
        PadHint = LimUiBuilder.CreateLabel(Pad.rectTransform, "Hint", Face, 15, new Color(1, 1, 1, 0.85f), TextAnchor.UpperCenter);
        Center(PadHint.rectTransform, -28, 190, 60);
        PadHint.horizontalOverflow = HorizontalWrapMode.Wrap;

        Readout = LimUiBuilder.CreateLabel(Body, "Readout", Face, 34, Color.white, TextAnchor.MiddleCenter);
        Place(Readout.rectTransform, Body, 0, -272, 380, 46);

        PauseButton = CloneButton(Body, 0, -326, 170, 32);
        PauseButton.onClick.AddListener(() => { if (Media.IsPlaying) Media.IsPlaying = false; EndSession(); });

        BpmLabel = LimUiBuilder.CreateLabel(Body, "BpmLabel", Face, 16, StatusColor, TextAnchor.MiddleLeft);
        PlaceLeft(BpmLabel.rectTransform, 20, -378, 50, 30);
        BpmField = CloneField(Body, 72, -378, 110, 30);
        TimingLabel = LimUiBuilder.CreateLabel(Body, "TimingLabel", Face, 16, StatusColor, TextAnchor.MiddleLeft);
        PlaceLeft(TimingLabel.rectTransform, 200, -378, 70, 30);
        TimingField = CloneField(Body, 272, -378, 128, 30);

        ApplyButton = CloneButton(Body, 0, -426, 210, 34);
        ApplyButton.onClick.AddListener(ApplyManual);

        Status = LimUiBuilder.CreateLabel(Body, "Status", Face, 14, StatusColor, TextAnchor.UpperCenter);
        Place(Status.rectTransform, Body, 0, -470, 390, 36);
        Status.horizontalOverflow = HorizontalWrapMode.Wrap;

        ManualWindow.SetActive(false);
        return true;
    }

    /// <summary>
    /// Hover hints and event triggers belong to whatever the copy was made
    /// from, and would keep reporting to it.
    /// </summary>
    private static void StripClone(GameObject Clone)
    {
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
        foreach (EventTrigger Trigger in Clone.GetComponentsInChildren<EventTrigger>(true))
        {
            // The title bar is dragged through its trigger, which the copy
            // needs: its calls already point at the copy's own frame.
            if (Trigger.name == "WindowHandle") continue;
            DestroyImmediate(Trigger);
        }
    }

    private Button CloneButton(RectTransform Parent, float X, float Y, float Width, float Height)
    {
        GameObject Copy = Instantiate(Media.PauseBtn.gameObject, Parent, false);
        LimThemeManager.Adopt(Media.PauseBtn.gameObject, Copy);
        StripClone(Copy);
        Copy.SetActive(true);
        Button Result = Copy.GetComponent<Button>();
        Result.onClick = new Button.ButtonClickedEvent();
        Place(Copy.GetComponent<RectTransform>(), Parent, X, Y, Width, Height);
        return Result;
    }

    private InputField CloneField(RectTransform Parent, float X, float Y, float Width, float Height)
    {
        GameObject Copy = Instantiate(Media.ProgressInputField.gameObject, Parent, false);
        LimThemeManager.Adopt(Media.ProgressInputField.gameObject, Copy);
        StripClone(Copy);
        Copy.SetActive(true);
        InputField Result = Copy.GetComponent<InputField>();
        Result.onValueChanged = new InputField.OnChangeEvent();
        Result.onEndEdit = new InputField.SubmitEvent();
        Result.contentType = InputField.ContentType.Standard;
        Result.text = "";
        PlaceLeft(Copy.GetComponent<RectTransform>(), X, Y, Width, Height);
        return Result;
    }

    /// <summary>Centred horizontally, Y down from the top.</summary>
    private static void Place(RectTransform Rect, RectTransform Parent, float X, float Y, float Width, float Height)
    {
        Rect.SetParent(Parent, false);
        Rect.anchorMin = new Vector2(0.5f, 1);
        Rect.anchorMax = new Vector2(0.5f, 1);
        Rect.pivot = new Vector2(0.5f, 1);
        Rect.anchoredPosition = new Vector2(X, Y);
        Rect.sizeDelta = new Vector2(Width, Height);
    }

    private static void PlaceLeft(RectTransform Rect, float X, float Y, float Width, float Height)
    {
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(0, 1);
        Rect.pivot = new Vector2(0, 1);
        Rect.anchoredPosition = new Vector2(X, Y);
        Rect.sizeDelta = new Vector2(Width, Height);
    }

    /// <summary>Centred in the parent, Y up from its middle.</summary>
    private static void Center(RectTransform Rect, float Y, float Width, float Height)
    {
        Rect.anchorMin = new Vector2(0.5f, 0.5f);
        Rect.anchorMax = new Vector2(0.5f, 0.5f);
        Rect.pivot = new Vector2(0.5f, 0.5f);
        Rect.anchoredPosition = new Vector2(0, Y);
        Rect.sizeDelta = new Vector2(Width, Height);
    }

    /// <summary>
    /// A white disc with a soft edge, drawn once: the project has no round
    /// sprite, and uGUI's own are editor-only resources.
    /// </summary>
    private static Sprite CircleSprite()
    {
        const int Size = 256;
        Texture2D Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] Pixels = new Color32[Size * Size];
        float Radius = Size / 2f - 1;
        for (int y = 0; y < Size; ++y)
        {
            for (int x = 0; x < Size; ++x)
            {
                float Dx = x + 0.5f - Size / 2f, Dy = y + 0.5f - Size / 2f;
                float Alpha = Mathf.Clamp01(Radius - Mathf.Sqrt(Dx * Dx + Dy * Dy) + 0.5f);
                Pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(Alpha * 255));
            }
        }
        Texture.SetPixels32(Pixels);
        Texture.Apply();
        return Sprite.Create(Texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
    }

    public void SetManualTexts()
    {
        if (ManualWindow == null) return;
        ManualFrame.WindowName = LimLanguageManager.TextDict["Analyzer_Manual_Title"];
        PadTitle.text = LimLanguageManager.TextDict["Analyzer_Manual_Tap"];
        SetLabel(PauseButton, LimLanguageManager.TextDict["Analyzer_Manual_Pause"]);
        SetLabel(ApplyButton, LimLanguageManager.TextDict["Analyzer_Manual_Apply"]);
        BpmLabel.text = LimLanguageManager.TextDict["Analyzer_Manual_Bpm"];
        TimingLabel.text = LimLanguageManager.TextDict["Analyzer_Manual_Timing"];
        RefreshReadout();
    }

    // ---- Tapping -----------------------------------------------------------

    /// <summary>
    /// The first press only starts the song, from wherever it was paused;
    /// every press after it is a beat.
    /// </summary>
    private void OnPadDown()
    {
        if (Media == null || LimSystem.ChartContainer == null) return;
        if (!Media.IsPlaying)
        {
            Media.IsPlaying = true;
            StartSession();
            Taps.Clear();
            RefreshReadout();
            return;
        }
        if (Clock == null) StartSession();

        double Now = SongNow();
        // A rest in the tapping starts a new count, rather than being read
        // as one very long beat.
        if (Taps.Count > 0 && Now - Taps[Taps.Count - 1] > TapGapReset * SessionPitch) Taps.Clear();
        Taps.Add(Now);
        Flash = 1;
        FillFieldsFromTaps();
        RefreshReadout();
    }

    /// <summary>
    /// Taps are timed on a stopwatch from the moment the song was started:
    /// the song's own position only moves in steps of an audio buffer, which
    /// is coarser than the difference a steady hand makes.
    /// </summary>
    private void StartSession()
    {
        SessionStart = Media.Time;
        SessionPitch = Mathf.Max(0.01f, Media.Pitch);
        Clock = System.Diagnostics.Stopwatch.StartNew();
    }

    private void EndSession()
    {
        Clock = null;
        RefreshReadout();
    }

    private double SongNow()
    {
        return SessionStart + Clock.Elapsed.TotalSeconds * SessionPitch;
    }

    /// <summary>
    /// The tempo and first beat of the line that best fits every tap, which
    /// shrugs off a single tap early or late where an average of the gaps
    /// would not: the gaps either side of a stray tap cancel out in the sum,
    /// but not in its effect on where the first beat is read.
    /// </summary>
    private bool FitTaps(out double Bpm, out double First)
    {
        Bpm = 0; First = 0;
        int N = Taps.Count;
        if (N < 2) return false;
        double MeanI = (N - 1) / 2.0, MeanT = 0;
        foreach (double T in Taps) MeanT += T;
        MeanT /= N;
        double Num = 0, Den = 0;
        for (int i = 0; i < N; ++i)
        {
            Num += (i - MeanI) * (Taps[i] - MeanT);
            Den += (i - MeanI) * (i - MeanI);
        }
        double Period = Num / Den;
        if (Period <= 0) return false;
        Bpm = 60.0 / Period;
        First = MeanT - Period * MeanI;
        return true;
    }

    private void FillFieldsFromTaps()
    {
        double Bpm, First;
        if (!FitTaps(out Bpm, out First)) return;
        if (!BpmField.isFocused) BpmField.text = Bpm.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        if (!TimingField.isFocused) TimingField.text = Mathf.Max(0, (float)First).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void RefreshReadout()
    {
        if (Readout == null) return;
        double Bpm, First;
        Readout.text = FitTaps(out Bpm, out First) ? BpmText(Math.Round(Bpm, 2)) + " BPM" : "— BPM";
        PadHint.text = Clock == null && Taps.Count == 0
            ? LimLanguageManager.TextDict["Analyzer_Manual_FirstPress"]
            : string.Format(LimLanguageManager.TextDict["Analyzer_Manual_Taps"], Taps.Count);
    }

    private void Update()
    {
        if (ManualWindow == null || !ManualWindow.activeInHierarchy) return;
        if (Clock != null)
        {
            if (Media == null || !Media.IsPlaying) EndSession();
            // A jump on the timeline while tapping moves the song under the
            // stopwatch: start timing again from where it is now.
            else if (Mathf.Abs((float)(SongNow() - Media.Time)) > 0.25f) { StartSession(); Taps.Clear(); RefreshReadout(); }
        }
        if (Flash > 0) Flash = Mathf.Max(0, Flash - UnityEngine.Time.unscaledDeltaTime * 6);
        Pad.color = Color.Lerp(PadColor, PadFlashColor, Flash);
    }

    private void ApplyManual()
    {
        float Bpm, Timing;
        bool Valid = LimNumber.TryParseFloat(BpmField.text, out Bpm) & LimNumber.TryParseFloat(TimingField.text, out Timing);
        if (!Valid || Bpm <= 0 || Bpm > 10000 || Timing < 0 || (Media != null && Timing > Media.Length))
        {
            Status.color = InvalidStatusColor;
            Status.text = LimLanguageManager.TextDict["Analyzer_Manual_Invalid"];
            return;
        }
        LimOperationManager Operations = LimOperationManager.Instance != null ? LimOperationManager.Instance : FindObjectOfType<LimOperationManager>();
        if (Operations == null) return;
        Operations.ReplaceBpmList(Operations.BpmListWith(Timing, Bpm));
        Status.color = StatusColor;
        Status.text = string.Format(LimLanguageManager.TextDict["Analyzer_Manual_Applied"], BpmText(Bpm), Format(Timing, "0.000"));
    }
}
