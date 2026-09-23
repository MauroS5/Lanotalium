using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Moving and resizing motions straight on the timeline, with the right
/// mouse button, instead of typing timings into the inspector.
///
/// Press the right button on a motion bar and drag sideways: the motion
/// follows the pointer and its start sticks to the nearest beatline. Holding
/// Ctrl while dragging moves it freely. A motion never lands on top of its
/// neighbours, but dragging far enough past one hops over it: the bar takes
/// the next free gap once it has travelled beyond that neighbour's middle.
///
/// Near either end of a bar the cursor becomes a left-right arrow and the
/// same right-button drag stretches that end. Only the timing changes: ease,
/// origin and destination stay as they were, so the motion does the same
/// thing, faster or slower.
///
/// With several motions selected they travel together, keeping their spacing
/// and their durations, the earliest one deciding where the group lands. The
/// whole gesture is one undo entry.
/// </summary>
public partial class LimTimeLineManager
{
    /// <summary>Pixels from a bar's end that grab the end instead of the bar.</summary>
    private const float MotionEdgePixels = 6f;
    private const float MotionMinDuration = 0.0001f;

    /// <summary>
    /// Under this duration a bar is too thin to aim at, and gets a diamond
    /// on its start to click instead.
    /// </summary>
    private const float SmallMotionDuration = 0.051f;
    private const float SmallMotionMarkerSize = 12f;

    private enum MotionDragMode { None, Move, ResizeStart, ResizeEnd }

    private class MotionDragItem
    {
        public LanotaCameraBase Motion;
        public float OriginTime;
        public float OriginDuration;
    }

    private MotionDragMode _MotionDragMode = MotionDragMode.None;
    private readonly List<MotionDragItem> _MotionDragItems = new List<MotionDragItem>();
    private MotionDragItem _MotionDragAnchor;
    private float _MotionDragGrabTime;
    private bool _MotionCursorIsResize;
    private PointerEventData _MotionPointerData;
    private readonly List<RaycastResult> _MotionRaycastHits = new List<RaycastResult>();

    /// <summary>True while a motion is being dragged or resized.</summary>
    public bool IsMotionDragInProgress { get { return _MotionDragMode != MotionDragMode.None; } }

