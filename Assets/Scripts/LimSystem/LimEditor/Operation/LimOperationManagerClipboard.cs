using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ctrl+C / Ctrl+V for notes.
///
/// Ctrl+C stores a deep copy of the current selection, keeping each note's
/// offset from the earliest one. Ctrl+V does not create anything yet: it
/// raises a ghost preview that follows the pointer, exactly like the
/// click-to-create cursor, and obeys the same Attach-to beatline and
/// angleline toggles. Left click drops the notes for real, right click
/// throws the pending paste away.
/// </summary>
public partial class LimOperationManager
{
    private class ClipboardItem
    {
        public Lanotalium.Chart.LanotaTapNote Tap;
        public Lanotalium.Chart.LanotaHoldNote Hold;
        public float OffsetTime;
        public float OffsetDegree;
        public GameObject Ghost;
    }

    private readonly List<ClipboardItem> _Clipboard = new List<ClipboardItem>();
    private bool _PasteActive;
    private int _PasteCommitFrame = -1;

    /// <summary>True while a paste preview is on screen awaiting a click.</summary>
    public bool IsPasting { get { return _PasteActive; } }

    /// <summary>
    /// True while any note gesture owns the pointer: dragging notes or
    /// placing a pending paste. The box-selection rectangle stays out of
    /// the way while this holds.
    /// </summary>
    public bool IsNoteGestureInProgress { get { return IsNoteDragInProgress || _PasteActive; } }

    public void DetectClipboard()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;

