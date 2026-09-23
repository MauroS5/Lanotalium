using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ctrl + arrow keys nudge the selected notes from line to line.
///
/// With nothing selected the shortcut keeps its old meaning: the media
/// player walks the timeline beatline by beatline (see LimMediaPlayerManager).
/// With one or more notes selected the timeline stays where it is and the
/// notes move instead:
///
///   Ctrl  + Left / Right : to the previous / next angleline
///   Ctrl  + Up   / Down  : to the next / previous beatline
///   Shift + Left / Right : 5 degrees, ignoring the anglelines
///   Shift + Up   / Down  : 1 degree, ignoring the anglelines
///   Alt   + Left / Right : turns the selection over in time
///   Alt   + Up   / Down  : mirrors the selection across its own middle degree
///
/// The two Flip buttons of the Creator window do the same mirroring.
///
/// The joints of a rail move with the same keys, and travel with any notes
/// selected alongside them. The two flips are the exception: they turn a
/// shape over, and one joint of a rail has no shape of its own to turn.
///
/// A selection travels rigidly: the note closest to the judgement line, the
/// earliest one in time, decides which line the group lands on, and every
/// other note moves by that same amount. A staircase stays a staircase. The
/// whole key press is a single undo entry.
///
/// Anglelines only exist while the Angleline tool of the Creator panel is
/// enabled; without them the left/right shortcut does nothing.
/// </summary>
public partial class LimOperationManager
{
    private const float NudgeTimeEpsilon = 0.0001f;
    private const float NudgeDegreeEpsilon = 0.01f;
    private const float NudgeCoarseDegree = 5f;
    private const float NudgeFineDegree = 1f;

    private class NudgeItem
    {
        public Lanotalium.Chart.LanotaTapNote Tap;
        public Lanotalium.Chart.LanotaHoldNote Hold;
        /// <summary>Set when this is one joint of Hold rather than Hold itself.</summary>
        public Lanotalium.Chart.LanotaJoints Joint;
        public float OriginTime;
        public float OriginDegree;
        public float TargetTime;
        public float TargetDegree;
    }

    /// <summary>
    /// True when at least one note is selected. The media player reads this
    /// to know that Ctrl + Left/Right belongs to the notes, not the timeline.
    /// </summary>
    public bool HasNoteSelection { get { return SelectedTapNote.Count > 0 || SelectedHoldNote.Count > 0; } }

    /// <summary>
    /// True when the arrow keys are the notes' business rather than the
    /// timeline's: something is selected, or a paste is waiting to be placed
    /// and the arrows are flipping its preview.
    /// </summary>
    public bool ArrowsBelongToNotes { get { return HasAnySelection || IsPasting; } }

