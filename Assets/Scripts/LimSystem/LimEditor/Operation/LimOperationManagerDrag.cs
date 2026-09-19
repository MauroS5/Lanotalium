using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drag-to-move for notes in the tuner viewport.
///
/// Press the left button on a note and drag: the note follows the pointer,
/// its timing and degree update live, and the Basic inspector panel on the
/// right refreshes while you move. Releasing the button commits a single
/// undo entry for the whole gesture.
///
/// If the grabbed note is part of a multi-selection (for example one made
/// with the box-selection rectangle) the whole selection moves rigidly,
/// keeping every relative offset.
///
/// Snapping follows the two "Attach to" toggles of the click-to-create
/// panel: with Beatline on the dragged note lands exactly on a beatline,
/// with Angleline on it lands exactly on an angleline, hopping to the
/// nearest one as the pointer moves. Holding Shift as the drag starts
/// moves freely regardless of both toggles.
///
/// Uses the same mouse -> chart conversion as click-to-create
/// (LimTunerCoordinate), so a dragged note lands exactly where a newly
/// created one would.
/// </summary>
public partial class LimOperationManager
{
    private const float DragThresholdPixels = 4f;

    private class DragItem
    {
        public Lanotalium.Chart.LanotaTapNote Tap;
        public Lanotalium.Chart.LanotaHoldNote Hold;
        public float OriginTime;
        public float OriginDegree;

        public float Time { get { return Tap != null ? Tap.Time : Hold.Time; } }
        public float Degree { get { return Tap != null ? Tap.Degree : Hold.Degree; } }
    }

    private readonly List<DragItem> _DragItems = new List<DragItem>();
    private DragItem _DragAnchor;

    private bool _DragPending;
    private bool _DragActive;
    private bool _DragConsumedClick;
    private bool _DragFreeMove;

    private Vector3 _DragMouseDownPosition;
    private float _DragGrabTime;
    private float _DragGrabDegree;
    private float _DragAnchorOriginAbsDegree;

    /// <summary>True while notes are actually following the pointer.</summary>
    public bool IsDraggingNote { get { return _DragActive; } }

    /// <summary>
    /// True from the moment the button goes down on a note until it is
    /// released. The box-selection rectangle checks this so that grabbing
    /// a note never starts a rectangle and never clears the selection.
    /// </summary>
    public bool IsNoteDragInProgress { get { return _DragPending || _DragActive; } }

    public void DetectNoteDrag()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;
        // A pending paste owns the pointer, and the click that drops it
        // must not immediately grab the note it just created.
        if (_PasteActive || _PasteCommitFrame == UnityEngine.Time.frameCount) { CancelDragTracking(); return; }
        // Creating notes owns the left button while it is switched on.
        if (LimClickToCreateManager.IsCreating) { CancelDragTracking(); return; }

