using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;

/// <summary>
/// Cutting one rail into a given number of rails that between them travel the
/// path the original travelled. The Creator's Segment Rail Note row asks for
/// it; the work is here, where the rest of the rail editing lives.
///
/// The pieces are equal in time, and each one is handed the degrees the
/// original was at when it began and ended. That is what keeps an ease: a
/// rail turning ninety degrees over five seconds with an ease on it does not
/// turn eighteen degrees a second, and slicing the turn evenly instead of
/// reading it off the curve would leave ten straight pieces that do not go
/// where the rail went.
///
/// Inside a piece the curve is read at a few points as well, so a piece is
/// only drawn straight when the original really was straight along it. That
/// makes the result follow the curve however few pieces are asked for,
/// instead of relying on the pieces being short enough not to notice.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>The most pieces one rail will be cut into.</summary>
    public const int RailSegmentMost = 64;
    /// <summary>How many points of the original each piece is read at.</summary>
    private const int RailSegmentSteps = 4;
    /// <summary>
    /// How far, in degrees, a piece may stray from the straight line joining
    /// its ends before it is given joints of its own. A rail is about six
    /// degrees wide, so half a degree is well under what can be seen, and a
    /// piece that passes stays a plain two-point rail instead of carrying
    /// four joints for nothing.
    /// </summary>
    private const float RailSegmentStraight = 0.5f;
    /// <summary>Under this a piece does not turn at all and needs no joints.</summary>
    private const float RailSegmentStill = 0.01f;

    public static bool IsRailSegmentCountInRange(int Segments)
    {
        return Segments >= 2 && Segments <= RailSegmentMost;
    }

    /// <summary>
    /// Returns false when there is nothing to do: no rail, too few pieces
    /// asked for, or pieces so short that they would not be rails at all.
    /// </summary>
    public bool SegmentRail(LanotaHoldNote Hold, int Segments)
    {
        if (Hold == null) return false;
        if (!IsRailSegmentCountInRange(Segments)) return false;
        if (TunerManager == null || !TunerManager.isInitialized) return false;

        float Start = Hold.Time;
        float End = Hold.Time + Hold.Duration;
        float Slice = Hold.Duration / Segments;
        if (Slice < RailMinPiece) return false;

        List<LanotaHoldNote> Pieces = new List<LanotaHoldNote>();
        for (int i = 0; i < Segments; ++i)
        {
            float From = Start + Slice * i;
            // The last piece is closed on the rail's own end rather than on
            // the sum of the slices, which would drift by a fraction.
            float To = i == Segments - 1 ? End : Start + Slice * (i + 1);
            Pieces.Add(BuildRailPiece(Hold, From, To));
        }

        SelectNothing();
        DeleteHoldNote(Hold, false);
        foreach (LanotaHoldNote Piece in Pieces) AddHoldNote(Piece, false, false, false);
        if (InspectorManager != null) InspectorManager.OnSelectChange();

        LimInspectorManager Inspector = InspectorManager;
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            DeleteHoldNote(Hold, false);
            foreach (LanotaHoldNote Piece in Pieces) AddHoldNote(Piece, false, false, false);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            foreach (LanotaHoldNote Piece in Pieces) DeleteHoldNote(Piece, false);
            AddHoldNote(Hold, false, false, false);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        AddToOperationSaver(OpSave);
        return true;
    }

    /// <summary>
    /// One piece of a rail: a rail of its own, starting where the original
    /// was at that moment and following it to the next.
    /// </summary>
    private LanotaHoldNote BuildRailPiece(LanotaHoldNote Hold, float From, float To)
    {
        LanotaHoldNote Piece = new LanotaHoldNote();
        Piece.Type = 5;
        Piece.Time = From;
        Piece.Duration = To - From;
        Piece.Size = Hold.Size;
        Piece.Sizef = Hold.Sizef;
        Piece.Critical = Hold.Critical;
        Piece.Combination = Hold.Combination;
        Piece.Bpm = Hold.Bpm;
        Piece.Group = Hold.Group;

        // Read off the original at each step. The degrees are the chart's
        // own, running on from one step to the next rather than being wrapped
        // round, so a piece of a rail that winds keeps winding.
        float[] Degrees = new float[RailSegmentSteps + 1];
        for (int i = 0; i <= RailSegmentSteps; ++i)
        {
            float Moment = From + (To - From) * i / RailSegmentSteps;
            Degrees[i] = RailDegreeAtTime(Hold, Moment);
        }
        Piece.Degree = LimMathUtil.NormalizeDegree(Degrees[0]);

        Piece.Joints = new List<LanotaJoints>();
        if (IsPieceStraight(Degrees))
        {
            Piece.Joints.Add(new LanotaJoints { dTime = To - From, dDegree = Degrees[RailSegmentSteps] - Degrees[0], Cfmi = 0 });
        }
        else
        {
            float Step = (To - From) / RailSegmentSteps;
            for (int i = 1; i <= RailSegmentSteps; ++i)
                Piece.Joints.Add(new LanotaJoints { dTime = Step, dDegree = Degrees[i] - Degrees[i - 1], Cfmi = 0 });
        }
        // A piece that goes nowhere is a plain rail, with no joints at all,
        // which is how one would have been written by hand.
        if (Piece.Joints.Count == 1 && Mathf.Abs(Piece.Joints[0].dDegree) < RailSegmentStill)
        {
            Piece.Joints = null;
            Piece.Jcount = 0;
        }
        else Piece.Jcount = Piece.Joints.Count;
        return Piece;
    }

    /// <summary>
    /// True when every point read between the two ends sits on the straight
    /// line joining them, which is the case whenever the original was linear
    /// along this piece.
    /// </summary>
    private static bool IsPieceStraight(float[] Degrees)
    {
        float First = Degrees[0];
        float Last = Degrees[Degrees.Length - 1];
        for (int i = 1; i < Degrees.Length - 1; ++i)
        {
            float Straight = First + (Last - First) * i / (Degrees.Length - 1);
            if (Mathf.Abs(Degrees[i] - Straight) > RailSegmentStraight) return false;
        }
        return true;
    }
}