    /// <summary>The pointer leaves with the window; it must not keep the arrow.</summary>
    private void OnDisable()
    {
        if (!_MotionCursorIsResize) return;
        _MotionCursorIsResize = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    /// <summary>
    /// Gives a very short motion something to click on: a small diamond sat
    /// on its start, made from the bar's own sprite turned 45 degrees, so no
    /// new image is needed. It is a child of the bar, so the click reaches
    /// the bar's button exactly as clicking the bar would, and it disappears
    /// with it.
    /// </summary>
    private void AddSmallMotionMarker(LanotaCameraBase Motion)
    {
        if (Motion.TimeLineGameObject == null) return;
        if (Motion.Duration >= SmallMotionDuration) return;

        Image Bar = Motion.TimeLineGameObject.GetComponent<Image>();
        GameObject Marker = new GameObject("SmallMotionMarker", typeof(RectTransform), typeof(Image));
        RectTransform Rect = Marker.GetComponent<RectTransform>();
        Rect.SetParent(Motion.TimeLineGameObject.transform, false);
        // Anchored to the middle of the bar's left edge, whatever pivot the
        // bar itself was built with.
        Rect.anchorMin = new Vector2(0, 0.5f);
        Rect.anchorMax = new Vector2(0, 0.5f);
        Rect.pivot = new Vector2(0.5f, 0.5f);
        Rect.anchoredPosition = Vector2.zero;
        Rect.sizeDelta = new Vector2(SmallMotionMarkerSize, SmallMotionMarkerSize);
        Rect.localRotation = Quaternion.Euler(0, 0, 45);

        Image Diamond = Marker.GetComponent<Image>();
        if (Bar != null) Diamond.sprite = Bar.sprite;
        Diamond.type = Image.Type.Simple;
        Diamond.color = new Color(1, 1, 1, 0.9f);
        Diamond.raycastTarget = true;
    }

    private void DetectMotionDrag()
    {
        // A paste waiting to be placed owns both buttons.
        if (_MotionPasteActive) return;
        if (Input.GetMouseButtonDown(1)) BeginMotionDrag();
        else if (Input.GetMouseButton(1) && _MotionDragMode != MotionDragMode.None) UpdateMotionDrag();
        else if (Input.GetMouseButtonUp(1) && _MotionDragMode != MotionDragMode.None) FinishMotionDrag();
        else if (_MotionDragMode == MotionDragMode.None) UpdateMotionCursor();
    }

    /// <summary>
    /// Timing under the pointer, read the same way the time pointer above the
    /// timeline reads it.
    /// </summary>
    private float PointerTiming()
    {
        return (LimMousePosition.MousePosition.x - ViewRect.anchoredPosition.x - 200) / Scale + TunerManager.ChartTime;
    }

    /// <summary>True while the pointer is inside the timeline window.</summary>
    private bool IsPointerOverTimeLine()
    {
        Vector3 Mouse = LimMousePosition.MousePosition;
        if (Mouse.x < ViewRect.anchoredPosition.x || Mouse.x > ViewRect.anchoredPosition.x + ViewRect.sizeDelta.x) return false;
        return Mouse.y <= ViewRect.anchoredPosition.y && Mouse.y >= ViewRect.anchoredPosition.y - ViewRect.sizeDelta.y;
    }

    /// <summary>The motion bar under the pointer, or null.</summary>
    private LanotaCameraBase MotionUnderPointer()
    {
        EventSystem Events = EventSystem.current;
        if (Events == null) return null;
        if (!IsPointerOverTimeLine()) return null;

        // Reused: this is asked once a frame while the pointer is in here.
        if (_MotionPointerData == null) _MotionPointerData = new PointerEventData(Events);
        _MotionPointerData.position = Input.mousePosition;
        _MotionRaycastHits.Clear();
        Events.RaycastAll(_MotionPointerData, _MotionRaycastHits);
        foreach (RaycastResult Hit in _MotionRaycastHits)
        {
            LanotaCameraBase Motion = OperationManager.FindMotionBase(Hit.gameObject.GetInstanceID());
            if (Motion != null) return Motion;
            // The diamond of a very short motion stands in for its bar.
            if (Hit.gameObject.transform.parent == null) continue;
            Motion = OperationManager.FindMotionBase(Hit.gameObject.transform.parent.gameObject.GetInstanceID());
            if (Motion != null) return Motion;
        }
        return null;
    }

    /// <summary>
    /// Which part of a bar the pointer is on. The ends are measured in time
    /// so they stay the same handful of pixels at any zoom.
    /// </summary>
    private MotionDragMode GrabModeFor(LanotaCameraBase Motion, float Timing)
    {
        float Edge = MotionEdgePixels / Scale;
        // A very short bar would be all handle and impossible to move.
        Edge = Mathf.Min(Edge, Motion.Duration / 3f);
        if (Timing - Motion.Time <= Edge) return MotionDragMode.ResizeStart;
        if (Motion.Time + Motion.Duration - Timing <= Edge) return MotionDragMode.ResizeEnd;
        return MotionDragMode.Move;
    }

    private void UpdateMotionCursor()
    {
        bool OnEdge = false;
        LanotaCameraBase Motion = MotionUnderPointer();
        if (Motion != null)
        {
            MotionDragMode Mode = GrabModeFor(Motion, PointerTiming());
            OnEdge = Mode == MotionDragMode.ResizeStart || Mode == MotionDragMode.ResizeEnd;
        }
        if (OnEdge == _MotionCursorIsResize) return;
        _MotionCursorIsResize = OnEdge;
        if (OnEdge) Cursor.SetCursor(LimResizeCursor.Texture, LimResizeCursor.Hotspot, CursorMode.Auto);
        else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void BeginMotionDrag()
    {
        _MotionDragItems.Clear();
        _MotionDragAnchor = null;
        _MotionDragMode = MotionDragMode.None;

        LanotaCameraBase Motion = MotionUnderPointer();
        if (Motion == null) return;

        float Timing = PointerTiming();
        MotionDragMode Mode = GrabModeFor(Motion, Timing);
        _MotionDragGrabTime = Timing;

        if (OperationManager.SelectedMotions.Contains(Motion) && OperationManager.SelectedMotions.Count > 1)
        {
            // A selection travels as one block.
            foreach (LanotaCameraBase Selected in OperationManager.SelectedMotions)
                _MotionDragItems.Add(new MotionDragItem { Motion = Selected, OriginTime = Selected.Time, OriginDuration = Selected.Duration });
            foreach (MotionDragItem Item in _MotionDragItems)
                if (_MotionDragAnchor == null || Item.OriginTime < _MotionDragAnchor.OriginTime) _MotionDragAnchor = Item;
        }
        else
        {
            _MotionDragAnchor = new MotionDragItem { Motion = Motion, OriginTime = Motion.Time, OriginDuration = Motion.Duration };
            _MotionDragItems.Add(_MotionDragAnchor);
        }
        _MotionDragMode = Mode;
    }

    private void UpdateMotionDrag()
    {
        if (_MotionDragAnchor == null) { _MotionDragMode = MotionDragMode.None; return; }
        float Timing = PointerTiming();
        bool Free = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (_MotionDragMode == MotionDragMode.Move)
        {
            if (_MotionDragItems.Count > 1) MoveMotionGroup(Timing - _MotionDragGrabTime, Free);
            else MoveSingleMotion(Timing - _MotionDragGrabTime, Free);
        }
        else
        {
            ResizeMotion(Timing, Free);
        }

        foreach (MotionDragItem Item in _MotionDragItems) PlaceMotionBar(Item.Motion);
    }

    /// <summary>
    /// Moves the bar without rebuilding it, so a drag does not churn through
    /// a new GameObject every frame.
    /// </summary>
    private void PlaceMotionBar(LanotaCameraBase Motion)
    {
        if (Motion.TimeLineGameObject == null) return;
        RectTransform Rect = Motion.TimeLineGameObject.GetComponent<RectTransform>();
        Rect.anchoredPosition = new Vector2(Motion.Time * Scale, 0);
        Rect.sizeDelta = new Vector2(Motion.Duration * Scale, 30);
    }

    private float SnapMotionTime(float Time, bool Free)
    {
        if (Free) return Time;
        ComponentBpmManager Bpm = EditorManager.InspectorWindow.ComponentBpm;
        if (Bpm == null || !Bpm.EnableBeatline) return Time;
        if (Bpm.BeatlineTimes == null || Bpm.BeatlineTimes.Count == 0) return Time;
        // The list is in order, so the search stops as soon as it goes past.
        float Best = Time, BestDelta = float.MaxValue;
        foreach (float Beatline in Bpm.BeatlineTimes)
        {
            float Delta = Mathf.Abs(Beatline - Time);
            if (Delta < BestDelta) { BestDelta = Delta; Best = Beatline; }
            else if (Beatline > Time) break;
        }
        return Best;
    }

    /// <summary>Every motion of the same kind, in the order they are stored.</summary>
    private List<LanotaCameraBase> SiblingsOf(LanotaCameraBase Motion)
    {
        List<LanotaCameraBase> Siblings = new List<LanotaCameraBase>();
        if (Motion is LanotaCameraXZ) foreach (LanotaCameraXZ M in CameraManager.Horizontal) Siblings.Add(M);
        else if (Motion is LanotaCameraY) foreach (LanotaCameraY M in CameraManager.Vertical) Siblings.Add(M);
        else if (Motion is LanotaCameraRot) foreach (LanotaCameraRot M in CameraManager.Rotation) Siblings.Add(M);
        else if (Motion is LanotaCameraTrs && CameraManager.Transparency != null) foreach (LanotaCameraTrs M in CameraManager.Transparency) Siblings.Add(M);
        return Siblings;
    }

    private float SongLength { get { return TunerManager.MediaPlayerManager.Length; } }

    /// <summary>
    /// One motion follows the pointer. It slots into whichever gap between
    /// its neighbours the pointer has reached, which is what lets a long
    /// enough drag hop over a neighbour, and it never overlaps one.
    /// </summary>
    private void MoveSingleMotion(float DeltaTime, bool Free)
    {
        LanotaCameraBase Motion = _MotionDragAnchor.Motion;
        float Duration = _MotionDragAnchor.OriginDuration;
        float Candidate = SnapMotionTime(_MotionDragAnchor.OriginTime + DeltaTime, Free);
        Candidate = Mathf.Clamp(Candidate, 0, Mathf.Max(0, SongLength - Duration));

        List<LanotaCameraBase> Others = SiblingsOf(Motion);
        Others.Remove(Motion);

        // The gap the bar belongs to now: it has passed a neighbour once its
        // middle is past that neighbour's middle.
        int Slot = 0;
        float Middle = Candidate + Duration / 2f;
        foreach (LanotaCameraBase Other in Others)
            if (Other.Time + Other.Duration / 2f < Middle) ++Slot;

        float Lower = Slot > 0 ? Others[Slot - 1].Time + Others[Slot - 1].Duration : 0;
        float Upper = (Slot < Others.Count ? Others[Slot].Time : SongLength) - Duration;
        if (Upper < Lower) return;

        Motion.Time = Mathf.Clamp(Candidate, Lower, Upper);
        Motion.Duration = Duration;
    }

    /// <summary>
    /// A whole selection moves rigidly, by the amount the earliest motion can
    /// travel: the group keeps its spacing and nothing in it overlaps a
    /// motion that was left out of the selection.
    /// </summary>
    private void MoveMotionGroup(float DeltaTime, bool Free)
    {
        float Delta = SnapMotionTime(_MotionDragAnchor.OriginTime + DeltaTime, Free) - _MotionDragAnchor.OriginTime;

        float MinDelta = float.MinValue, MaxDelta = float.MaxValue;
        foreach (MotionDragItem Item in _MotionDragItems)
        {
            float Lower = 0, Upper = SongLength;
            foreach (LanotaCameraBase Other in SiblingsOf(Item.Motion))
            {
                if (IsBeingDragged(Other)) continue;
                float OtherEnd = Other.Time + Other.Duration;
                if (OtherEnd <= Item.OriginTime && OtherEnd > Lower) Lower = OtherEnd;
                if (Other.Time >= Item.OriginTime + Item.OriginDuration && Other.Time < Upper) Upper = Other.Time;
            }
            MinDelta = Mathf.Max(MinDelta, Lower - Item.OriginTime);
            MaxDelta = Mathf.Min(MaxDelta, Upper - Item.OriginTime - Item.OriginDuration);
        }
        if (MaxDelta < MinDelta) return;
        Delta = Mathf.Clamp(Delta, MinDelta, MaxDelta);

        foreach (MotionDragItem Item in _MotionDragItems)
        {
            Item.Motion.Time = Item.OriginTime + Delta;
            Item.Motion.Duration = Item.OriginDuration;
        }
    }

    private bool IsBeingDragged(LanotaCameraBase Motion)
    {
        foreach (MotionDragItem Item in _MotionDragItems) if (Item.Motion == Motion) return true;
        return false;
    }

    /// <summary>
    /// Stretches one end of a motion. Only the timing moves: whatever the
    /// motion does, and how it eases into it, is left alone.
    ///
    /// When the grabbed motion is part of a selection, every motion in it is
    /// stretched by the same amount of time, each one keeping its own end
    /// still, so a row of motions grows or shrinks together. The amount is
    /// held back to what the tightest of them allows, so none of them ends up
    /// inside a neighbour or shorter than nothing.
    /// </summary>
    private void ResizeMotion(float Timing, bool Free)
    {
        float AnchorEdge = _MotionDragMode == MotionDragMode.ResizeStart
            ? _MotionDragAnchor.OriginTime
            : _MotionDragAnchor.OriginTime + _MotionDragAnchor.OriginDuration;
        float Delta = SnapMotionTime(Timing, Free) - AnchorEdge;

        // What each motion in the gesture can take, the smallest run wins.
        float MinDelta = float.MinValue, MaxDelta = float.MaxValue;
        foreach (MotionDragItem Item in _MotionDragItems)
        {
            float Start = Item.OriginTime;
            float End = Item.OriginTime + Item.OriginDuration;
            float Lower = 0, Upper = SongLength;
            foreach (LanotaCameraBase Other in SiblingsOf(Item.Motion))
            {
                if (IsBeingDragged(Other)) continue;
                float OtherEnd = Other.Time + Other.Duration;
                if (OtherEnd <= Start && OtherEnd > Lower) Lower = OtherEnd;
                if (Other.Time >= End && Other.Time < Upper) Upper = Other.Time;
            }
            if (_MotionDragMode == MotionDragMode.ResizeStart)
            {
                MinDelta = Mathf.Max(MinDelta, Lower - Start);
                MaxDelta = Mathf.Min(MaxDelta, End - MotionMinDuration - Start);
            }
            else
            {
                MinDelta = Mathf.Max(MinDelta, Start + MotionMinDuration - End);
                MaxDelta = Mathf.Min(MaxDelta, Upper - End);
            }
        }
        if (MaxDelta < MinDelta) return;
        Delta = Mathf.Clamp(Delta, MinDelta, MaxDelta);

        foreach (MotionDragItem Item in _MotionDragItems)
        {
            if (_MotionDragMode == MotionDragMode.ResizeStart)
            {
                Item.Motion.Time = Item.OriginTime + Delta;
                Item.Motion.Duration = Item.OriginDuration - Delta;
            }
            else
            {
                Item.Motion.Time = Item.OriginTime;
                Item.Motion.Duration = Item.OriginDuration + Delta;
            }
        }
    }

    /// <summary>
    /// Writes the gesture down as one undo entry and puts the timeline back
    /// in order: the lists are sorted by timing, so a motion that hopped over
    /// another has to take its new place in them.
    /// </summary>
    private void FinishMotionDrag()
    {
        List<MotionDragItem> Items = new List<MotionDragItem>(_MotionDragItems);
        _MotionDragMode = MotionDragMode.None;
        _MotionDragItems.Clear();
        _MotionDragAnchor = null;

        List<float> FinalTimes = new List<float>();
        List<float> FinalDurations = new List<float>();
        bool Moved = false;
        foreach (MotionDragItem Item in Items)
        {
            FinalTimes.Add(Item.Motion.Time);
            FinalDurations.Add(Item.Motion.Duration);
            if (Item.Motion.Time != Item.OriginTime || Item.Motion.Duration != Item.OriginDuration) Moved = true;
        }
        if (!Moved) { RebuildTimeLineAfterMotionDrag(); return; }

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            for (int i = 0; i < Items.Count; ++i) { Items[i].Motion.Time = FinalTimes[i]; Items[i].Motion.Duration = FinalDurations[i]; }
            RebuildTimeLineAfterMotionDrag();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            foreach (MotionDragItem Item in Items) { Item.Motion.Time = Item.OriginTime; Item.Motion.Duration = Item.OriginDuration; }
            RebuildTimeLineAfterMotionDrag();
        });
        OperationManager.AddToOperationSaver(OpSave);
        RebuildTimeLineAfterMotionDrag();
    }

