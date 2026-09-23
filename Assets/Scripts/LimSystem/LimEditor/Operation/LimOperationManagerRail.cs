using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;

/// <summary>
/// Editing rail notes on the ring instead of by arithmetic.
///
///   Ctrl and a drag on the end  moves where the rail finishes
///   J                           puts a joint under the pointer
///   Shift + S                   cuts the rail in two under the pointer
///
/// The joints that appear can then be picked up and moved like any other
/// note, with the mouse or with the arrow keys, which is handled by the drag
/// and nudge files: everything they need from a rail is here.
///
/// How a rail is written down. The head is Time and Degree; Duration is how
/// long the whole thing lasts. Without joints it runs straight out at one
/// degree. With joints, each one carries dTime and dDegree, the step from the
/// joint before it, and the last joint in the list is the end of the rail
/// rather than a bend, which is why it is the one joint that is never drawn.
/// So a rail with a single bend in it has two joints, and the end of a rail
/// that has any joints at all is its last one, whose absolute time has to
/// stay equal to Time plus Duration.
///
/// Every gesture here is one undo entry, and it is written as a whole shape:
/// a rail's length and the list of its joints are taken together, because a
/// cut or a bend changes both at once and putting one back without the other
/// would leave a rail that ends in two places.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>How far from a rail, in degrees, still counts as being on it.</summary>
    private const float RailPickDegrees = 4f;
    /// <summary>The smallest gap allowed between two joints, in seconds.</summary>
    private const float RailMinJointGap = 0.001f;
    /// <summary>The smallest piece a cut or an insert will leave behind.</summary>
    private const float RailMinPiece = 0.01f;
    /// <summary>Steps a joint is sampled in while looking for angleline crossings.</summary>
    private const int RailCrossingSteps = 24;
    /// <summary>How close to the end handle the pointer has to be, in ring units.</summary>
    private const float RailHandleGrabRadius = 0.6f;
    private const float RailHandleGrabFloor = 0.12f;
    /// <summary>Below this the end of a rail is too close to the middle to be caught.</summary>
    private const float RailHandleMinPercent = 20f;

    /// <summary>One joint of one rail, which is what a selected joint is.</summary>
    public class RailJoint
    {
        public LanotaHoldNote Hold;
        public LanotaJoints Joint;
    }

    public readonly List<RailJoint> SelectedJoints = new List<RailJoint>();

    /// <summary>True when anything at all is picked up: notes, rails or joints.</summary>
    public bool HasAnySelection { get { return HasNoteSelection || SelectedJoints.Count > 0; } }

    private LanotaHoldNote _RailUnderPointer;
    private bool _RailPointerValid;
    private float _RailCutTime;

    private LanotaHoldNote _RailHandleRail;
    private bool _RailHandleActive;
    private RailShape _RailHandleOrigin;
    private float _RailHandleGrabTime, _RailHandleGrabDegree;
    private float _RailHandleEndTime, _RailHandleEndDegree;

    /// <summary>True while the end of a rail is being dragged about.</summary>
    public bool IsDraggingRailEnd { get { return _RailHandleActive; } }

    #region Reading a rail

    /// <summary>
    /// The degree a rail is at, at one moment of its own span. Worked out
    /// from the steps themselves rather than from the absolute values kept
    /// alongside them, so the answer is right whether or not anything has
    /// refreshed those this frame. It is the chart's own degree, with the
    /// camera's rotation nowhere in it.
    /// </summary>
    public float RailDegreeAtTime(LanotaHoldNote Hold, float Time)
    {
        if (Hold == null) return 0;
        if (Hold.Joints == null || Hold.Joints.Count == 0) return Hold.Degree;

        float aTime = Hold.Time, aDegree = Hold.Degree;
        for (int i = 0; i < Hold.Joints.Count; ++i)
        {
            LanotaJoints Joint = Hold.Joints[i];
            float NextTime = aTime + Joint.dTime;
            if (Time <= NextTime)
            {
                if (Joint.dTime <= 0) return aDegree + Joint.dDegree;
                float Percent = (Time - aTime) / Joint.dTime;
                return aDegree + Joint.dDegree * EasedShare(Percent, Joint.Cfmi);
            }
            aTime = NextTime;
            aDegree += Joint.dDegree;
        }
        return aDegree;
    }

    /// <summary>
    /// How far along its turn a joint is at a given part of its time. The
    /// rail's own renderer reads the two the same way round, so a joint with
    /// an ease on it bends here exactly as it is drawn.
    /// </summary>
    private float EasedShare(float Percent, int Ease)
    {
        if (TunerManager == null || TunerManager.CameraManager == null) return Percent;
        return TunerManager.CameraManager.CalculateEasedCurve(Percent, Ease);
    }

    /// <summary>Where a rail finishes, in the chart's own degrees.</summary>
    public float RailEndDegree(LanotaHoldNote Hold)
    {
        if (Hold == null) return 0;
        if (Hold.Joints == null || Hold.Joints.Count == 0) return Hold.Degree;
        float Degree = Hold.Degree;
        for (int i = 0; i < Hold.Joints.Count; ++i) Degree += Hold.Joints[i].dDegree;
        return Degree;
    }

    /// <summary>
    /// The absolute values every joint carries alongside its step, brought
    /// up to date. The same sums the tuner does each frame, without the
    /// camera rotation that cancels out of them.
    /// </summary>
    private static void RefreshJointAbsolutes(LanotaHoldNote Hold)
    {
        if (Hold == null || Hold.Joints == null) return;
        float aTime = Hold.Time, aDegree = Hold.Degree;
        for (int i = 0; i < Hold.Joints.Count; ++i)
        {
            aTime += Hold.Joints[i].dTime;
            aDegree += Hold.Joints[i].dDegree;
            Hold.Joints[i].aTime = aTime;
            Hold.Joints[i].aDegree = aDegree;
        }
    }

    /// <summary>
    /// Which step of the rail a moment falls in, and where that step starts.
    /// False when the moment is outside the joints, which happens on a rail
    /// whose joints do not add up to its length.
    /// </summary>
    private static bool FindRailSegment(LanotaHoldNote Hold, float Time, out int Index, out float StartTime, out float StartDegree)
    {
        Index = -1; StartTime = 0; StartDegree = 0;
        if (Hold == null || Hold.Joints == null) return false;
        float aTime = Hold.Time, aDegree = Hold.Degree;
        for (int i = 0; i < Hold.Joints.Count; ++i)
        {
            float NextTime = aTime + Hold.Joints[i].dTime;
            if (Time < NextTime)
            {
                Index = i; StartTime = aTime; StartDegree = aDegree;
                return true;
            }
            aTime = NextTime;
            aDegree += Hold.Joints[i].dDegree;
        }
        return false;
    }

    /// <summary>
    /// Pulls a cut away from the joints on either side of it, by at most a
    /// millisecond, so neither of the pieces it leaves behind lasts no time
    /// at all. Without it a cut asked for exactly on a joint, which is what
    /// the Ctrl snapping makes easy to do, would leave a step of nothing
    /// behind, and the tuner cannot draw one.
    /// </summary>
    private static bool ClampCutToSegment(LanotaHoldNote Hold, ref float Time, out int Index, out float StartTime, out float StartDegree)
    {
        if (!FindRailSegment(Hold, Time, out Index, out StartTime, out StartDegree)) return false;
        float Lowest = StartTime + RailMinJointGap;
        float Highest = StartTime + Hold.Joints[Index].dTime - RailMinJointGap;
        Time = Mathf.Clamp(Time, Lowest, Mathf.Max(Lowest, Highest));
        return true;
    }

    #endregion

    #region The shape of a rail, for undo

    /// <summary>
    /// A rail's length and joints together, which is the smallest thing that
    /// can be put back without leaving the rail ending in two places.
    /// </summary>
    private class RailShape
    {
        public float Duration;
        public readonly List<float> DTime = new List<float>();
        public readonly List<float> DDegree = new List<float>();
        public readonly List<int> Ease = new List<int>();
    }

    private static RailShape CaptureRailShape(LanotaHoldNote Hold)
    {
        RailShape Shape = new RailShape();
        Shape.Duration = Hold.Duration;
        if (Hold.Joints == null) return Shape;
        for (int i = 0; i < Hold.Joints.Count; ++i)
        {
            Shape.DTime.Add(Hold.Joints[i].dTime);
            Shape.DDegree.Add(Hold.Joints[i].dDegree);
            Shape.Ease.Add(Hold.Joints[i].Cfmi);
        }
        return Shape;
    }

    private static bool SameRailShape(RailShape Left, RailShape Right)
    {
        if (Left.Duration != Right.Duration) return false;
        if (Left.DTime.Count != Right.DTime.Count) return false;
        for (int i = 0; i < Left.DTime.Count; ++i)
        {
            if (Left.DTime[i] != Right.DTime[i]) return false;
            if (Left.DDegree[i] != Right.DDegree[i]) return false;
            if (Left.Ease[i] != Right.Ease[i]) return false;
        }
        return true;
    }

    private void RestoreRailShape(LanotaHoldNote Hold, RailShape Shape)
    {
        if (Hold == null) return;
        if (Hold.Joints == null) Hold.Joints = new List<LanotaJoints>();

        while (Hold.Joints.Count > Shape.DTime.Count)
        {
            LanotaJoints Last = Hold.Joints[Hold.Joints.Count - 1];
            DeSelectJoint(Last);
            if (Last.JointGameObject != null) Destroy(Last.JointGameObject);
            Hold.Joints.RemoveAt(Hold.Joints.Count - 1);
        }
        while (Hold.Joints.Count < Shape.DTime.Count) Hold.Joints.Add(new LanotaJoints());

        for (int i = 0; i < Shape.DTime.Count; ++i)
        {
            Hold.Joints[i].dTime = Shape.DTime[i];
            Hold.Joints[i].dDegree = Shape.DDegree[i];
            Hold.Joints[i].Cfmi = Shape.Ease[i];
        }
        Hold.Duration = Shape.Duration;
        Hold.Jcount = Hold.Joints.Count;
        EnsureJointObjects(Hold);
        RefreshJointAbsolutes(Hold);
        RefreshJointInspector();
    }

    /// <summary>
    /// Gives an object to every joint that is drawn, which is all of them but
    /// the last: charts arrive with the end joint left without one, and a
    /// joint added after it would turn that end into a bend with nothing to
    /// show for it.
    /// </summary>
    private void EnsureJointObjects(LanotaHoldNote Hold)
    {
        if (Hold == null || Hold.Joints == null) return;
        if (TunerManager == null || TunerManager.HoldNoteManager == null) return;
        for (int i = 0; i < Hold.Joints.Count - 1; ++i)
        {
            if (Hold.Joints[i].JointGameObject != null) continue;
            TunerManager.HoldNoteManager.InstantiateJointNote(Hold.Joints[i]);
        }
    }

    private void RefreshJointInspector()
    {
        if (InspectorManager == null) return;
        if (InspectorManager.ComponentHoldNote == null) return;
        InspectorManager.ComponentHoldNote.RefreshJointList();
    }

    private void CommitRailShape(LanotaHoldNote Hold, RailShape Before, RailShape After)
    {
        LimInspectorManager Inspector = InspectorManager;
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            RestoreRailShape(Hold, After);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            RestoreRailShape(Hold, Before);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        AddToOperationSaver(OpSave);
    }

    #endregion

    #region Moving one joint

    /// <summary>
    /// Puts one joint at a moment and a degree of the chart, leaving the rest
    /// of the rail exactly where it was: the joint after this one takes up
    /// whatever the move left over. Moving the last joint, which is the end
    /// of the rail, changes the rail's length with it.
    ///
    /// The degree is not wrapped into 0 to 360. A rail is allowed to wind
    /// round the ring more than once, and wrapping a joint's step would fold
    /// the whole turn back on itself.
    /// </summary>
    private static void WriteJointRaw(LanotaHoldNote Hold, LanotaJoints Joint, float Time, float Degree)
    {
        if (Hold == null || Hold.Joints == null || Joint == null) return;
        int Index = Hold.Joints.IndexOf(Joint);
        if (Index < 0) return;

        float PrevTime = Hold.Time, PrevDegree = Hold.Degree;
        for (int i = 0; i < Index; ++i)
        {
            PrevTime += Hold.Joints[i].dTime;
            PrevDegree += Hold.Joints[i].dDegree;
        }
        float OldTime = PrevTime + Joint.dTime;
        float OldDegree = PrevDegree + Joint.dDegree;

        // A joint may not be dragged past its neighbours: a step of no time
        // at all is a rail the tuner cannot draw.
        float Lowest = PrevTime + RailMinJointGap;
        float Highest = float.MaxValue;
        if (Index < Hold.Joints.Count - 1) Highest = OldTime + Hold.Joints[Index + 1].dTime - RailMinJointGap;
        Time = Mathf.Clamp(Time, Lowest, Mathf.Max(Lowest, Highest));

        Joint.dTime = Time - PrevTime;
        Joint.dDegree = Degree - PrevDegree;
        if (Index < Hold.Joints.Count - 1)
        {
            LanotaJoints Next = Hold.Joints[Index + 1];
            Next.dTime = (OldTime + Next.dTime) - Time;
            Next.dDegree = (OldDegree + Next.dDegree) - Degree;
        }
        else Hold.Duration = Time - Hold.Time;
        RefreshJointAbsolutes(Hold);
    }

    #endregion

    #region Selecting joints

    public bool IsJointSelected(LanotaJoints Joint)
    {
        for (int i = 0; i < SelectedJoints.Count; ++i) if (SelectedJoints[i].Joint == Joint) return true;
        return false;
    }

    public RailJoint FindJointByInstanceId(int InstanceId)
    {
        if (TunerManager == null || TunerManager.HoldNoteManager == null) return null;
        if (TunerManager.HoldNoteManager.HoldNote == null) return null;
        foreach (LanotaHoldNote Hold in TunerManager.HoldNoteManager.HoldNote)
        {
            if (Hold.Joints == null) continue;
            foreach (LanotaJoints Joint in Hold.Joints)
            {
                if (Joint.JointGameObject == null) continue;
                if (Joint.InstanceId != InstanceId) continue;
                return new RailJoint { Hold = Hold, Joint = Joint };
            }
        }
        return null;
    }

    public void SelectJoint(LanotaHoldNote Hold, LanotaJoints Joint, bool MultiSelect = false)
    {
        if (Hold == null || Joint == null) return;
        if (IsJointSelected(Joint))
        {
            if (!Input.GetKey(KeyCode.LeftControl)) SelectNothing();
            else DeSelectJoint(Joint);
            if (InspectorManager != null) InspectorManager.OnSelectChange();
            return;
        }
        if (!Input.GetKey(KeyCode.LeftControl) && !MultiSelect) SelectNothing();
        Joint.OnSelect = true;
        SelectedJoints.Add(new RailJoint { Hold = Hold, Joint = Joint });
        if (InspectorManager != null) InspectorManager.OnSelectChange();
        DeSelectAllMotions();
    }

    public void DeSelectJoint(LanotaJoints Joint)
    {
        for (int i = 0; i < SelectedJoints.Count; ++i)
        {
            if (SelectedJoints[i].Joint != Joint) continue;
            Joint.OnSelect = false;
            SelectedJoints.RemoveAt(i);
            return;
        }
    }

    public void DeSelectAllJoints()
    {
        foreach (RailJoint Selected in SelectedJoints) Selected.Joint.OnSelect = false;
        SelectedJoints.Clear();
    }

    /// <summary>
    /// Takes the selected joints out of their rails, each rail as one undo
    /// entry, leaving the rest of every rail where it was.
    /// </summary>
    public void DeleteSelectedJoints()
    {
        if (SelectedJoints.Count == 0) return;
        List<RailJoint> Doomed = new List<RailJoint>(SelectedJoints);
        DeSelectAllJoints();

        List<LanotaHoldNote> Rails = new List<LanotaHoldNote>();
        List<RailShape> Before = new List<RailShape>();
        foreach (RailJoint Selected in Doomed)
        {
            if (Rails.Contains(Selected.Hold)) continue;
            Rails.Add(Selected.Hold);
            Before.Add(CaptureRailShape(Selected.Hold));
        }

        foreach (RailJoint Selected in Doomed)
        {
            LanotaHoldNote Hold = Selected.Hold;
            if (Hold.Joints == null) continue;
            int Index = Hold.Joints.IndexOf(Selected.Joint);
            // The last joint is the rail's end; taking it away would leave
            // the rail finishing nowhere, so it is not deleted with this.
            if (Index < 0 || Index >= Hold.Joints.Count - 1) continue;
            LanotaJoints Next = Hold.Joints[Index + 1];
            Next.dTime += Selected.Joint.dTime;
            Next.dDegree += Selected.Joint.dDegree;
            if (Selected.Joint.JointGameObject != null) Destroy(Selected.Joint.JointGameObject);
            Hold.Joints.RemoveAt(Index);
            Hold.Jcount = Hold.Joints.Count;
        }

        for (int i = 0; i < Rails.Count; ++i)
        {
            RefreshJointAbsolutes(Rails[i]);
            RailShape After = CaptureRailShape(Rails[i]);
            if (SameRailShape(Before[i], After)) continue;
            CommitRailShape(Rails[i], Before[i], After);
        }
        RefreshJointInspector();
    }

    #endregion

    #region Where the pointer is on a rail

    /// <summary>
    /// Works out once a frame which rail the pointer is resting on and where
    /// along it, so the guide line, the J key and the S key all agree about
    /// the same spot.
    ///
    /// A rail is about six degrees wide wherever it is on the ring: it is
    /// drawn a hundredth of its distance from the middle across, and that
    /// ratio does not change as it comes in. So one angle serves as the
    /// whole test, near and far alike.
    /// </summary>
    private void UpdateRailPointer()
    {
        _RailUnderPointer = null;
        _RailPointerValid = false;
        if (TunerManager == null || !TunerManager.isInitialized) return;
        if (TunerManager.HoldNoteManager == null || TunerManager.HoldNoteManager.HoldNote == null) return;
        if (TunerManager.CameraManager == null) return;
        if (TunerWindowRect == null || !LimMousePosition.IsMouseOverWindow(TunerWindowRect)) return;

        float PointerTime, ScreenDegree;
        if (!LimTunerCoordinate.TryGetChartPointAtMouse(TunerWindowRect, TunerCamera, TunerManager, out PointerTime, out ScreenDegree)) return;
        float PointerDegree = ScreenDegree - TunerManager.CameraManager.CurrentRotation;

        LanotaHoldNote Best = null;
        float BestDelta = RailPickDegrees;
        foreach (LanotaHoldNote Hold in TunerManager.HoldNoteManager.HoldNote)
        {
            if (!Hold.shouldUpdate) continue;
            if (PointerTime < Hold.Time || PointerTime > Hold.Time + Hold.Duration) continue;
            float Delta = Mathf.Abs(Mathf.DeltaAngle(RailDegreeAtTime(Hold, PointerTime), PointerDegree));
            if (Delta >= BestDelta) continue;
            BestDelta = Delta;
            Best = Hold;
        }
        if (Best == null) return;

        _RailUnderPointer = Best;
        _RailPointerValid = true;
        _RailCutTime = SnapRailCutTime(Best, PointerTime);
    }

    /// <summary>
    /// With Ctrl held, the cut is pulled onto something the rail actually
    /// crosses: a beatline, or an angleline the rail passes through on its
    /// way round. Without it the cut lands wherever the pointer is.
    /// </summary>
    private float SnapRailCutTime(LanotaHoldNote Hold, float Time)
    {
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return Time;

        List<float> Candidates = new List<float>();
        if (InspectorManager != null && InspectorManager.ComponentBpm != null && InspectorManager.ComponentBpm.BeatlineTimes != null)
            Candidates.AddRange(InspectorManager.ComponentBpm.BeatlineTimes);
        AddAnglelineCrossings(Hold, Candidates);

        float Lowest = Hold.Time + RailMinPiece;
        float Highest = Hold.Time + Hold.Duration - RailMinPiece;
        float Best = Time, BestDelta = float.MaxValue;
        foreach (float Candidate in Candidates)
        {
            if (Candidate < Lowest || Candidate > Highest) continue;
            float Delta = Mathf.Abs(Candidate - Time);
            if (Delta >= BestDelta) continue;
            BestDelta = Delta;
            Best = Candidate;
        }
        return Best;
    }

    /// <summary>
    /// When a rail crosses the anglelines. A joint is walked in short steps
    /// and each step checked for a line lying between its two ends, which
    /// works whatever ease the joint carries and however many times the rail
    /// goes round.
    /// </summary>
    private void AddAnglelineCrossings(LanotaHoldNote Hold, List<float> Times)
    {
        if (Hold.Joints == null || Hold.Joints.Count == 0) return;
        LimAngleLineManager Angleline = LimClickToCreateManager.SharedAnglelineManager;
        if (Angleline == null || !Angleline.Enable) return;
        List<float> Lines = Angleline.AnglelineAngles;
        if (Lines == null || Lines.Count == 0) return;

        float aTime = Hold.Time, aDegree = Hold.Degree;
        for (int i = 0; i < Hold.Joints.Count; ++i)
        {
            LanotaJoints Joint = Hold.Joints[i];
            if (Joint.dTime > 0 && Mathf.Abs(Joint.dDegree) > 0.0001f)
            {
                for (int Step = 1; Step <= RailCrossingSteps; ++Step)
                {
                    float FromPercent = (Step - 1f) / RailCrossingSteps;
                    float ToPercent = (float)Step / RailCrossingSteps;
                    float FromTime = aTime + Joint.dTime * FromPercent;
                    float ToTime = aTime + Joint.dTime * ToPercent;
                    float FromDegree = aDegree + Joint.dDegree * EasedShare(FromPercent, Joint.Cfmi);
                    float ToDegree = aDegree + Joint.dDegree * EasedShare(ToPercent, Joint.Cfmi);
                    for (int l = 0; l < Lines.Count; ++l) AddCrossing(Times, FromTime, ToTime, FromDegree, ToDegree, Lines[l]);
                }
            }
            aTime += Joint.dTime;
            aDegree += Joint.dDegree;
        }
    }

    private static void AddCrossing(List<float> Times, float FromTime, float ToTime, float FromDegree, float ToDegree, float Line)
    {
        float Low = Mathf.Min(FromDegree, ToDegree);
        float High = Mathf.Max(FromDegree, ToDegree);
        float Sweep = ToDegree - FromDegree;
        if (Mathf.Abs(Sweep) < 0.0001f) return;

        // An angleline is not one degree but every degree a turn of the ring
        // away from it, so a rail that winds round meets the same line again.
        float Target = Line + 360f * Mathf.Ceil((Low - Line) / 360f);
        int Guard = 0;
        while (Target <= High && Guard++ < 64)
        {
            Times.Add(FromTime + (ToTime - FromTime) * (Target - FromDegree) / Sweep);
            Target += 360f;
        }
    }

    #endregion

    #region J : a joint under the pointer

    private void DetectRailJointInsert()
    {
        if (!Input.GetKeyDown(KeyCode.J)) return;
        if (IsTypingInTextField()) return;
        if (_PasteActive || IsNoteDragInProgress || _RailHandleActive) return;
        if (!_RailPointerValid || _RailUnderPointer == null) return;
        InsertRailJoint(_RailUnderPointer, _RailCutTime);
    }

    /// <summary>
    /// Puts a joint into a rail without changing the shape of it: the rail
    /// looks exactly the same until the joint is moved. With Ctrl held the
    /// joint lands on the beatline or angleline the cut line was showing.
    ///
    /// A rail that had no joints gets two, because the end of a rail with
    /// joints is itself a joint: one where the pointer was, and one carrying
    /// the end it already had.
    ///
    /// A joint carrying an ease is cut at the degree the ease had reached,
    /// which keeps a straight run exactly straight; both halves keep the
    /// ease, so the two together bend a little differently from the one they
    /// came from. That is what setting JCount by hand has always done.
    /// </summary>
    public bool InsertRailJoint(LanotaHoldNote Hold, float Time)
    {
        if (Hold == null) return false;
        float Start = Hold.Time, End = Hold.Time + Hold.Duration;
        if (Time - Start < RailMinPiece || End - Time < RailMinPiece) return false;

        RailShape Before = CaptureRailShape(Hold);
        if (Hold.Joints == null) Hold.Joints = new List<LanotaJoints>();

        if (Hold.Joints.Count == 0)
        {
            Hold.Joints.Add(new LanotaJoints { dTime = Time - Start, dDegree = 0, Cfmi = 0 });
            Hold.Joints.Add(new LanotaJoints { dTime = End - Time, dDegree = 0, Cfmi = 0 });
        }
        else
        {
            int Index; float SegmentStart, SegmentDegree;
            if (!ClampCutToSegment(Hold, ref Time, out Index, out SegmentStart, out SegmentDegree)) return false;
            LanotaJoints Segment = Hold.Joints[Index];
            float Taken = Time - SegmentStart;

            float Share = Segment.dTime <= 0 ? 0 : EasedShare(Taken / Segment.dTime, Segment.Cfmi);
            float TakenDegree = Segment.dDegree * Share;
            Hold.Joints.Insert(Index, new LanotaJoints { dTime = Taken, dDegree = TakenDegree, Cfmi = Segment.Cfmi });
            Segment.dTime -= Taken;
            Segment.dDegree -= TakenDegree;
        }

        Hold.Jcount = Hold.Joints.Count;
        EnsureJointObjects(Hold);
        RefreshJointAbsolutes(Hold);
        RefreshJointInspector();

        RailShape After = CaptureRailShape(Hold);
        CommitRailShape(Hold, Before, After);
        if (InspectorManager != null) InspectorManager.OnSelectChange();
        return true;
    }

    #endregion

    #region S : cutting a rail in two

    /// <summary>
    /// Shift and S, not S on its own: Ctrl + S saves the project and S on the
    /// timeline splits a motion, and a cut wanted on a beatline is asked for
    /// with Ctrl held, which would have made the save shortcut cut instead.
    /// Shift is free in the tuner and leaves both of those alone.
    /// </summary>
    private void DetectRailSplit()
    {
        if (!Input.GetKeyDown(KeyCode.S)) return;
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
        if (IsTypingInTextField()) return;
        if (_PasteActive || IsNoteDragInProgress || _RailHandleActive) return;
        if (!_RailPointerValid || _RailUnderPointer == null) return;
        SplitRail(_RailUnderPointer, _RailCutTime);
    }

    /// <summary>
    /// Cuts a rail where the pointer is and leaves two rails that together
    /// do what the one did: the first keeps the head it had and stops at the
    /// cut, the second starts there with a head of its own, at the degree the
    /// rail had reached, and carries on to the end. Size, kind and everything
    /// else are copied, so a rail of one second and size two cut three tenths
    /// in leaves seven tenths of size two behind it.
    /// </summary>
    public bool SplitRail(LanotaHoldNote Hold, float Time)
    {
        if (Hold == null) return false;
        float Start = Hold.Time, End = Hold.Time + Hold.Duration;
        if (Time - Start < RailMinPiece || End - Time < RailMinPiece) return false;

        // Worked out before anything is built from it: the cut may be moved
        // a shade to keep clear of a joint, and the tail starts where the cut
        // really lands.
        bool HasJoints = Hold.Joints != null && Hold.Joints.Count != 0;
        int Index = -1;
        float SegmentStart = 0, SegmentDegree = 0;
        if (HasJoints && !ClampCutToSegment(Hold, ref Time, out Index, out SegmentStart, out SegmentDegree)) return false;

        RailShape Before = CaptureRailShape(Hold);

        LanotaHoldNote Tail = new LanotaHoldNote();
        Tail.Type = 5;
        Tail.Time = Time;
        Tail.Duration = End - Time;
        Tail.Degree = LimMathUtil.NormalizeDegree(RailDegreeAtTime(Hold, Time));
        Tail.Size = Hold.Size;
        Tail.Sizef = Hold.Sizef;
        Tail.Critical = Hold.Critical;
        Tail.Combination = Hold.Combination;
        Tail.Bpm = Hold.Bpm;
        Tail.Group = Hold.Group;
        Tail.Joints = new List<LanotaJoints>();

        if (HasJoints)
        {
            LanotaJoints Segment = Hold.Joints[Index];
            float Taken = Time - SegmentStart;
            float Share = Segment.dTime <= 0 ? 0 : EasedShare(Taken / Segment.dTime, Segment.Cfmi);
            float TakenDegree = Segment.dDegree * Share;

            // The rest of the segment the cut fell in becomes the tail's
            // first step, and every joint past it moves across unchanged.
            Tail.Joints.Add(new LanotaJoints { dTime = Segment.dTime - Taken, dDegree = Segment.dDegree - TakenDegree, Cfmi = Segment.Cfmi });
            for (int i = Index + 1; i < Hold.Joints.Count; ++i)
                Tail.Joints.Add(new LanotaJoints { dTime = Hold.Joints[i].dTime, dDegree = Hold.Joints[i].dDegree, Cfmi = Hold.Joints[i].Cfmi });

            // What is left of the head stops at the cut, which becomes its
            // own last joint: the end of a rail with joints is always one.
            for (int i = Hold.Joints.Count - 1; i > Index; --i)
            {
                LanotaJoints Gone = Hold.Joints[i];
                DeSelectJoint(Gone);
                if (Gone.JointGameObject != null) Destroy(Gone.JointGameObject);
                Hold.Joints.RemoveAt(i);
            }
            Segment.dTime = Taken;
            Segment.dDegree = TakenDegree;
        }

        if (Tail.Joints.Count == 0) Tail.Joints = null;
        Tail.Jcount = Tail.Joints == null ? 0 : Tail.Joints.Count;

        Hold.Duration = Time - Start;
        Hold.Jcount = Hold.Joints == null ? 0 : Hold.Joints.Count;
        EnsureJointObjects(Hold);
        RefreshJointAbsolutes(Hold);
        RailShape After = CaptureRailShape(Hold);

        AddHoldNote(Tail, false, false, false);
        RefreshJointInspector();
        if (InspectorManager != null) InspectorManager.OnSelectChange();

        LimInspectorManager Inspector = InspectorManager;
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            RestoreRailShape(Hold, After);
            AddHoldNote(Tail, false, false, false);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            DeleteHoldNote(Tail, false);
            RestoreRailShape(Hold, Before);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        AddToOperationSaver(OpSave);
        return true;
    }

    #endregion

    #region Ctrl and a drag on the end of a rail

    /// <summary>
    /// Where the end of a rail sits on the ring, and how big it is drawn
    /// there. False when it is behind the judgement line or too close to the
    /// middle to be worth showing.
    /// </summary>
    private bool TryRailEndPoint(LanotaHoldNote Hold, out Vector3 Position, out float Scale)
    {
        Position = Vector3.zero; Scale = 0;
        if (Hold == null || TunerManager == null || TunerManager.CameraManager == null) return false;
        float EndTime = Hold.Time + Hold.Duration;
        if (EndTime < TunerManager.ChartTime) return false;

        float Percent = LimTunerCoordinate.EasedPercent(LimTunerCoordinate.TimeToMovePercent(EndTime, TunerManager));
        if (Percent < RailHandleMinPercent) return false;
        float Degree = RailEndDegree(Hold) + TunerManager.CameraManager.CurrentRotation;
        Position = new Vector3(-Percent / 10 * Mathf.Sin(Degree * Mathf.Deg2Rad), 0, -Percent / 10 * Mathf.Cos(Degree * Mathf.Deg2Rad));
        Scale = Percent / 100;
        return true;
    }

    private LanotaHoldNote RailEndUnderPointer()
    {
        if (TunerManager == null || TunerManager.HoldNoteManager == null) return null;
        if (TunerManager.HoldNoteManager.HoldNote == null) return null;

        Vector3 World = LimTunerCoordinate.TunerScreenToWorld(LimTunerCoordinate.MouseToTunerScreen(TunerWindowRect), TunerCamera);
        LanotaHoldNote Best = null;
        float BestDistance = float.MaxValue;
        foreach (LanotaHoldNote Hold in TunerManager.HoldNoteManager.HoldNote)
        {
            if (!Hold.shouldUpdate) continue;
            Vector3 Position; float Scale;
            if (!TryRailEndPoint(Hold, out Position, out Scale)) continue;
            float Distance = Vector3.Distance(World, Position);
            if (Distance > RailHandleGrabRadius * Scale + RailHandleGrabFloor) continue;
            if (Distance >= BestDistance) continue;
            BestDistance = Distance;
            Best = Hold;
        }
        return Best;
    }

    /// <summary>
    /// Ctrl and the left button on the little handle at a rail's end pull
    /// that end about: where it finishes and which way the rail leans, in one
    /// gesture, snapping the same way a dragged note does. Shift ignores the
    /// snapping, and may be pressed and let go at any point of the drag.
    ///
    /// Run before the tuner's own Ctrl-drag and before the note drag, both of
    /// which stand aside while this is going on. It works with Click To
    /// Create switched on as well, since that tool already refuses to place
    /// anything while Ctrl is held, and rails are mostly edited with it on.
    /// </summary>
    private void DetectRailEndDrag()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;

        if (Input.GetMouseButtonUp(0)) { FinishRailEndDrag(); return; }
        if (!Input.GetMouseButton(0))
        {
            _RailHandleActive = false;
            _RailHandleRail = null;
            return;
        }
        if (_RailHandleActive) { UpdateRailEndDrag(); return; }
        if (!Input.GetMouseButtonDown(0)) return;
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
        if (_PasteActive || IsNoteDragInProgress || _TunerPanActive) return;
        if (TunerWindowRect == null || !LimMousePosition.IsMouseOverWindow(TunerWindowRect)) return;

        LanotaHoldNote Rail = RailEndUnderPointer();
        if (Rail == null) return;
        BeginRailEndDrag(Rail);
    }

    private void BeginRailEndDrag(LanotaHoldNote Rail)
    {
        float GrabTime, GrabDegree;
        if (!LimTunerCoordinate.TryGetChartPointAtMouse(TunerWindowRect, TunerCamera, TunerManager, out GrabTime, out GrabDegree)) return;

        _RailHandleRail = Rail;
        _RailHandleOrigin = CaptureRailShape(Rail);
        _RailHandleGrabTime = GrabTime;
        _RailHandleGrabDegree = GrabDegree;
        _RailHandleEndTime = Rail.Time + Rail.Duration;
        _RailHandleEndDegree = RailEndDegree(Rail);
        _RailHandleActive = true;
    }

    private void UpdateRailEndDrag()
    {
        if (_RailHandleRail == null) return;
        float PointerTime, PointerDegree;
        if (!LimTunerCoordinate.TryGetChartPointAtMouse(TunerWindowRect, TunerCamera, TunerManager, out PointerTime, out PointerDegree)) return;

        // Both in the chart's own terms: the pointer's travel is a difference
        // between two on-screen degrees, so the camera's rotation cancels out
        // of it and never has to be taken off.
        float Time = _RailHandleEndTime + (PointerTime - _RailHandleGrabTime);
        // Read every frame rather than caught as the drag began: the end of a
        // rail is usually put roughly where it goes and then freed to be
        // nudged off the grid, and reaching for Shift beforehand means
        // letting go and starting again.
        bool FreeMove = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float Degree = _RailHandleEndDegree + Mathf.DeltaAngle(_RailHandleGrabDegree, PointerDegree);
        if (!FreeMove)
        {
            if (LimClickToCreateManager.SnapToBeatline && InspectorManager != null && InspectorManager.ComponentBpm != null
                && InspectorManager.ComponentBpm.BeatlineTimes != null && InspectorManager.ComponentBpm.BeatlineTimes.Count != 0)
                Time = FindNearestBeatlineByTime(Time);
            LimAngleLineManager Angleline = LimClickToCreateManager.SharedAnglelineManager;
            if (LimClickToCreateManager.SnapToAngleline && Angleline != null && Angleline.Enable)
                Degree = Angleline.FindNearestAnglelineByRelativeDegree(Degree);
        }
        ApplyRailEnd(_RailHandleRail, Time, Degree);
        if (InspectorManager != null) InspectorManager.OnSelectChange();
    }

    /// <summary>
    /// The end of a rail moved from outside, which is how the drag that
    /// places one draws its length and its lean at the same time. The click
    /// that made the rail is already one undo entry and takes the whole thing
    /// away, shape included, so nothing more is recorded here.
    /// </summary>
    public void StretchRailEnd(LanotaHoldNote Hold, float Time, float Degree)
    {
        if (Hold == null) return;
        ApplyRailEnd(Hold, Time, Degree);
    }

    /// <summary>
    /// Puts a rail's end at a moment and a degree. A rail with no joints has
    /// no end to bend, so it grows one the moment it is actually leant to one
    /// side; pulled straight in or out it stays the plain rail it was.
    /// </summary>
    private void ApplyRailEnd(LanotaHoldNote Hold, float Time, float Degree)
    {
        float Lowest = Hold.Time + RailMinPiece;
        if (Hold.Joints != null && Hold.Joints.Count >= 2)
        {
            RefreshJointAbsolutes(Hold);
            Lowest = Mathf.Max(Lowest, Hold.Joints[Hold.Joints.Count - 2].aTime + RailMinJointGap);
        }
        Time = Mathf.Max(Time, Lowest);

        if (Hold.Joints == null || Hold.Joints.Count == 0)
        {
            if (Mathf.Abs(Degree - Hold.Degree) < 0.0001f)
            {
                Hold.Duration = Time - Hold.Time;
                return;
            }
            AddJointNoteFromVoid(Hold);
        }

        int Last = Hold.Joints.Count - 1;
        float PrevTime = Hold.Time, PrevDegree = Hold.Degree;
        for (int i = 0; i < Last; ++i)
        {
            PrevTime += Hold.Joints[i].dTime;
            PrevDegree += Hold.Joints[i].dDegree;
        }
        Hold.Joints[Last].dTime = Time - PrevTime;
        Hold.Joints[Last].dDegree = Degree - PrevDegree;
        Hold.Duration = Time - Hold.Time;
        Hold.Jcount = Hold.Joints.Count;
        RefreshJointAbsolutes(Hold);
    }

    private void FinishRailEndDrag()
    {
        if (!_RailHandleActive)
        {
            _RailHandleRail = null;
            return;
        }
        LanotaHoldNote Rail = _RailHandleRail;
        RailShape Before = _RailHandleOrigin;
        _RailHandleActive = false;
        _RailHandleRail = null;
        _RailHandleOrigin = null;
        // The button coming up ended a drag, not a click on whatever is
        // underneath.
        _DragConsumedClick = true;

        if (Rail == null || Before == null) return;
        RailShape After = CaptureRailShape(Rail);
        if (SameRailShape(Before, After)) return;
        RefreshJointInspector();
        CommitRailShape(Rail, Before, After);
    }

    #endregion
}
