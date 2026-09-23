using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Volume row of the Media Player, under Pitch.
///
/// It is a copy of the Pitch row, so it arrives with the same slider, the
/// same label and the same little field, and the rows below it are pushed
/// down to make space. The song is the only thing it touches: the hit sounds
/// and the pausing BGM have their own sources and keep their own level.
///
/// The bar goes to 125 per cent. Above 1 an AudioSource simply amplifies what
/// it plays, so a quiet export can be brought up without re-rendering it,
/// though anything already near the top will distort.
/// </summary>
public partial class LimMediaPlayerManager
{
    /// <summary>Where the row goes, and how far everything below it moves.</summary>
    private const float VolumeRowY = -110f;
    private const float VolumeRowStep = 30f;
    public const float MaxVolume = 1.25f;

    private Slider VolumeSlider;
    private InputField VolumeInputField;
    private Image VolumeImg;
    private Text VolumeLabel;
    private bool VolumeOnEdit, VolumeBuilt;

    private void BuildVolumeRow()
    {
        if (VolumeBuilt) return;
        if (PitchSlider == null) return;
        RectTransform Source = PitchSlider.GetComponent<RectTransform>();
        if (Source == null || Source.parent == null) return;
        VolumeBuilt = true;

        RectTransform Content = Source.parent as RectTransform;
        PushRowDown(Content, "PlayControl");
        PushRowDown(Content, "PreciseModeController");

        GameObject Clone = Instantiate(Source.gameObject, Content);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = "VolumeSlider";
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchorMin = Source.anchorMin;
        Rect.anchorMax = Source.anchorMax;
        Rect.pivot = Source.pivot;
        Rect.sizeDelta = Source.sizeDelta;
        Rect.anchoredPosition = new Vector2(Source.anchoredPosition.x, VolumeRowY);
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
        // The pitch row reports being pressed and being typed into through
        // triggers of its own; the copy would report the same thing while
        // this row is the one being used.
        foreach (EventTrigger Inherited in Clone.GetComponentsInChildren<EventTrigger>(true)) DestroyImmediate(Inherited);

        VolumeSlider = Clone.GetComponent<Slider>();
        VolumeInputField = Clone.GetComponentInChildren<InputField>(true);
        if (VolumeInputField != null) VolumeImg = VolumeInputField.GetComponent<Image>();
        foreach (Text Candidate in Clone.GetComponentsInChildren<Text>(true))
        {
            if (VolumeInputField != null && Candidate.transform.IsChildOf(VolumeInputField.transform)) continue;
            VolumeLabel = Candidate;
            break;
        }

        if (VolumeSlider != null)
        {
            // The copy came wired to the pitch of the song.
            VolumeSlider.onValueChanged = new Slider.SliderEvent();
            VolumeSlider.minValue = 0;
            VolumeSlider.maxValue = MaxVolume;
            VolumeSlider.wholeNumbers = false;
            VolumeSlider.value = Mathf.Clamp(LimSystem.Preferences.MusicVolume, 0, MaxVolume);
            VolumeSlider.onValueChanged.AddListener((float Value) => { OnVolumeSliderChange(); });
        }
        if (VolumeInputField != null)
        {
            VolumeInputField.onValueChanged = new InputField.OnChangeEvent();
            VolumeInputField.onEndEdit = new InputField.SubmitEvent();
            VolumeInputField.onValueChanged.AddListener((string Value) => { OnVolumeFieldChange(); });
            VolumeInputField.onEndEdit.AddListener((string Value) => { VolumeOnEdit = false; });
        }
        ApplyVolume(LimSystem.Preferences.MusicVolume, true);
        if (LimLanguageManager.TextDict != null) SetVolumeText();
    }

    /// <summary>Moves a row of the window down by the height of the new one.</summary>
    private static void PushRowDown(RectTransform Content, string Name)
    {
        if (Content == null) return;
        Transform Row = Content.Find(Name);
        if (Row == null) return;
        RectTransform Rect = Row as RectTransform;
        if (Rect == null) return;
        Rect.anchoredPosition = new Vector2(Rect.anchoredPosition.x, Rect.anchoredPosition.y - VolumeRowStep);
    }

    public void SetVolumeText()
    {
        if (VolumeLabel != null) VolumeLabel.text = LimLanguageManager.TextDict["Window_MediaPlayer_Volume"];
    }

    /// <summary>
    /// Written as a percentage because that is how loudness is spoken about:
    /// the field reads 100 when the song plays at the level it was made at.
    /// </summary>
    private void ApplyVolume(float Value, bool WriteField)
    {
        float Level = Mathf.Clamp(Value, 0, MaxVolume);
        LimSystem.Preferences.MusicVolume = Level;
        if (MusicPlayer != null) MusicPlayer.volume = Level;
        if (WriteField && VolumeInputField != null) VolumeInputField.text = Mathf.RoundToInt(Level * 100).ToString();
        if (VolumeImg != null) VolumeImg.color = ValidColor;
    }

    public void OnVolumeSliderChange()
    {
        if (VolumeSlider == null) return;
        ApplyVolume(VolumeSlider.value, !VolumeOnEdit);
    }
    public void OnVolumeFieldChange()
    {
        if (VolumeInputField == null) return;
        VolumeOnEdit = true;
        float Percent;
        if (!LimNumber.TryParseFloat(VolumeInputField.text, out Percent))
        {
            if (VolumeImg != null) VolumeImg.color = InvalidColor;
            return;
        }
        if (Percent < 0 || Percent > MaxVolume * 100)
        {
            if (VolumeImg != null) VolumeImg.color = InvalidColor;
            return;
        }
        ApplyVolume(Percent / 100f, false);
        if (VolumeSlider != null) VolumeSlider.value = Percent / 100f;
        VolumeOnEdit = false;
    }

    /// <summary>
    /// Puts the saved level back on the bar and on the source once a song is
    /// loaded. The row is built while the editor starts up, which can happen
    /// before the preferences file has been read, so what it shows until now
    /// may be the default rather than the level that was chosen.
    /// </summary>
    private void RestoreVolume()
    {
        float Level = Mathf.Clamp(LimSystem.Preferences.MusicVolume, 0, MaxVolume);
        if (VolumeSlider != null) VolumeSlider.value = Level;
        ApplyVolume(Level, true);
    }
}