    private void RebuildTimeLineAfterMotionDrag()
    {
        CameraManager.SortHorizontalList();
        CameraManager.SortVerticalList();
        CameraManager.SortRotationList();
        InstantiateAllTimeLine();

        // Rebuilding makes new bars, so the selection has to be painted again
        // and the inspector pointed at the row the motion now sits in.
        foreach (LanotaCameraBase Motion in OperationManager.SelectedMotions)
            if (Motion.TimeLineGameObject != null) Motion.TimeLineGameObject.GetComponent<Image>().color = Selected;
        RefreshMotionInspectorIndex();
    }

    private void RefreshMotionInspectorIndex()
    {
        if (OperationManager.SelectedMotions.Count != 1) return;
        LanotaCameraBase Motion = OperationManager.SelectedMotions[0];
        int Index = CameraManager.Horizontal.IndexOf(Motion as LanotaCameraXZ);
        if (Motion is LanotaCameraXZ && Index != -1) { ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Horizontal, Index); return; }
        Index = CameraManager.Vertical.IndexOf(Motion as LanotaCameraY);
        if (Motion is LanotaCameraY && Index != -1) { ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Vertical, Index); return; }
        Index = CameraManager.Rotation.IndexOf(Motion as LanotaCameraRot);
        if (Motion is LanotaCameraRot && Index != -1) { ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Rotation, Index); return; }
        if (CameraManager.Transparency == null) return;
        Index = CameraManager.Transparency.IndexOf(Motion as LanotaCameraTrs);
        if (Motion is LanotaCameraTrs && Index != -1) ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Transparency, Index);
    }
}