        if (Input.GetMouseButtonDown(0)) BeginDragCandidate();
        else if (Input.GetMouseButton(0)) UpdateDragCandidate();
        else if (Input.GetMouseButtonUp(0)) FinishDrag();
    }

    private void CancelDragTracking()
    {
        _DragPending = false;
        _DragActive = false;
        _DragItems.Clear();
        _DragAnchor = null;
    }

    private void BeginDragCandidate()
    {
        CancelDragTracking();
        if (!LimMousePosition.IsMouseOverWindow(TunerWindowRect)) return;

        Vector3 TunerPosition = LimTunerCoordinate.MouseToTunerScreen(TunerWindowRect);
        Ray ray = TunerCamera.ScreenPointToRay(TunerPosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit)) return;

        int InstanceId = hit.collider.gameObject.GetInstanceID();
        int TapIndex = FindTapNoteIndexByInstanceID(InstanceId);
        if (TapIndex != -1)
        {
            _DragAnchor = new DragItem { Tap = TunerManager.TapNoteManager.TapNote[TapIndex] };
        }
        else
        {
            int HoldIndex = FindHoldNoteIndexByInstanceID(InstanceId);
            if (HoldIndex == -1) return;
            _DragAnchor = new DragItem { Hold = TunerManager.HoldNoteManager.HoldNote[HoldIndex] };
        }

        _DragPending = true;
        _DragMouseDownPosition = LimMousePosition.MousePosition;
    }

    /// <summary>
    /// Promotes the pending candidate into a live drag: works out which
    /// notes travel together and records where everything started.
    /// </summary>
    private bool BeginActualDrag()
    {
        // Shift pressed as the drag begins disables snapping for the gesture.
        _DragFreeMove = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        bool AnchorAlreadySelected = _DragAnchor.Tap != null ? IsTapNoteSelected(_DragAnchor.Tap) : IsHoldNoteSelected(_DragAnchor.Hold);
        if (!AnchorAlreadySelected)
        {
            // Grabbing an unselected note selects it alone, like clicking it.
            if (_DragAnchor.Tap != null) SelectTapNote(_DragAnchor.Tap);
            else SelectHoldNote(_DragAnchor.Hold);
        }

        _DragItems.Clear();
        bool AnchorInSelection = false;
        foreach (Lanotalium.Chart.LanotaTapNote Tap in SelectedTapNote)
        {
            DragItem Item = Tap == _DragAnchor.Tap ? _DragAnchor : new DragItem { Tap = Tap };
            if (Item == _DragAnchor) AnchorInSelection = true;
            Item.OriginTime = Tap.Time; Item.OriginDegree = Tap.Degree;
            _DragItems.Add(Item);
        }
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in SelectedHoldNote)
        {
            DragItem Item = Hold == _DragAnchor.Hold ? _DragAnchor : new DragItem { Hold = Hold };
            if (Item == _DragAnchor) AnchorInSelection = true;
            Item.OriginTime = Hold.Time; Item.OriginDegree = Hold.Degree;
            _DragItems.Add(Item);
        }
        if (!AnchorInSelection || _DragItems.Count == 0) return false;

        // Where on the chart the pointer grabbed, so the note keeps its
        // offset from the cursor instead of jumping under it.
        float GrabTime, GrabDegree;
        if (!LimTunerCoordinate.TryGetChartPointAtMouse(TunerWindowRect, TunerCamera, TunerManager, out GrabTime, out GrabDegree)) return false;
        _DragGrabTime = GrabTime;
        _DragGrabDegree = GrabDegree;
        _DragAnchorOriginAbsDegree = _DragAnchor.OriginDegree + TunerManager.CameraManager.CalculateCameraRotation(_DragAnchor.OriginTime);

        _DragActive = true;
        return true;
    }

    private void UpdateDragCandidate()
    {
        if (!_DragPending) return;

        if (!_DragActive)
        {
            // Wait for real movement so a plain click still selects.
            if (Vector3.Distance(LimMousePosition.MousePosition, _DragMouseDownPosition) < DragThresholdPixels) return;
            if (!BeginActualDrag()) { CancelDragTracking(); return; }
        }

        float PointerTime, PointerDegree;
        if (!LimTunerCoordinate.TryGetChartPointAtMouse(TunerWindowRect, TunerCamera, TunerManager, out PointerTime, out PointerDegree)) return;

        // Move the anchor by how far the pointer travelled, then snap the
        // anchor itself. Snapping the note rather than the cursor is what
        // makes it land exactly on a beatline or angleline.
        float AnchorTime = _DragAnchor.OriginTime + (PointerTime - _DragGrabTime);
        float AnchorAbsDegree = _DragAnchorOriginAbsDegree + Mathf.DeltaAngle(_DragGrabDegree, PointerDegree);
        AnchorTime = ApplyBeatlineSnap(AnchorTime);
        AnchorAbsDegree = ApplyAnglelineSnap(AnchorAbsDegree);

        ApplyToNote(_DragAnchor, AnchorTime, AnchorAbsDegree, true);

        // Everything else follows rigidly, in the chart's own coordinates.
        float DeltaTime = _DragAnchor.Time - _DragAnchor.OriginTime;
        float DeltaDegree = _DragAnchor.Degree - _DragAnchor.OriginDegree;
        for (int i = 0; i < _DragItems.Count; ++i)
        {
            DragItem Item = _DragItems[i];
            if (Item == _DragAnchor) continue;
            ApplyToNote(Item, Item.OriginTime + DeltaTime, Item.OriginDegree + DeltaDegree, false);
        }

        if (InspectorManager != null) InspectorManager.OnSelectChange();
    }

    private float ApplyBeatlineSnap(float Time)
    {
        if (_DragFreeMove) return Time;
        if (!LimClickToCreateManager.SnapToBeatline) return Time;
        if (InspectorManager == null || InspectorManager.ComponentBpm == null) return Time;
        if (InspectorManager.ComponentBpm.BeatlineTimes == null) return Time;
        if (InspectorManager.ComponentBpm.BeatlineTimes.Count == 0) return Time;
        return FindNearestBeatlineByTime(Time);
    }

    private float ApplyAnglelineSnap(float Degree)
    {
        if (_DragFreeMove) return Degree;
        if (!LimClickToCreateManager.SnapToAngleline) return Degree;
        LimAngleLineManager Angleline = LimClickToCreateManager.SharedAnglelineManager;
        if (Angleline == null || !Angleline.Enable || Angleline.AnglelineCount == 0) return Degree;
        return Angleline.FindNearestAnglelineByDegree(Degree);
    }

    /// <summary>
    /// Writes one note's position without touching the undo stack. The
    /// timing goes first: converting an absolute on-screen degree removes
    /// the camera rotation sampled at the note's own timing.
    /// </summary>
    private void ApplyToNote(DragItem Item, float Time, float Degree, bool DegreeIsAbsolute)
    {
        if (Item.Tap != null)
        {
            SetTapNoteTime(Item.Tap, Time, false);
            SetTapNoteDegree(Item.Tap, Degree, DegreeIsAbsolute, false);
        }
        else if (Item.Hold != null)
        {
            SetHoldNoteTime(Item.Hold, Time, false);
            SetHoldNoteDegree(Item.Hold, Degree, DegreeIsAbsolute, false);
        }
    }

    private void FinishDrag()
    {
        if (!_DragActive) { CancelDragTracking(); return; }

        // One undo entry for the whole gesture instead of one per frame.
        List<DragItem> Items = new List<DragItem>(_DragItems);
        List<float> FinalTimes = new List<float>();
        List<float> FinalDegrees = new List<float>();
        bool Moved = false;
        foreach (DragItem Item in Items)
        {
            FinalTimes.Add(Item.Time);
            FinalDegrees.Add(Item.Degree);
            if (Item.Time != Item.OriginTime || Item.Degree != Item.OriginDegree) Moved = true;
        }

        if (Moved)
        {
            LimInspectorManager Inspector = InspectorManager;
            Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
            OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
            {
                for (int i = 0; i < Items.Count; ++i) WriteRaw(Items[i], FinalTimes[i], FinalDegrees[i]);
                if (Inspector != null) Inspector.OnSelectChange();
            });
            OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
            {
                for (int i = 0; i < Items.Count; ++i) WriteRaw(Items[i], Items[i].OriginTime, Items[i].OriginDegree);
                if (Inspector != null) Inspector.OnSelectChange();
            });
            AddToOperationSaver(OpSave);
        }

        // Stop the same mouse-up from being read as a selection click.
        _DragConsumedClick = true;
        CancelDragTracking();
    }

    private static void WriteRaw(DragItem Item, float Time, float Degree)
    {
        if (Item.Tap != null) { Item.Tap.Time = Time; Item.Tap.Degree = Degree; }
        else if (Item.Hold != null) { Item.Hold.Time = Time; Item.Hold.Degree = Degree; }
    }

    /// <summary>
    /// Consumed once by DetectNoteSelection so that the button release
    /// which ended a drag does not re-run selection.
    /// </summary>
    private bool ConsumeDragClick()
    {
        if (!_DragConsumedClick) return false;
        _DragConsumedClick = false;
        return true;
    }
}
