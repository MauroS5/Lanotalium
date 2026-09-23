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
///
/// Dropping does not end the paste: the preview stays up so the same pattern
/// can be laid down as many times as wanted, which is what copying a phrase
/// is usually for. Right click is what ends it.
///
/// While that preview is up the arrow keys flip what is about to be pasted,
/// the same two mirrors the Flip buttons make: left or right turns it over
/// in time, up or down mirrors it across its own middle degree. Each press
/// toggles, so pressing twice puts it back, and the preview shows the result
/// before anything is dropped. The copy itself is never modified.
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

    /// <summary>
    /// Which of the two things Ctrl+C put away last. Ctrl+V used to offer
    /// the motions back only while no notes had ever been copied, and the
    /// note clipboard is never emptied, so one copied note in a session shut
    /// the motion paste off until the editor was restarted.
    /// </summary>
    private bool _ClipboardHoldsMotions;

    // A pending paste can be flipped before it is dropped. The offsets kept
    // in the clipboard are always the ones that were copied; these two say
    // how to read them, so flipping back and forth loses nothing.
    private bool _PasteFlipTime, _PasteFlipDegree;
    private float _ClipboardSpanEnd;
    private float _ClipboardDegreeMin, _ClipboardDegreeMax;

    /// <summary>How solid the ghost notes are drawn, against 1 for a real note.</summary>
    private const float PasteGhostOpacity = 0.65f;

    /// <summary>True while a paste preview is on screen awaiting a click.</summary>
    public bool IsPasting { get { return _PasteActive; } }

    /// <summary>
    /// True while any note gesture owns the pointer: dragging notes or
    /// placing a pending paste. The box-selection rectangle stays out of
    /// the way while this holds.
    /// </summary>
    public bool IsNoteGestureInProgress { get { return IsNoteDragInProgress || _PasteActive || IsPanningTuner || IsDraggingRailEnd; } }

    public void DetectClipboard()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;

        if (!IsTypingInTextField())
        {
            bool Ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            // Motions and notes share the two shortcuts: whichever kind is
            // selected is the kind that gets copied, and a paste offers back
            // whichever was copied last.
            bool Motions = SelectedTapNote.Count == 0 && SelectedHoldNote.Count == 0 && SelectedMotions.Count > 0;
            if (Ctrl && Input.GetKeyDown(KeyCode.C))
            {
                // Whichever preview happens to be up belongs to the copy
                // before this one, and is now out of date.
                CancelPaste();
                if (TimeLineManager != null) TimeLineManager.CancelMotionPaste();
                if (Motions && TimeLineManager != null)
                {
                    TimeLineManager.CopySelectedMotions();
                    _ClipboardHoldsMotions = true;
                }
                else
                {
                    CopySelectionToClipboard();
                    _ClipboardHoldsMotions = false;
                }
            }
            if (Ctrl && Input.GetKeyDown(KeyCode.V)) BeginPasteOfWhateverWasCopied();
        }

        if (!_PasteActive) return;

        // Right click abandons a paste that has not been dropped yet, and
        // puts the selection down with it: one press, everything let go.
        if (Input.GetMouseButtonDown(1))
        {
            CancelPaste();
            SelectNothing();
            DeSelectAllMotions();
            return;
        }

        // While the preview is up the arrows belong to it, modifiers or not:
        // the selection is still the notes that were copied, and they must
        // not move while their copy is being placed.
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)) _PasteFlipTime = !_PasteFlipTime;
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) _PasteFlipDegree = !_PasteFlipDegree;

        UpdatePastePreview();

        if (Input.GetMouseButtonDown(0) && LimMousePosition.IsMouseOverWindow(TunerWindowRect)) CommitPaste();
    }

    /// <summary>
    /// Offers back whatever Ctrl+C took last. Falls through to the other
    /// kind when that one is empty, so a Ctrl+V still does something useful
    /// after the only copy of a session.
    /// </summary>
    private void BeginPasteOfWhateverWasCopied()
    {
        bool MotionsAvailable = TimeLineManager != null && TimeLineManager.HasCopiedMotions;
        if (_ClipboardHoldsMotions && MotionsAvailable) { TimeLineManager.BeginMotionPaste(); return; }
        if (!_ClipboardHoldsMotions && _Clipboard.Count > 0) { BeginPaste(); return; }
        if (MotionsAvailable) TimeLineManager.BeginMotionPaste();
        else BeginPaste();
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
        if (TimeLineManager != null) TimeLineManager.CancelMotionPaste();
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

        MeasureClipboardExtent();
        Debug.Log("[Clipboard] Copied " + _Clipboard.Count + " note(s).");
    }

    /// <summary>
    /// Records how far the copied notes reach, which is what the two flips
    /// mirror across. Measured once per copy, never while placing.
    /// </summary>
    private void MeasureClipboardExtent()
    {
        _ClipboardSpanEnd = 0;
        _ClipboardDegreeMin = float.MaxValue;
        _ClipboardDegreeMax = float.MinValue;
        foreach (ClipboardItem Item in _Clipboard)
        {
            float End = Item.OffsetTime + ClipboardDuration(Item);
            if (End > _ClipboardSpanEnd) _ClipboardSpanEnd = End;
            if (Item.OffsetDegree < _ClipboardDegreeMin) _ClipboardDegreeMin = Item.OffsetDegree;
            if (Item.OffsetDegree > _ClipboardDegreeMax) _ClipboardDegreeMax = Item.OffsetDegree;
        }
    }

    private static float ClipboardDuration(ClipboardItem Item)
    {
        return Item.Hold != null ? Item.Hold.Duration : 0;
    }

    /// <summary>
    /// Where one copied note sits right now, given the flips in force. Time
    /// turns the group over so the last note leads; degree mirrors it across
    /// its own middle, the same two moves the Flip buttons make.
    /// </summary>
    private void GetPasteOffsets(ClipboardItem Item, out float OffsetTime, out float OffsetDegree)
    {
        OffsetTime = _PasteFlipTime ? _ClipboardSpanEnd - Item.OffsetTime - ClipboardDuration(Item) : Item.OffsetTime;
        OffsetDegree = _PasteFlipDegree ? _ClipboardDegreeMin + _ClipboardDegreeMax - Item.OffsetDegree : Item.OffsetDegree;
    }

    public void BeginPaste()
    {
        if (_Clipboard.Count == 0) return;
        CancelPaste();
        // Snapping for a paste follows the toggles only; it must not
        // inherit the Shift-to-move-freely state of the last drag.
        _DragFreeMove = false;
        // Each paste starts the way the notes were copied.
        _PasteFlipTime = false;
        _PasteFlipDegree = false;

        Transform Parent = LimClickToCreateManager.SharedGhostParent;
        foreach (ClipboardItem Item in _Clipboard)
        {
            GameObject Prefab = Item.Tap != null
                ? TunerManager.TapNoteManager.GetPrefab(Item.Tap.Type, Item.Tap.Size, false)
                : TunerManager.HoldNoteManager.GetPrefab(Item.Hold.Size, false);
            if (Prefab == null) continue;
            Item.Ghost = Parent != null ? Instantiate(Prefab, Parent) : Instantiate(Prefab);
            FadeGhost(Item.Ghost);
            Item.Ghost.SetActive(false);
        }
        _PasteActive = true;
    }

    /// <summary>
    /// A pending paste is drawn see-through, so it reads as something not
    /// yet dropped and cannot be mistaken for the notes already on the ring.
    /// The notes the paste creates are new objects at full strength, so
    /// nothing has to be turned back afterwards.
    /// </summary>
    private static void FadeGhost(GameObject Ghost)
    {
        foreach (SpriteRenderer Renderer in Ghost.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Renderer.sortingLayerName = "ClickToCreate";
            Color Faded = Renderer.color;
            Faded.a *= PasteGhostOpacity;
            Renderer.color = Faded;
        }
        // A hold's rail is drawn with lines rather than sprites.
        foreach (LineRenderer Line in Ghost.GetComponentsInChildren<LineRenderer>(true))
        {
            Color Start = Line.startColor, End = Line.endColor;
            Start.a *= PasteGhostOpacity;
            End.a *= PasteGhostOpacity;
            Line.startColor = Start;
            Line.endColor = End;
        }
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
        // The pointer's degree is an on-screen one, and the ring on screen is
        // turned by the camera's rotation right now: that is how notes and
        // anglelines are both drawn. Taking that one rotation off gives the
        // note's own degree, and snapping there lands it on exactly the
        // angleline that was typed.
        AnchorRelativeDegree = Degree - TunerManager.CameraManager.CurrentRotation;
        AnchorRelativeDegree = ApplyAnglelineSnap(AnchorRelativeDegree);
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

            float OffsetTime, OffsetDegree;
            GetPasteOffsets(Item, out OffsetTime, out OffsetDegree);
            float Time = AnchorTime + OffsetTime;
            float RelativeDegree = AnchorRelativeDegree + OffsetDegree;
            // Put back the same rotation the notes on screen are drawn with,
            // so the ghost sits where the note itself will sit.
            float AbsDegree = RelativeDegree + TunerManager.CameraManager.CurrentRotation;

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
            float OffsetTime, OffsetDegree;
            GetPasteOffsets(Item, out OffsetTime, out OffsetDegree);
            if (Item.Tap != null)
            {
                Lanotalium.Chart.LanotaTapNote New = Item.Tap.DeepCopy();
                // Whatever is laid down goes into the group being worked on,
                // wherever it was copied from.
                New.Group = LimTimeGroups.ActiveGroup;
                New.Time = AnchorTime + OffsetTime;
                New.Degree = LimMathUtil.NormalizeDegree(AnchorRelativeDegree + OffsetDegree);
                NewTaps.Add(New);
            }
            else if (Item.Hold != null)
            {
                Lanotalium.Chart.LanotaHoldNote New = Item.Hold.DeepCopy();
                New.Group = LimTimeGroups.ActiveGroup;
                New.Time = AnchorTime + OffsetTime;
                New.Degree = LimMathUtil.NormalizeDegree(AnchorRelativeDegree + OffsetDegree);
                // A mirrored rail has to turn the other way, like the notes.
                if (_PasteFlipDegree && New.Joints != null) ReverseJointDegrees(New);
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
        // The ghosts stay where they are, ready for the next drop.
        // The click that dropped the paste must not also act as a selection.
        _DragConsumedClick = true;
    }
}
