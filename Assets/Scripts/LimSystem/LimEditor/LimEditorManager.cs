using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class LimEditorManager : MonoBehaviour
{
    public static LimEditorManager Instance { get; set; }
    public LimMediaPlayerManager MusicPlayerWindow;
    public LimInspectorManager InspectorWindow;
    public LimTunerWindowManager TunerWindow;
    public LimTimeLineManager TimeLineWindow;
    public LimCreatorManager CreatorWindow;
    public LimSpectrumManager SpectrumWindow;
    public LimPreferencesManager PreferencesWindow;
    public LimGizmoMotionManager GizmoMotionWindow;
    public LimCloudManager CloudManager;
    public LimTopMenuManager TopMenu;
    public LimStatusManager StatusManager;
    public LimSubmitManager SubmitManager;
    
    public void ResetEditorLayout()
    {
        MusicPlayerWindow.BaseWindow.WindowRectTransform.anchoredPosition = new Vector2(1420, -890);
        InspectorWindow.BaseWindow.WindowRectTransform.anchoredPosition = new Vector2(1420, -60);
        TunerWindow.BaseWindow.WindowRectTransform.anchoredPosition = new Vector2(0, -60);
        TimeLineWindow.BaseWindow.WindowRectTransform.anchoredPosition = new Vector2(0, -860);
        CreatorWindow.BaseWindow.WindowRectTransform.anchoredPosition = new Vector2(1000, -60);
        SpectrumWindow.BaseWindow.WindowRectTransform.anchoredPosition = new Vector2(0, -655);
        StatusManager.BaseWindow.WindowRectTransform.anchoredPosition = new Vector2(1000, -655);

        MusicPlayerWindow.BaseWindow.WindowRectTransform.sizeDelta = new Vector2(500, 190);
        InspectorWindow.BaseWindow.WindowRectTransform.sizeDelta = new Vector2(500, 800);
        TunerWindow.BaseWindow.WindowRectTransform.sizeDelta = new Vector2(1000, 562.5f);
        TimeLineWindow.BaseWindow.WindowRectTransform.sizeDelta = new Vector2(1000, 220);
        CreatorWindow.BaseWindow.WindowRectTransform.sizeDelta = new Vector2(420, 562.5f);
        SpectrumWindow.BaseWindow.WindowRectTransform.sizeDelta = new Vector2(420, 175);
        StatusManager.BaseWindow.WindowRectTransform.sizeDelta = new Vector2(420, 425);
    }
    public void RestoreEditorLayout()
    {
        if (!LimSystem.EditorLayout.IsLayoutValid()) return;
        MusicPlayerWindow.BaseWindow.WindowRectTransform.anchoredPosition = LimSystem.EditorLayout.MusicPlayerPos.ToVector2();
        InspectorWindow.BaseWindow.WindowRectTransform.anchoredPosition = LimSystem.EditorLayout.InspectorPos.ToVector2();
        TunerWindow.BaseWindow.WindowRectTransform.anchoredPosition = LimSystem.EditorLayout.TunerWindowPos.ToVector2();
        TimeLineWindow.BaseWindow.WindowRectTransform.anchoredPosition = LimSystem.EditorLayout.TimelinePos.ToVector2();
        CreatorWindow.BaseWindow.WindowRectTransform.anchoredPosition = LimSystem.EditorLayout.CreatorPos.ToVector2();
        SpectrumWindow.BaseWindow.WindowRectTransform.anchoredPosition = LimSystem.EditorLayout.SpectrumPos.ToVector2();
        MusicPlayerWindow.BaseWindow.WindowRectTransform.sizeDelta = LimSystem.EditorLayout.MusicPlayerSize.ToVector2();
        InspectorWindow.BaseWindow.WindowRectTransform.sizeDelta = LimSystem.EditorLayout.InspectorSize.ToVector2();
        TunerWindow.BaseWindow.WindowRectTransform.sizeDelta = LimSystem.EditorLayout.TunerWindowSize.ToVector2();
        TimeLineWindow.BaseWindow.WindowRectTransform.sizeDelta = LimSystem.EditorLayout.TimelineSize.ToVector2();
        CreatorWindow.BaseWindow.WindowRectTransform.sizeDelta = LimSystem.EditorLayout.CreatorSize.ToVector2();
        SpectrumWindow.BaseWindow.WindowRectTransform.sizeDelta = LimSystem.EditorLayout.SpectrumSize.ToVector2();
    }
    public void SaveEditorLayout()
    {
        LimSystem.EditorLayout.MusicPlayerPos = new Lanotalium.Editor.Vector2Save(MusicPlayerWindow.BaseWindow.WindowRectTransform.anchoredPosition);
        LimSystem.EditorLayout.InspectorPos = new Lanotalium.Editor.Vector2Save(InspectorWindow.BaseWindow.WindowRectTransform.anchoredPosition);
        LimSystem.EditorLayout.TunerWindowPos = new Lanotalium.Editor.Vector2Save(TunerWindow.BaseWindow.WindowRectTransform.anchoredPosition);
        LimSystem.EditorLayout.TimelinePos = new Lanotalium.Editor.Vector2Save(TimeLineWindow.BaseWindow.WindowRectTransform.anchoredPosition);
        LimSystem.EditorLayout.CreatorPos = new Lanotalium.Editor.Vector2Save(CreatorWindow.BaseWindow.WindowRectTransform.anchoredPosition);
        LimSystem.EditorLayout.SpectrumPos = new Lanotalium.Editor.Vector2Save(SpectrumWindow.BaseWindow.WindowRectTransform.anchoredPosition);
        LimSystem.EditorLayout.MusicPlayerSize = new Lanotalium.Editor.Vector2Save(MusicPlayerWindow.BaseWindow.WindowRectTransform.sizeDelta);
        LimSystem.EditorLayout.InspectorSize = new Lanotalium.Editor.Vector2Save(InspectorWindow.BaseWindow.WindowRectTransform.sizeDelta);
        LimSystem.EditorLayout.TunerWindowSize = new Lanotalium.Editor.Vector2Save(TunerWindow.BaseWindow.WindowRectTransform.sizeDelta);
        LimSystem.EditorLayout.TimelineSize = new Lanotalium.Editor.Vector2Save(TimeLineWindow.BaseWindow.WindowRectTransform.sizeDelta);
        LimSystem.EditorLayout.CreatorSize = new Lanotalium.Editor.Vector2Save(CreatorWindow.BaseWindow.WindowRectTransform.sizeDelta);
        LimSystem.EditorLayout.SpectrumSize = new Lanotalium.Editor.Vector2Save(SpectrumWindow.BaseWindow.WindowRectTransform.sizeDelta);
    }

    /// <summary>
    /// Windows that stay closed on launch.
    ///
    /// Spectrum and Status are diagnostic panels rather than everyday
    /// tools, so they no longer take up the workspace as soon as the
    /// editor opens. Both are still reachable from the top menu, which
    /// toggles these same objects.
    ///
    /// The Event window (the news / what's new popup) is closed too: its
    /// contents are baked into the scene and were only ever refreshed from
    /// the servers that no longer exist, so it greeted every launch with
    /// the same text.
    ///
    /// Runs in Start, before the first frame is drawn, so nothing flashes.
    /// </summary>
    private void CloseWindowsOnStartup()
    {
        if (SpectrumWindow != null && SpectrumWindow.BaseWindow != null)
            SpectrumWindow.BaseWindow.gameObject.SetActive(false);
        if (StatusManager != null && StatusManager.BaseWindow != null)
            StatusManager.BaseWindow.gameObject.SetActive(false);

        // Addressed by path instead of a serialized field so that no scene
        // rewiring is needed; the Event window hangs off this same object.
        Transform EventWindow = transform.Find("CommonWindows/Event");
        if (EventWindow != null) EventWindow.gameObject.SetActive(false);
    }

    private void Start()
    {
        Instance = this;
        CloseWindowsOnStartup();
        if (LimSystem.Preferences.HideWhatsNew) return;
    }
    public void SetTexts()
    {

    }
}
