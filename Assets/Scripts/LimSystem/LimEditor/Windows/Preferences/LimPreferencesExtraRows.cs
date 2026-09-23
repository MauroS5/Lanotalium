using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The four settings added to Preferences since: what colour the waveform is
/// drawn in, how dark the editor looks, whether rails show the marks that
/// make them easier to edit, and how opaque the tuner's background is.
///
/// The last is a slider, which this window had none of: it is built by
/// LimUiBuilder where a list's dropdown would have been, wearing that
/// dropdown's sprite so it matches the rows above it.
///
/// Whether the waveform is drawn at all is not among them any more: the
/// TimeLine carries that switch in its own title bar, right above the strip,
/// and a second copy of it two windows away was only somewhere else to have
/// to look.
///
/// They are built from copies of the rows already in the window, so the scene
/// file keeps the settings it always had and the new rows cannot end up
/// looking like something bolted on: the Autosave row is the pattern for a
/// switch, the Effect Theme row for a list.
///
/// Built from SetTexts rather than from Start, because this window begins
/// inactive and its Start would not run until it was first opened, which is
/// long after the theme has decided how everything in the scene looks.
/// </summary>
public partial class LimPreferencesManager
{
    private const float ExtraRowStep = 35f;
    private const float FirstExtraRowY = -295f;
    private const float ExtraContentHeight = 540f;
    /// <summary>Room for the number to the right of the slider, and the gaps around it.</summary>
    private const float OpacityValueWidth = 36f;
    private const float OpacityValueGap = 6f;
    private const float OpacitySliderHeight = 20f;
    private const float OpacityLabelGap = 6f;

    private Toggle RailGuideToggle;
    private Dropdown WaveformColorDropdown, ThemeDropdown;
    private Text WaveformColorRowText, ThemeRowText, RailGuideRowText;
    private Slider TunerBackgroundSlider;
    private Text TunerBackgroundRowText, TunerBackgroundValueText;
    private bool ExtraRowsBuilt;

    private void BuildExtraRows()
    {
        if (ExtraRowsBuilt) return;
        if (AutosaveToggle == null || AudioEffectThemeDropdown == null) return;
        RectTransform SwitchRow = AutosaveToggle.transform.parent as RectTransform;
        RectTransform ListRow = AudioEffectThemeDropdown.transform.parent as RectTransform;
        if (SwitchRow == null || ListRow == null) return;
        RectTransform Content = SwitchRow.parent as RectTransform;
        if (Content == null) return;
        ExtraRowsBuilt = true;

        GameObject ColorRow = CloneRow(ListRow, "WaveformColor", FirstExtraRowY);
        WaveformColorDropdown = ColorRow.GetComponentInChildren<Dropdown>(true);
        WaveformColorRowText = FirstTextOutside(ColorRow, WaveformColorDropdown);
        if (WaveformColorDropdown != null)
        {
            WaveformColorDropdown.onValueChanged = new Dropdown.DropdownEvent();
            WaveformColorDropdown.onValueChanged.AddListener((int Value) => { OnWaveformColorDropdownChange(); });
        }

        GameObject ThemeRow = CloneRow(ListRow, "Theme", FirstExtraRowY - ExtraRowStep);
        ThemeDropdown = ThemeRow.GetComponentInChildren<Dropdown>(true);
        ThemeRowText = FirstTextOutside(ThemeRow, ThemeDropdown);
        if (ThemeDropdown != null)
        {
            ThemeDropdown.onValueChanged = new Dropdown.DropdownEvent();
            ThemeDropdown.onValueChanged.AddListener((int Value) => { OnThemeDropdownChange(); });
        }

        GameObject RailRow = CloneRow(SwitchRow, "RailNoteVisualGuide", FirstExtraRowY - ExtraRowStep * 2);
        RailGuideToggle = RailRow.GetComponentInChildren<Toggle>(true);
        RailGuideRowText = RailRow.GetComponentInChildren<Text>(true);
        if (RailGuideToggle != null)
        {
            RailGuideToggle.onValueChanged = new Toggle.ToggleEvent();
            RailGuideToggle.isOn = LimSystem.Preferences.RailNoteVisualGuide;
            RailGuideToggle.onValueChanged.AddListener((bool On) => { OnRailGuideToggleChange(); });
        }

        GameObject BackgroundRow = CloneRow(ListRow, "TunerBackgroundOpacity", FirstExtraRowY - ExtraRowStep * 3);
        BuildTunerBackgroundSlider(BackgroundRow);

        // The window can be made shorter than its contents, so the scrolling
        // area has to know the rows are there.
        Content.sizeDelta = new Vector2(Content.sizeDelta.x, ExtraContentHeight);
        if (LimLanguageManager.TextDict != null) SetExtraTexts();
    }

