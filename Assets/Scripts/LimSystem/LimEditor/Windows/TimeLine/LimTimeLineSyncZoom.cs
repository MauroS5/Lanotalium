using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Sync Both switch in the TimeLine's title bar, beside Waveform.
///
/// The waveform strip keeps a zoom of its own, which is what makes it useful
/// as a map of the whole song. Switched on, this ties the two together: the
/// strip shows exactly the stretch of song the TimeLine shows, starting at the
/// playhead, so a transient sits under the beatline it belongs to, and the
/// wheel over either of them zooms both. Switched off, each goes back to the
/// zoom it had, since the strip's own zoom is never written to while they are
/// tied.
///
/// The switch is a copy of the Waveform one next to it, placed one width to
/// the right, so it matches the bar it sits in without the scene being edited.
/// </summary>
public partial class LimTimeLineManager
{
    private Toggle SyncZoomToggle;
    private Text SyncZoomText;
    private bool SyncZoomBuilt;

    private void BuildSyncZoomToggle()
    {
        if (SyncZoomBuilt) return;
        if (WaveformToggle == null) return;
        RectTransform Source = WaveformToggle.transform.parent as RectTransform;
        if (Source == null || Source.parent == null) return;
        SyncZoomBuilt = true;

        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = "SyncZoom";
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchorMin = Source.anchorMin;
        Rect.anchorMax = Source.anchorMax;
        Rect.pivot = Source.pivot;
        Rect.sizeDelta = Source.sizeDelta;
        Rect.anchoredPosition = Source.anchoredPosition + new Vector2(Source.sizeDelta.x, 0);
        // The copy arrived explaining the switch it was copied from.
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);

        SyncZoomText = Clone.GetComponentInChildren<Text>(true);
        SyncZoomToggle = Clone.GetComponentInChildren<Toggle>(true);
        if (SyncZoomToggle != null)
        {
            // A fresh event: the copy arrived still switching the waveform on
            // and off.
            SyncZoomToggle.onValueChanged = new Toggle.ToggleEvent();
            SyncZoomToggle.isOn = LimSystem.Preferences.WaveformSyncZoom;
            SyncZoomToggle.onValueChanged.AddListener((bool On) => { OnSyncZoomToggle(); });
        }
        if (LimLanguageManager.TextDict != null) SetSyncZoomText();
    }

    public void SetSyncZoomText()
    {
        if (SyncZoomText == null) return;
        SyncZoomText.text = LimLanguageManager.TextDict["Window_TimeLine_SyncZoom"];
    }

    public void OnSyncZoomToggle()
    {
        if (SyncZoomToggle == null) return;
        LimSystem.Preferences.WaveformSyncZoom = SyncZoomToggle.isOn;
        if (WaveformManager != null) WaveformManager.Redraw();
    }
}
