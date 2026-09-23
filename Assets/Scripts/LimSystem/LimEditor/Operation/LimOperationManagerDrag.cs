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
/// The joints of a rail are dragged by the same code and behave like notes:
/// one moves on its own, the rest of the rail stays where it was, and the
/// joint after it takes up the slack. See LimOperationManagerRail.
///
/// Holding Ctrl fans the selection instead of moving it: see ApplyFan.
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

    /// <summary>
    /// One thing being dragged: a note, a rail, or one joint of a rail, in
    /// which case Hold is the rail it belongs to. A joint's place on the
    /// chart is the absolute time and degree it has reached, not the step it
    /// carries, which is what makes it move like a note.
    /// </summary>
    private class DragItem
    {
        public Lanotalium.Chart.LanotaTapNote Tap;
        public Lanotalium.Chart.LanotaHoldNote Hold;
        public Lanotalium.Chart.LanotaJoints Joint;
        public float OriginTime;
        public float OriginDegree;

        public float Time { get { return Joint != null ? Joint.aTime : (Tap != null ? Tap.Time : Hold.Time); } }
        public float Degree { get { return Joint != null ? Joint.aDegree : (Tap != null ? Tap.Degree : Hold.Degree); } }
    }

    private readonly List<DragItem> _DragItems = new List<DragItem>();
    private DragItem _DragAnchor;

    private bool _DragPending;
    private bool _DragActive;
    private bool _DragConsumedClick;
    private bool _DragFreeMove;
    private bool _DragFanMode;

    private Vector3 _DragMouseDownPosition;
    private float _DragGrabTime;
    private float _DragGrabDegree;

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
        // Ctrl and the left button belong to dragging the tuner itself, or
        // to pulling the end of a rail about.
        if (_TunerPanActive || _RailHandleActive) { CancelDragTracking(); return; }

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
            if (HoldIndex != -1) _DragAnchor = new DragItem { Hold = TunerManager.HoldNoteManager.HoldNote[HoldIndex] };
            else
            {
                RailJoint Grabbed = FindJointByInstanceId(InstanceId);
                if (Grabbed == null) return;
                _DragAnchor = new DragItem { Hold = Grabbed.Hold, Joint = Grabbed.Joint };
            }
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

        bool AnchorAlreadySelected;
        if (_DragAnchor.Joint != null) AnchorAlreadySelected = IsJointSelected(_DragAnchor.Joint);
        else if (_DragAnchor.Tap != null) AnchorAlreadySelected = IsTapNoteSelected(_DragAnchor.Tap);
        else AnchorAlreadySelected = IsHoldNoteSelected(_DragAnchor.Hold);
        if (!AnchorAlreadySelected)
        {
            // Grabbing an unselected note selects it alone, like clicking it.
            if (_DragAnchor.Joint != null) SelectJoint(_DragAnchor.Hold, _DragAnchor.Joint);
            else if (_DragAnchor.Tap != null) SelectTapNote(_DragAnchor.Tap);
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
            DragItem Item = (_DragAnchor.Joint == null && Hold == _DragAnchor.Hold) ? _DragAnchor : new DragItem { Hold = Hold };
            if (Item == _DragAnchor) AnchorInSelection = true;
            Item.OriginTime = Hold.Time; Item.OriginDegree = Hold.Degree;
            _DragItems.Add(Item);
        }
        foreach (RailJoint Selected in SelectedJoints)
        {
            // Worked out from the steps before anything is read off, so a
            // joint that has not been redrawn this frame still starts where
            // it really is.
            RefreshJointAbsolutes(Selected.Hold);
            DragItem Item = Selected.Joint == _DragAnchor.Joint ? _DragAnchor : new DragItem { Hold = Selected.Hold, Joint = Selected.Joint };
            if (Item == _DragAnchor) AnchorInSelection = true;
            Item.OriginTime = Selected.Joint.aTime; Item.OriginDegree = Selected.Joint.aDegree;
            _DragItems.Add(Item);
        }
        if (!AnchorInSelection || _DragItems.Count == 0) return false;

        // Where on the chart the pointer grabbed, so the note keeps its
        // offset from the cursor instead of jumping under it.
        float GrabTime, GrabDegree;
        if (!LimTunerCoordinate.TryGetChartPointAtMouse(TunerWindowRect, TunerCamera, TunerManager, out GrabTime, out GrabDegree)) return false;
        _DragGrabTime = GrabTime;
        _DragGrabDegree = GrabDegree;

        // Ctrl fans the selection out instead of moving it about, which only
        // means anything with more than one note in hand.
        _DragFanMode = _DragItems.Count > 1 && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

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

        if (_DragFanMode)
        {
            float FanDegree = _DragAnchor.OriginDegree + Mathf.DeltaAngle(_DragGrabDegree, PointerDegree);
            if (ApplyFan(ApplyAnglelineSnap(FanDegree)))
            {
                if (InspectorManager != null) InspectorManager.OnSelectChange();
                return;
            }
            // Nothing to fan about: fall through and move the group as usual.
            _DragFanMode = false;
        }

        // Move the anchor by how far the pointer travelled, then snap the
        // anchor itself: snapping the note rather than the cursor is what
        // makes it land exactly on a beatline or angleline.
        //
        // All of it in the note's own degrees. The pointer's travel is a
        // difference between two on-screen degrees, so the camera's rotation
        // cancels out of it and never has to be added or taken off.
        float AnchorTime = _DragAnchor.OriginTime + (PointerTime - _DragGrabTime);
        float AnchorDegree = _DragAnchor.OriginDegree + Mathf.DeltaAngle(_DragGrabDegree, PointerDegree);
        AnchorTime = ApplyBeatlineSnap(AnchorTime);
        AnchorDegree = ApplyAnglelineSnap(AnchorDegree);

        ApplyToNote(_DragAnchor, AnchorTime, AnchorDegree, false);

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

    /// <summary>
    /// Fans the selection out. Both ends of the run, the first note and the
    /// last, stay exactly where they were; the note being dragged goes where
    /// the pointer is; every other note is put on the straight line between
    /// the end on its own side and the dragged note, spaced by its own
    /// timing.
    ///
    /// So dragging an end swings the whole run as one line, and dragging a
    /// note from the middle bends the run at that note and leaves it looking
    /// like a > with the point where the pointer is. Nothing is ever carried
    /// past an end: the two notes that hold the run in place are the two the
    /// eye reads it by.
    ///
    /// Only the degrees move. The gesture is about the shape of the run, and
    /// dragging the timings about at the same time would make the lines it is
    /// measured against move as it is being drawn.
    ///
    /// Returns false when there is nothing to fan about, which is a selection
    /// whose notes all fall at the same moment: there is no run to spread.
    /// </summary>
    private bool ApplyFan(float DraggedDegree)
    {
        DragItem First, Last;
        if (!FindFanEnds(out First, out Last)) return false;

        float DraggedTime = _DragAnchor.OriginTime;
        for (int i = 0; i < _DragItems.Count; ++i)
        {
            DragItem Item = _DragItems[i];
            if (Item == _DragAnchor) { ApplyToNote(Item, Item.OriginTime, DraggedDegree, false); continue; }

            // A note sharing the dragged note's timing sits on the point of
            // the bend and has nowhere of its own to be, so it is left where
            // it is rather than piled onto the note being dragged.
            if (Mathf.Abs(Item.OriginTime - DraggedTime) < 0.0001f)
            {
                ApplyToNote(Item, Item.OriginTime, Item.OriginDegree, false);
                continue;
            }

            // The end of the run on this note's side of the one being
            // dragged. Dragging an end leaves every note on the same side,
            // which is what makes that case one straight line.
            DragItem End = Item.OriginTime < DraggedTime ? First : Last;
            float Span = DraggedTime - End.OriginTime;
            if (Mathf.Abs(Span) < 0.0001f) { ApplyToNote(Item, Item.OriginTime, Item.OriginDegree, false); continue; }

            // Measured as a turn from the end rather than a difference of two
            // degrees, so a run lying across 0 does not fan the wrong way.
            float Reach = Mathf.DeltaAngle(End.OriginDegree, DraggedDegree);
            float Percent = (Item.OriginTime - End.OriginTime) / Span;
            ApplyToNote(Item, Item.OriginTime, End.OriginDegree + Reach * Percent, false);
        }
        return true;
    }

    /// <summary>
    /// The first and last notes of the selection in time, which are the two
    /// the fan is pinned to. False when they are the same note, meaning the
    /// whole selection falls at one moment.
    /// </summary>
    private bool FindFanEnds(out DragItem First, out DragItem Last)
    {
        First = null;
        Last = null;
        foreach (DragItem Item in _DragItems)
        {
            if (First == null || Item.OriginTime < First.OriginTime) First = Item;
            if (Last == null || Item.OriginTime > Last.OriginTime) Last = Item;
        }
        if (First == null || Last == null || First == Last) return false;
        return Mathf.Abs(Last.OriginTime - First.OriginTime) >= 0.0001f;
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

    /// <summary>
    /// Snaps a note's own degree, not an on-screen one. Working in the note's
    /// own degrees is what makes it land on exactly the angleline that was
    /// typed: an on-screen degree carries the camera's rotation at the
    /// current moment, while the note is written with the rotation at its own
    /// timing, and with a rotation motion running those two differ by a few
    /// degrees, which is exactly what used to be left over.
    /// </summary>
    private float ApplyAnglelineSnap(float Degree)
    {
        if (_DragFreeMove) return Degree;
        if (!LimClickToCreateManager.SnapToAngleline) return Degree;
        LimAngleLineManager Angleline = LimClickToCreateManager.SharedAnglelineManager;
        if (Angleline == null || !Angleline.Enable) return Degree;
        return Angleline.FindNearestAnglelineByRelativeDegree(Degree);
    }

    /// <summary>
    /// Writes one note's position without touching the undo stack. The
    /// timing goes first: converting an absolute on-screen degree removes
    /// the camera rotation sampled at the note's own timing.
    /// </summary>
    private void ApplyToNote(DragItem Item, float Time, float Degree, bool DegreeIsAbsolute)
    {
        if (Item.Joint != null)
        {
            // A joint is written in the chart's own degrees whatever the
            // caller asked for: its step is measured from the joint before
            // it, which never carried a camera rotation to take off.
            WriteJointRaw(Item.Hold, Item.Joint, Time, Degree);
        }
        else if (Item.Tap != null)
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
        if (Item.Joint != null)
        {
            // Not wrapped into 0 to 360: a rail is allowed to wind round the
            // ring, and wrapping a joint would fold that turn back on itself.
            WriteJointRaw(Item.Hold, Item.Joint, Time, Degree);
            return;
        }
        Degree = LimMathUtil.NormalizeDegree(Degree);
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