    /// <summary>
    /// Turns a copy of a list row into a slider row: the slider takes the
    /// dropdown's place and look, the number it stands at is written to its
    /// right, and the dropdown goes.
    /// </summary>
    private void BuildTunerBackgroundSlider(GameObject Row)
    {
        Dropdown List = Row.GetComponentInChildren<Dropdown>(true);
        TunerBackgroundRowText = FirstTextOutside(Row, List);
        if (List == null) return;
        RectTransform Place = List.GetComponent<RectTransform>();
        RectTransform RowRect = Row.GetComponent<RectTransform>();
        float Value = Mathf.Clamp(LimSystem.Preferences.TunerBackgroundAlpha, 0, 100);

        TunerBackgroundSlider = LimUiBuilder.CreateSlider(RowRect, "Slider", List.GetComponent<Image>(), 0, 100, Value);
        RectTransform SliderRect = TunerBackgroundSlider.GetComponent<RectTransform>();
        SliderRect.anchorMin = Place.anchorMin;
        SliderRect.anchorMax = Place.anchorMax;
        SliderRect.pivot = Place.pivot;
        SliderRect.anchoredPosition = Place.anchoredPosition;
        SliderRect.sizeDelta = new Vector2(Place.sizeDelta.x - OpacityValueWidth - OpacityValueGap, OpacitySliderHeight);
        TunerBackgroundSlider.wholeNumbers = true;
        TunerBackgroundSlider.value = Value;
        TunerBackgroundSlider.onValueChanged.AddListener((float Changed) => { OnTunerBackgroundSliderChange(); });

        Font Face = TunerBackgroundRowText != null ? TunerBackgroundRowText.font : null;
        int FontSize = TunerBackgroundRowText != null && TunerBackgroundRowText.fontSize > 0 ? TunerBackgroundRowText.fontSize : 14;
        Color Ink = TunerBackgroundRowText != null ? TunerBackgroundRowText.color : Color.white;
        TunerBackgroundValueText = LimUiBuilder.CreateLabel(RowRect, "Value", Face, FontSize, Ink, TextAnchor.MiddleRight);
        RectTransform ValueRect = TunerBackgroundValueText.rectTransform;
        ValueRect.anchorMin = Place.anchorMin;
        ValueRect.anchorMax = Place.anchorMax;
        ValueRect.pivot = Place.pivot;
        ValueRect.anchoredPosition = new Vector2(Place.anchoredPosition.x + Place.sizeDelta.x - OpacityValueWidth, Place.anchoredPosition.y);
        ValueRect.sizeDelta = new Vector2(OpacityValueWidth, Place.sizeDelta.y);
        ShowTunerBackgroundValue();

        // The label and the control overlap a little on the list rows, which
        // short words never showed. This one is long in Spanish, so it stops
        // short of the slider and shrinks to fit rather than run under it.
        if (TunerBackgroundRowText != null)
        {
            RectTransform LabelRect = TunerBackgroundRowText.rectTransform;
            LabelRect.sizeDelta = new Vector2(Place.anchoredPosition.x - OpacityLabelGap, LabelRect.sizeDelta.y);
            TunerBackgroundRowText.resizeTextForBestFit = true;
            TunerBackgroundRowText.resizeTextMinSize = 8;
            TunerBackgroundRowText.resizeTextMaxSize = FontSize;
        }
        DestroyImmediate(List.gameObject);
    }

    private void ShowTunerBackgroundValue()
    {
        if (TunerBackgroundValueText == null || TunerBackgroundSlider == null) return;
        TunerBackgroundValueText.text = Mathf.RoundToInt(TunerBackgroundSlider.value).ToString();
    }

    /// <summary>
    /// Nothing else has to be told: the tuner reads the setting each frame
    /// and redraws its background the moment the number changes.
    /// </summary>
    public void OnTunerBackgroundSliderChange()
    {
        if (TunerBackgroundSlider == null) return;
        LimSystem.Preferences.TunerBackgroundAlpha = TunerBackgroundSlider.value;
        ShowTunerBackgroundValue();
    }

    private GameObject CloneRow(RectTransform Source, string Name, float Y)
    {
        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = Name;
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchoredPosition = new Vector2(Source.anchoredPosition.x, Y);
        // A copy arrives still reporting to the setting it was copied from.
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
        Clone.SetActive(true);
        return Clone;
    }