    public void DetectNoteNudge()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;
        if (_PasteActive || IsNoteDragInProgress) return;
        if (IsTypingInTextField()) return;
        if (!HasAnySelection) return;

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) NudgeSelectionToAngleline(true);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) NudgeSelectionToAngleline(false);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) NudgeSelectionToBeatline(true);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) NudgeSelectionToBeatline(false);
        }
        else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) NudgeSelectionByDegree(NudgeCoarseDegree);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) NudgeSelectionByDegree(-NudgeCoarseDegree);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) NudgeSelectionByDegree(NudgeFineDegree);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) NudgeSelectionByDegree(-NudgeFineDegree);
        }
        else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)) FlipSelectionHorizontal();
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) FlipSelectionVertical();
        }
    }

    /// <summary>
    /// Turns the selection over in time, the way a sheet of paper turns over:
    /// it stays exactly where it was on the ring, every note keeps its degree,
    /// and the last note becomes the first. A staircase running one way comes
    /// back running the other way.
    ///
    /// The axis is the selection's own span, from the earliest note to the end
    /// of the latest one, so nothing drifts away from where it was written.
    /// </summary>
    public void FlipSelectionHorizontal()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;
        if (!HasNoteSelection) return;

        List<NudgeItem> Items = CollectSelectionForNudge(false);
        float Min = float.MaxValue, Max = float.MinValue;
        foreach (NudgeItem Item in Items)
        {
            if (Item.OriginTime < Min) Min = Item.OriginTime;
            float End = Item.OriginTime + NudgeDuration(Item);
            if (End > Max) Max = End;
        }

        // A hold is mirrored by its whole span, so it still ends where the
        // note that used to follow it began.
        foreach (NudgeItem Item in Items) Item.TargetTime = Min + Max - Item.OriginTime - NudgeDuration(Item);
        CommitFlip(Items, null);
    }

    /// <summary>
    /// Mirrors the selection across its own middle degree: the notes keep
    /// their timings and the shape is reflected left to right, again without
    /// the group leaving the place it occupied.
    /// </summary>
    public void FlipSelectionVertical()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;
        if (!HasNoteSelection) return;

        List<NudgeItem> Items = CollectSelectionForNudge(false);
        // Measured as turns away from the first note, so a group sitting
        // across the 0 / 360 seam is still read as one continuous shape.
        float First = Items[0].OriginDegree;
        float Min = float.MaxValue, Max = float.MinValue;
        List<float> Unwrapped = new List<float>();
        foreach (NudgeItem Item in Items)
        {
            float Degree = First + Mathf.DeltaAngle(First, Item.OriginDegree);
            Unwrapped.Add(Degree);
            if (Degree < Min) Min = Degree;
            if (Degree > Max) Max = Degree;
        }
        for (int i = 0; i < Items.Count; ++i) Items[i].TargetDegree = LimMathUtil.NormalizeDegree(Min + Max - Unwrapped[i]);

        // The mirror reverses the turning direction, so a hold's rail has to
        // turn the other way too.
        List<Lanotalium.Chart.LanotaHoldNote> Rails = new List<Lanotalium.Chart.LanotaHoldNote>();
        foreach (NudgeItem Item in Items)
            if (Item.Hold != null && Item.Hold.Joints != null && Item.Hold.Joints.Count > 0) Rails.Add(Item.Hold);
        CommitFlip(Items, Rails);
    }

    private static float NudgeDuration(NudgeItem Item)
    {
        return Item.Hold != null ? Item.Hold.Duration : 0;
    }

    /// <summary>
    /// Applies a flip as a single undo entry. Rails, when given, have their
    /// joints turned the other way, which undoes itself on the way back.
    /// </summary>
    private void CommitFlip(List<NudgeItem> Items, List<Lanotalium.Chart.LanotaHoldNote> Rails)
    {
        LimInspectorManager Inspector = InspectorManager;
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            foreach (NudgeItem Item in Items) WriteNudge(Item, Item.TargetTime, Item.TargetDegree);
            if (Rails != null) foreach (Lanotalium.Chart.LanotaHoldNote Hold in Rails) ReverseJointDegrees(Hold);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            foreach (NudgeItem Item in Items) WriteNudge(Item, Item.OriginTime, Item.OriginDegree);
            if (Rails != null) foreach (Lanotalium.Chart.LanotaHoldNote Hold in Rails) ReverseJointDegrees(Hold);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Forward();
        AddToOperationSaver(OpSave);
    }

    private static void ReverseJointDegrees(Lanotalium.Chart.LanotaHoldNote Hold)
    {
        foreach (Lanotalium.Chart.LanotaJoints Joint in Hold.Joints) Joint.dDegree = -Joint.dDegree;
    }

    /// <summary>
    /// Everything the arrows are to move. Joints are left out of the two
    /// flips: a flip is about the shape a group of notes makes, and one joint
    /// of a rail turned over on its own has no shape of its own to turn.
    /// </summary>
    private List<NudgeItem> CollectSelectionForNudge(bool IncludeJoints = true)
    {
        List<NudgeItem> Items = new List<NudgeItem>();
        foreach (Lanotalium.Chart.LanotaTapNote Tap in SelectedTapNote)
            Items.Add(new NudgeItem { Tap = Tap, OriginTime = Tap.Time, OriginDegree = Tap.Degree, TargetTime = Tap.Time, TargetDegree = Tap.Degree });
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in SelectedHoldNote)
            Items.Add(new NudgeItem { Hold = Hold, OriginTime = Hold.Time, OriginDegree = Hold.Degree, TargetTime = Hold.Time, TargetDegree = Hold.Degree });
        if (!IncludeJoints) return Items;
        foreach (RailJoint Selected in SelectedJoints)
        {
            RefreshJointAbsolutes(Selected.Hold);
            Items.Add(new NudgeItem
            {
                Hold = Selected.Hold,
                Joint = Selected.Joint,
                OriginTime = Selected.Joint.aTime,
                OriginDegree = Selected.Joint.aDegree,
                TargetTime = Selected.Joint.aTime,
                TargetDegree = Selected.Joint.aDegree
            });
        }
        return Items;
    }

    private void NudgeSelectionToAngleline(bool Forward)
    {
        LimAngleLineManager Angleline = LimClickToCreateManager.SharedAnglelineManager;
        if (Angleline == null || !Angleline.Enable) return;

        List<NudgeItem> Items = CollectSelectionForNudge();
        NudgeItem Anchor = FindAnchor(Items);
        if (Anchor == null) return;

        // Compared in the note's own degree, the one the inspector shows and
        // the one typed in the angleline field, so the anchor lands on that
        // exact value instead of on a camera-rotated copy of it.
        float DeltaDegree;
        if (!Angleline.TryFindPrevOrNextAngleline(Anchor.OriginDegree, Forward, NudgeDegreeEpsilon, out DeltaDegree)) return;

        foreach (NudgeItem Item in Items) Item.TargetDegree = Item.OriginDegree + DeltaDegree;
        CommitNudge(Items);
    }

    private void NudgeSelectionToBeatline(bool Forward)
    {
        if (InspectorManager == null || InspectorManager.ComponentBpm == null) return;
        List<float> BeatlineTimes = InspectorManager.ComponentBpm.BeatlineTimes;
        if (BeatlineTimes == null || BeatlineTimes.Count == 0) return;

        List<NudgeItem> Items = CollectSelectionForNudge();
        NudgeItem Anchor = FindAnchor(Items);
        if (Anchor == null) return;

        float DeltaTime = FindPrevOrNextBeatlineByTime(BeatlineTimes, Anchor.OriginTime, Forward) - Anchor.OriginTime;
        if (DeltaTime == 0) return;

        // The group keeps its exact spacing in seconds, so an uneven grid
        // never squeezes or stretches what was selected.
        foreach (NudgeItem Item in Items) Item.TargetTime = Item.OriginTime + DeltaTime;
        CommitNudge(Items);
    }

    /// <summary>
    /// Turns the whole selection by a fixed number of degrees, ignoring the
    /// anglelines: free rotation for shapes that do not belong to the grid.
    /// </summary>
    private void NudgeSelectionByDegree(float DeltaDegree)
    {
        List<NudgeItem> Items = CollectSelectionForNudge();
        foreach (NudgeItem Item in Items) Item.TargetDegree = Item.OriginDegree + DeltaDegree;
        CommitNudge(Items);
    }

    /// <summary>
    /// The note the group moves by: the one closest to the judgement line,
    /// that is the earliest of the selection.
    /// </summary>
    private static NudgeItem FindAnchor(List<NudgeItem> Items)
    {
        NudgeItem Anchor = null;
        foreach (NudgeItem Item in Items)
            if (Anchor == null || Item.OriginTime < Anchor.OriginTime) Anchor = Item;
        return Anchor;
    }

    /// <summary>
    /// First beatline strictly after (or before) Time. A note sitting off the
    /// grid therefore lands on the grid with the first press. Returns Time
    /// unchanged at either end of the chart.
    /// </summary>
    private static float FindPrevOrNextBeatlineByTime(List<float> BeatlineTimes, float Time, bool Forward)
    {
        if (Forward)
        {
            for (int i = 0; i < BeatlineTimes.Count; ++i)
                if (BeatlineTimes[i] > Time + NudgeTimeEpsilon) return BeatlineTimes[i];
        }
        else
        {
            for (int i = BeatlineTimes.Count - 1; i >= 0; --i)
                if (BeatlineTimes[i] < Time - NudgeTimeEpsilon) return BeatlineTimes[i];
        }
        return Time;
    }

    /// <summary>
    /// Writes the whole selection at once as a single undo entry. Notes that
    /// had nowhere left to go stay where they are.
    /// </summary>
    private void CommitNudge(List<NudgeItem> Items)
    {
        bool Moved = false;
        foreach (NudgeItem Item in Items)
            if (Item.TargetTime != Item.OriginTime || Item.TargetDegree != Item.OriginDegree) { Moved = true; break; }
        if (!Moved) return;

        LimInspectorManager Inspector = InspectorManager;
        foreach (NudgeItem Item in Items) WriteNudge(Item, Item.TargetTime, Item.TargetDegree);
        if (Inspector != null) Inspector.OnSelectChange();

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            foreach (NudgeItem Item in Items) WriteNudge(Item, Item.TargetTime, Item.TargetDegree);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            foreach (NudgeItem Item in Items) WriteNudge(Item, Item.OriginTime, Item.OriginDegree);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        AddToOperationSaver(OpSave);
    }

    private static void WriteNudge(NudgeItem Item, float Time, float Degree)
    {
        if (Item.Joint != null)
        {
            // A joint's degree is a running total along its rail, so it is
            // not wrapped: see WriteJointRaw.
            WriteJointRaw(Item.Hold, Item.Joint, Time, Degree);
            return;
        }
        Degree = LimMathUtil.NormalizeDegree(Degree);
        if (Item.Tap != null) { Item.Tap.Time = Time; Item.Tap.Degree = Degree; }
        else if (Item.Hold != null) { Item.Hold.Time = Time; Item.Hold.Degree = Degree; }
    }
}
