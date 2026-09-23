using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;

/// <summary>
/// Joining a run of separate hold notes back into one, which is the other
/// half of Segment Hold Note.
///
/// The first one keeps its head, and every rail after it becomes joints of
/// that head: the gap between one rail's end and the next one's head becomes
/// a joint of its own, so a run that was not quite continuous is joined by a
/// straight piece rather than silently closed up. Eases ride along with the
/// joints that carry them, so a run of segmented pieces put back together
/// travels the path it travelled while it was in pieces.
///
/// Only the degrees and timings each piece already had are used, so nothing
/// moves on the ring; what changes is that there is one note where there
/// were several, which is what the game counts and scores.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>
    /// Returns false only when there is nothing to join: fewer than two hold
    /// notes, or something other than hold notes in the selection.
    ///
    /// It used to refuse as well whenever two of the pieces overlapped in
    /// time, which is easy to do by hand and gave no sign of why the button
    /// had done nothing. A rail is one path through time and cannot be in two
    /// places at once, so an overlapping piece is simply carried on from
    /// where the one before it finished; nothing is ever refused for it.
    /// </summary>
    public bool MergeSelectedRails()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return false;
        if (SelectedTapNote.Count != 0) return false;
        if (SelectedHoldNote.Count < 2) return false;

        List<LanotaHoldNote> Pieces = new List<LanotaHoldNote>(SelectedHoldNote);
        Pieces.Sort((LanotaHoldNote Left, LanotaHoldNote Right) => { return Left.Time.CompareTo(Right.Time); });

        LanotaHoldNote First = Pieces[0];
        LanotaHoldNote Joined = new LanotaHoldNote();
        Joined.Type = 5;
        Joined.Time = First.Time;
        Joined.Degree = First.Degree;
        Joined.Size = First.Size;
        Joined.Sizef = First.Sizef;
        Joined.Critical = First.Critical;
        Joined.Combination = First.Combination;
        Joined.Bpm = First.Bpm;
        Joined.Group = First.Group;
        // The length is not worked out from the pieces' own timings: it is
        // read off the joints once they are all written, further down, so the
        // two can never end up saying different things.
        Joined.Joints = new List<LanotaJoints>();

        // Walked in the chart's own running degrees rather than wrapped ones,
        // so a run that crosses zero, or goes round more than once, keeps
        // going the way it was going instead of doubling back.
        // Every step written is at least RailMinJointGap long and the running
        // total is advanced by exactly what was written, so the joints can
        // never disagree with the length worked out from them and the tuner
        // is never handed a step of no time at all.
        float aTime = First.Time;
        float aDegree = First.Degree;
        for (int i = 0; i < Pieces.Count; ++i)
        {
            LanotaHoldNote Piece = Pieces[i];
            // Joining up to this piece's head, which after the first one may
            // sit away from where the piece before it finished.
            if (i != 0)
            {
                float Head = aDegree + Mathf.DeltaAngle(aDegree, Piece.Degree);
                bool Turns = Mathf.Abs(Head - aDegree) > 0.0001f;
                float Gap = Piece.Time - aTime;
                if (Gap > RailMinJointGap || Turns)
                {
                    float Step = Mathf.Max(Gap, RailMinJointGap);
                    Joined.Joints.Add(new LanotaJoints { dTime = Step, dDegree = Head - aDegree, Cfmi = 0 });
                    aTime += Step;
                    aDegree = Head;
                }
            }

            if (Piece.Joints == null || Piece.Joints.Count == 0)
            {
                // A straight piece is one step that turns nowhere.
                float Step = Mathf.Max(Piece.Duration, RailMinJointGap);
                Joined.Joints.Add(new LanotaJoints { dTime = Step, dDegree = 0, Cfmi = 0 });
                aTime += Step;
                continue;
            }
            foreach (LanotaJoints Joint in Piece.Joints)
            {
                float Step = Mathf.Max(Joint.dTime, RailMinJointGap);
                Joined.Joints.Add(new LanotaJoints { dTime = Step, dDegree = Joint.dDegree, Cfmi = Joint.Cfmi });
                aTime += Step;
                aDegree += Joint.dDegree;
            }
        }
        if (Joined.Joints.Count == 0) return false;
        Joined.Jcount = Joined.Joints.Count;
        // The joints are what say where the rail ends; the length is read off
        // them so the two cannot disagree.
        Joined.Duration = aTime - Joined.Time;

        SelectNothing();
        foreach (LanotaHoldNote Piece in Pieces) DeleteHoldNote(Piece, false);
        AddHoldNote(Joined, false, false, false);
        if (InspectorManager != null) InspectorManager.OnSelectChange();

        LimInspectorManager Inspector = InspectorManager;
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            foreach (LanotaHoldNote Piece in Pieces) DeleteHoldNote(Piece, false);
            AddHoldNote(Joined, false, false, false);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            DeleteHoldNote(Joined, false);
            foreach (LanotaHoldNote Piece in Pieces) AddHoldNote(Piece, false, false, false);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        AddToOperationSaver(OpSave);
        return true;
    }
}