    /// <summary>
    /// The label of a row, which is the one piece of text that does not
    /// belong to the control beside it.
    /// </summary>
    private static Text FirstTextOutside(GameObject Row, Component Control)
    {
        foreach (Text Candidate in Row.GetComponentsInChildren<Text>(true))
        {
            if (Control != null && Candidate.transform.IsChildOf(Control.transform)) continue;
            return Candidate;
        }
        return null;
    }

    /// <summary>
    /// Fills the two lists and names the rows. Called again on a change of
    /// language, like the rest of this window.
    /// </summary>
    public void SetExtraTexts()
    {
        if (WaveformColorRowText != null) WaveformColorRowText.text = LimLanguageManager.TextDict["Preferences_WaveformColor"];
        if (ThemeRowText != null) ThemeRowText.text = LimLanguageManager.TextDict["Preferences_Theme"];
        if (RailGuideRowText != null) RailGuideRowText.text = LimLanguageManager.TextDict["Preferences_RailNoteVisualGuide"];
        if (TunerBackgroundRowText != null) TunerBackgroundRowText.text = LimLanguageManager.TextDict["Preferences_TunerBackgroundOpacity"];

        if (WaveformColorDropdown != null)
        {
            List<Dropdown.OptionData> Colours = new List<Dropdown.OptionData>();
            for (int i = 0; i < LimWaveformPalette.Count; ++i)
            {
                Colours.Add(new Dropdown.OptionData(LimLanguageManager.TextDict[LimWaveformPalette.TextKey(i)]));
            }
            WaveformColorDropdown.options = Colours;
            WaveformColorDropdown.value = (int)LimSystem.Preferences.WaveformColor;
            WaveformColorDropdown.RefreshShownValue();
        }
        if (ThemeDropdown != null)
        {
            ThemeDropdown.options = new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData(LimLanguageManager.TextDict["Preferences_Theme_Default"]),
                new Dropdown.OptionData(LimLanguageManager.TextDict["Preferences_Theme_Dark"]),
                new Dropdown.OptionData(LimLanguageManager.TextDict["Preferences_Theme_Light"])
            };
            ThemeDropdown.value = (int)LimSystem.Preferences.Theme;
            ThemeDropdown.RefreshShownValue();
        }
    }

    /// <summary>Brings the four rows up to date when the window is opened.</summary>
    private void RestoreExtraRows()
    {
        BuildExtraRows();
        if (WaveformColorDropdown != null) WaveformColorDropdown.value = (int)LimSystem.Preferences.WaveformColor;
        if (ThemeDropdown != null) ThemeDropdown.value = (int)LimSystem.Preferences.Theme;
        if (RailGuideToggle != null) RailGuideToggle.isOn = LimSystem.Preferences.RailNoteVisualGuide;
        if (TunerBackgroundSlider != null) TunerBackgroundSlider.value = Mathf.Clamp(LimSystem.Preferences.TunerBackgroundAlpha, 0, 100);
        ShowTunerBackgroundValue();
    }

    /// <summary>
    /// The bead on the end of every rail and the cut line the S key follows.
    /// Nothing else has to be told: the tuner reads the setting each frame
    /// and simply stops drawing them.
    /// </summary>
    public void OnRailGuideToggleChange()
    {
        if (RailGuideToggle == null) return;
        LimSystem.Preferences.RailNoteVisualGuide = RailGuideToggle.isOn;
    }

    public void OnWaveformColorDropdownChange()
    {
        if (WaveformColorDropdown == null) return;
        LimSystem.Preferences.WaveformColor = (Lanotalium.Editor.WaveformColor)WaveformColorDropdown.value;
        LimTimeLineManager TimeLine = FindTimeLine();
        if (TimeLine != null && TimeLine.WaveformManager != null) TimeLine.WaveformManager.Redraw();
    }
    public void OnThemeDropdownChange()
    {
        if (ThemeDropdown == null) return;
        LimSystem.Preferences.Theme = (Lanotalium.Editor.UiTheme)ThemeDropdown.value;
        LimThemeManager.Refresh();
    }

    private LimTimeLineManager FindTimeLine()
    {
        if (TimeLineManager != null) return TimeLineManager;
        TimeLineManager = FindObjectOfType<LimTimeLineManager>();
        return TimeLineManager;
    }
    private LimTimeLineManager TimeLineManager;
}
