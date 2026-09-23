using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tools that work on whatever is already selected: thinning a selection
/// down to its even or odd members, and turning a motion's values around.
///
/// Thinning counts in the order things happen: the first selected note or
/// motion is number one. Keeping the even ones of six leaves the second,
/// fourth and sixth. Notes and motions are counted separately, so a
/// selection holding both thins each of them on its own.
///
/// Inverting mirrors what a motion does: a camera that dropped 20 rises 20,
/// a rotation to one side turns to the other. Only the values change, never
/// the timing, so the movement keeps its place and its ease.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>
    /// The motion a plain click last landed on, which is where a later
    /// Shift+click measures its range from.
    /// </summary>
    private Lanotalium.Chart.LanotaCameraBase _LastClickedMotion;

    /// <summary>
    /// Shift+click selects everything between the motion clicked before and
    /// this one, from A to B. Both have to be the same kind of motion, since
    /// the rows of the timeline are separate runs; a Shift+click across rows
    /// falls back to a plain click.
    /// </summary>
    private bool SelectMotionRangeTo(Lanotalium.Chart.LanotaCameraBase Motion)
    {
        if (_LastClickedMotion == null || _LastClickedMotion == Motion) return false;

        List<Lanotalium.Chart.LanotaCameraBase> Row = MotionRowOf(Motion);
        int From = Row.IndexOf(_LastClickedMotion);
        int To = Row.IndexOf(Motion);
        if (From == -1 || To == -1) return false;
        if (From > To) { int Swap = From; From = To; To = Swap; }

        DeSelectAllMotions();
        for (int i = From; i <= To; ++i) SelectMotion(Row[i]);

        if (SelectedMotions.Count >= 2) InspectorManager.ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Multiple, 0);
        else if (SelectedMotions.Count == 1) RefreshMotionInspectorFor(SelectedMotions[0]);
        InspectorManager.ArrangeComponentsUi();
        return true;
    }

    /// <summary>The timeline row a motion lives in, in playing order.</summary>
    private List<Lanotalium.Chart.LanotaCameraBase> MotionRowOf(Lanotalium.Chart.LanotaCameraBase Motion)
    {
        List<Lanotalium.Chart.LanotaCameraBase> Row = new List<Lanotalium.Chart.LanotaCameraBase>();
        if (Motion is Lanotalium.Chart.LanotaCameraXZ) foreach (Lanotalium.Chart.LanotaCameraXZ M in TunerManager.CameraManager.Horizontal) Row.Add(M);
        else if (Motion is Lanotalium.Chart.LanotaCameraY) foreach (Lanotalium.Chart.LanotaCameraY M in TunerManager.CameraManager.Vertical) Row.Add(M);
        else if (Motion is Lanotalium.Chart.LanotaCameraRot) foreach (Lanotalium.Chart.LanotaCameraRot M in TunerManager.CameraManager.Rotation) Row.Add(M);
        else if (Motion is Lanotalium.Chart.LanotaCameraTrs && TunerManager.CameraManager.Transparency != null) foreach (Lanotalium.Chart.LanotaCameraTrs M in TunerManager.CameraManager.Transparency) Row.Add(M);
        return Row;
    }

    public void DetectSelectionTools()
    {
        if (LimSystem.ChartContainer == null) return;
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
        if (!Input.GetKeyDown(KeyCode.R)) return;
        if (IsTypingInTextField()) return;
        InvertSelectedMotionValues();
    }

    /// <summary>Keeps the 2nd, 4th, 6th… of the selection.</summary>
    public void SelectEvenOfSelection() { ThinSelection(false); }

    /// <summary>Keeps the 1st, 3rd, 5th… of the selection.</summary>
    public void SelectOddOfSelection() { ThinSelection(true); }

    private void ThinSelection(bool KeepOdd)
    {
        ThinNoteSelection(KeepOdd);
        ThinMotionSelection(KeepOdd);
        InspectorManager.OnSelectChange();
        InspectorManager.ArrangeComponentsUi();
    }

    private void ThinNoteSelection(bool KeepOdd)
    {
        if (SelectedTapNote.Count + SelectedHoldNote.Count < 2) return;

        // Taps and holds are counted as one run, in the order they are played.
        List<Lanotalium.Chart.LanotaTapNote> Taps = new List<Lanotalium.Chart.LanotaTapNote>(SelectedTapNote);
        List<Lanotalium.Chart.LanotaHoldNote> Holds = new List<Lanotalium.Chart.LanotaHoldNote>(SelectedHoldNote);
        List<object> Ordered = new List<object>();
        foreach (Lanotalium.Chart.LanotaTapNote Tap in Taps) Ordered.Add(Tap);
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in Holds) Ordered.Add(Hold);
        Ordered.Sort((object A, object B) =>
        {
            float TimeA = A is Lanotalium.Chart.LanotaTapNote ? ((Lanotalium.Chart.LanotaTapNote)A).Time : ((Lanotalium.Chart.LanotaHoldNote)A).Time;
            float TimeB = B is Lanotalium.Chart.LanotaTapNote ? ((Lanotalium.Chart.LanotaTapNote)B).Time : ((Lanotalium.Chart.LanotaHoldNote)B).Time;
            if (TimeA != TimeB) return TimeA.CompareTo(TimeB);
            float DegreeA = A is Lanotalium.Chart.LanotaTapNote ? ((Lanotalium.Chart.LanotaTapNote)A).Degree : ((Lanotalium.Chart.LanotaHoldNote)A).Degree;
            float DegreeB = B is Lanotalium.Chart.LanotaTapNote ? ((Lanotalium.Chart.LanotaTapNote)B).Degree : ((Lanotalium.Chart.LanotaHoldNote)B).Degree;
            return DegreeA.CompareTo(DegreeB);
        });

        for (int i = 0; i < Ordered.Count; ++i)
        {
            bool IsOdd = i % 2 == 0;
            if (IsOdd == KeepOdd) continue;
            Lanotalium.Chart.LanotaTapNote Tap = Ordered[i] as Lanotalium.Chart.LanotaTapNote;
            if (Tap != null) DeSelectTapNote(Tap);
            else DeSelectHoldNote((Lanotalium.Chart.LanotaHoldNote)Ordered[i]);
        }
    }

    private void ThinMotionSelection(bool KeepOdd)
    {
        if (SelectedMotions.Count < 2) return;

        List<Lanotalium.Chart.LanotaCameraBase> Ordered = new List<Lanotalium.Chart.LanotaCameraBase>(SelectedMotions);
        Ordered.Sort((Lanotalium.Chart.LanotaCameraBase A, Lanotalium.Chart.LanotaCameraBase B) => { return A.Time.CompareTo(B.Time); });

        List<Lanotalium.Chart.LanotaCameraBase> Drop = new List<Lanotalium.Chart.LanotaCameraBase>();
        for (int i = 0; i < Ordered.Count; ++i)
        {
            bool IsOdd = i % 2 == 0;
            if (IsOdd != KeepOdd) Drop.Add(Ordered[i]);
        }
        // Collected first: deselecting takes them out of the list being read.
        foreach (Lanotalium.Chart.LanotaCameraBase Motion in Drop) DeSelectMotion(Motion);

        if (SelectedMotions.Count == 1) RefreshMotionInspectorFor(SelectedMotions[0]);
        else if (SelectedMotions.Count >= 2) InspectorManager.ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Multiple, 0);
    }

    /// <summary>
    /// Turns every selected motion's values around. A delta of -20 becomes
    /// 20; for a type 8 horizontal both its degree and its radius are deltas,
    /// so both turn. A type 11 target radius is left alone: it is a place on
    /// the ring, not an amount, and there is no negative side of it.
    /// </summary>
    public void InvertSelectedMotionValues()
    {
        if (SelectedMotions.Count == 0) return;
        List<Lanotalium.Chart.LanotaCameraBase> Motions = new List<Lanotalium.Chart.LanotaCameraBase>(SelectedMotions);

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() => { InvertMotionValues(Motions); });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() => { InvertMotionValues(Motions); });
        InvertMotionValues(Motions);
        AddToOperationSaver(OpSave);
    }

    private void InvertMotionValues(List<Lanotalium.Chart.LanotaCameraBase> Motions)
    {
        foreach (Lanotalium.Chart.LanotaCameraBase Motion in Motions)
        {
            // A transparency is a level from 0 to 100, not a change that can
            // go the other way: it turns around its middle instead, 30
            // becoming 70. Doing it twice gives the number back, which is
            // what the undo of this relies on.
            if (Motion.Type == 14)
            {
                Motion.ctp = LimCameraManager.OpaqueTransparency - Motion.ctp;
                continue;
            }
            Motion.ctp = -Motion.ctp;
            if (Motion.Type == 8) Motion.ctp1 = -Motion.ctp1;
        }
        if (Motions.Count == 1) RefreshMotionInspectorFor(Motions[0]);
    }

    /// <summary>Points the motion component at a motion's current row.</summary>
    private void RefreshMotionInspectorFor(Lanotalium.Chart.LanotaCameraBase Motion)
    {
        Lanotalium.Chart.LanotaCameraXZ Hor = Motion as Lanotalium.Chart.LanotaCameraXZ;
        if (Hor != null)
        {
            InspectorManager.ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Horizontal, TunerManager.CameraManager.Horizontal.IndexOf(Hor));
            return;
        }
        Lanotalium.Chart.LanotaCameraY Ver = Motion as Lanotalium.Chart.LanotaCameraY;
        if (Ver != null)
        {
            InspectorManager.ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Vertical, TunerManager.CameraManager.Vertical.IndexOf(Ver));
            return;
        }
        Lanotalium.Chart.LanotaCameraRot Rot = Motion as Lanotalium.Chart.LanotaCameraRot;
        if (Rot != null)
        {
            InspectorManager.ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Rotation, TunerManager.CameraManager.Rotation.IndexOf(Rot));
            return;
        }
        Lanotalium.Chart.LanotaCameraTrs Trs = Motion as Lanotalium.Chart.LanotaCameraTrs;
        if (Trs != null && TunerManager.CameraManager.Transparency != null)
            InspectorManager.ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Transparency, TunerManager.CameraManager.Transparency.IndexOf(Trs));
    }
}