        if (!IsTypingInTextField())
        {
            bool Ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Ctrl && Input.GetKeyDown(KeyCode.C)) CopySelectionToClipboard();
            if (Ctrl && Input.GetKeyDown(KeyCode.V)) BeginPaste();
        }

        if (!_PasteActive) return;

        // Right click abandons a paste that has not been dropped yet.
        if (Input.GetMouseButtonDown(1)) { CancelPaste(); return; }

        UpdatePastePreview();

        if (Input.GetMouseButtonDown(0) && LimMousePosition.IsMouseOverWindow(TunerWindowRect)) CommitPaste();
    }

    private static bool IsTypingInTextField()
    {
        EventSystem Events = EventSystem.current;
        if (Events == null || Events.currentSelectedGameObject == null) return false;
        return Events.currentSelectedGameObject.GetComponent<InputField>() != null;
    }

    public void CopySelectionToClipboard()
    {
        if (SelectedTapNote.Count == 0 && SelectedHoldNote.Count == 0) return;
        CancelPaste();
        _Clipboard.Clear();

        // The earliest note is the anchor; everything else keeps its offset.
        float AnchorTime = float.MaxValue;
        float AnchorDegree = 0;
        foreach (Lanotalium.Chart.LanotaTapNote Tap in SelectedTapNote)
            if (Tap.Time < AnchorTime) { AnchorTime = Tap.Time; AnchorDegree = Tap.Degree; }
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in SelectedHoldNote)
            if (Hold.Time < AnchorTime) { AnchorTime = Hold.Time; AnchorDegree = Hold.Degree; }

        foreach (Lanotalium.Chart.LanotaTapNote Tap in SelectedTapNote)
            _Clipboard.Add(new ClipboardItem { Tap = Tap.DeepCopy(), OffsetTime = Tap.Time - AnchorTime, OffsetDegree = Tap.Degree - AnchorDegree });
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in SelectedHoldNote)
            _Clipboard.Add(new ClipboardItem { Hold = Hold.DeepCopy(), OffsetTime = Hold.Time - AnchorTime, OffsetDegree = Hold.Degree - AnchorDegree });

        Debug.Log("[Clipboard] Copied " + _Clipboard.Count + " note(s).");
    }

    public void BeginPaste()
    {
        if (_Clipboard.Count == 0) return;
        CancelPaste();
        // Snapping for a paste follows the toggles only; it must not
        // inherit the Shift-to-move-freely state of the last drag.
        _DragFreeMove = false;

        Transform Parent = LimClickToCreateManager.SharedGhostParent;
        foreach (ClipboardItem Item in _Clipboard)
        {
            GameObject Prefab = Item.Tap != null
                ? TunerManager.TapNoteManager.GetPrefab(Item.Tap.Type, Item.Tap.Size, false)
                : TunerManager.HoldNoteManager.GetPrefab(Item.Hold.Size, false);
            if (Prefab == null) continue;
            Item.Ghost = Parent != null ? Instantiate(Prefab, Parent) : Instantiate(Prefab);
            SpriteRenderer Renderer = Item.Ghost.GetComponentInChildren<SpriteRenderer>();
            if (Renderer != null) Renderer.sortingLayerName = "ClickToCreate";
            Item.Ghost.SetActive(false);
        }
        _PasteActive = true;
    }

    public void CancelPaste()
    {
        foreach (ClipboardItem Item in _Clipboard)
        {
            if (Item.Ghost != null) Destroy(Item.Ghost);
            Item.Ghost = null;
        }
        _PasteActive = false;
    }

    /// <summary>
    /// Works out where the pasted notes would land right now. Returns false
    /// when the pointer is not over a usable part of the ring.
    /// </summary>
    private bool TryGetPasteAnchor(out float AnchorTime, out float AnchorRelativeDegree)
    {
        AnchorTime = 0; AnchorRelativeDegree = 0;
        float Time, Degree;
        if (!LimTunerCoordinate.TryGetChartPointAtMouse(TunerWindowRect, TunerCamera, TunerManager, out Time, out Degree)) return false;
        AnchorTime = ApplyBeatlineSnap(Time);
        float AbsDegree = ApplyAnglelineSnap(Degree);
        AnchorRelativeDegree = AbsDegree - TunerManager.CameraManager.CalculateCameraRotation(AnchorTime);
        return true;
    }

    private void UpdatePastePreview()
    {
        float AnchorTime, AnchorRelativeDegree;
        bool Valid = TryGetPasteAnchor(out AnchorTime, out AnchorRelativeDegree);

        foreach (ClipboardItem Item in _Clipboard)
        {
            if (Item.Ghost == null) continue;
            if (!Valid) { if (Item.Ghost.activeInHierarchy) Item.Ghost.SetActive(false); continue; }

            float Time = AnchorTime + Item.OffsetTime;
            float RelativeDegree = AnchorRelativeDegree + Item.OffsetDegree;
            float AbsDegree = RelativeDegree + TunerManager.CameraManager.CalculateCameraRotation(Time);

            bool OnScreen = LimTunerCoordinate.PlacePreview(Item.Ghost.transform, Time, AbsDegree, TunerManager);
            if (Item.Ghost.activeInHierarchy != OnScreen) Item.Ghost.SetActive(OnScreen);
        }
    }

    private void CommitPaste()
    {
        float AnchorTime, AnchorRelativeDegree;
        if (!TryGetPasteAnchor(out AnchorTime, out AnchorRelativeDegree)) return;

        List<Lanotalium.Chart.LanotaTapNote> NewTaps = new List<Lanotalium.Chart.LanotaTapNote>();
        List<Lanotalium.Chart.LanotaHoldNote> NewHolds = new List<Lanotalium.Chart.LanotaHoldNote>();

        foreach (ClipboardItem Item in _Clipboard)
        {
            if (Item.Tap != null)
            {
                Lanotalium.Chart.LanotaTapNote New = Item.Tap.DeepCopy();
                New.Time = AnchorTime + Item.OffsetTime;
                New.Degree = AnchorRelativeDegree + Item.OffsetDegree;
                NewTaps.Add(New);
            }
            else if (Item.Hold != null)
            {
                Lanotalium.Chart.LanotaHoldNote New = Item.Hold.DeepCopy();
                New.Time = AnchorTime + Item.OffsetTime;
                New.Degree = AnchorRelativeDegree + Item.OffsetDegree;
                NewHolds.Add(New);
            }
        }
        if (NewTaps.Count == 0 && NewHolds.Count == 0) { CancelPaste(); return; }

        foreach (Lanotalium.Chart.LanotaTapNote New in NewTaps) AddTapNote(New, false, false, false);
        foreach (Lanotalium.Chart.LanotaHoldNote New in NewHolds) AddHoldNote(New, false, false, false);

        // A paste is one undo step, however many notes it dropped.
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            foreach (Lanotalium.Chart.LanotaTapNote New in NewTaps) AddTapNote(New, false, false, false);
            foreach (Lanotalium.Chart.LanotaHoldNote New in NewHolds) AddHoldNote(New, false, false, false);
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            foreach (Lanotalium.Chart.LanotaTapNote New in NewTaps) DeleteTapNote(New, false);
            foreach (Lanotalium.Chart.LanotaHoldNote New in NewHolds) DeleteHoldNote(New, false);
            SelectNothing();
        });
        AddToOperationSaver(OpSave);

        _PasteCommitFrame = UnityEngine.Time.frameCount;
        CancelPaste();
        // The click that dropped the paste must not also act as a selection.
        _DragConsumedClick = true;
    }
}
