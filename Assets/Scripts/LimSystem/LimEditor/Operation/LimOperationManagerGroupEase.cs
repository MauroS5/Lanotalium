using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bends a run of notes along one of the editor's ease curves.
///
/// The first and the last note of the selection stay exactly where they are
/// and everything between them is moved onto the curve that runs from one to
/// the other, spaced by its own timing. Ease 0 is a straight line, so a run
/// that was bent is straightened again by asking for 0, and the numbers are
/// the same ones a motion's Ease field takes, evaluated with the same
/// function the camera uses, so a curve reads the same wherever it is asked
/// for.
///
/// The sweep from the first note to the last is added up note by note rather
/// than measured between the two ends. Degrees are kept inside 0 to 360, so a
/// run that travels three quarters of the way round the ring reads as a
/// quarter of a turn the other way if only its ends are compared, and
/// straightening it would fold the whole pattern over.
/// </summary>
public partial class LimOperationManager
{
    private const int GroupEaseHighest = 12;

    /// <summary>The ease numbers this accepts, which are the motion ones.</summary>
    public static bool IsGroupEaseInRange(int Mode)
    {
        return Mode >= 0 && Mode <= GroupEaseHighest;
    }

    /// <summary>
    /// Returns false when there is nothing to bend: fewer than three notes,
    /// or a selection that falls at one moment and so has no run to spread.
    /// </summary>
    public bool ApplyGroupEase(int Mode)
    {
        if (TunerManager == null || !TunerManager.isInitialized) return false;
        if (TunerManager.CameraManager == null) return false;
        if (!IsGroupEaseInRange(Mode)) return false;

        List<DragItem> Items = CollectSelectionByTime();
        if (Items.Count < 3) return false;

        DragItem First = Items[0];
        float Span = Items[Items.Count - 1].OriginTime - First.OriginTime;
        if (Mathf.Abs(Span) < 0.0001f) return false;

        float Sweep = 0;
        for (int i = 1; i < Items.Count; ++i)
        {
            Sweep += Mathf.DeltaAngle(Items[i - 1].OriginDegree, Items[i].OriginDegree);
        }

        List<float> Eased = new List<float>();
        for (int i = 0; i < Items.Count; ++i)
        {
            float Percent = (Items[i].OriginTime - First.OriginTime) / Span;
            Eased.Add(First.OriginDegree + Sweep * TunerManager.CameraManager.CalculateEasedCurve(Percent, Mode));
        }

        LimInspectorManager Inspector = InspectorManager;
        WriteGroupEase(Items, Eased);
        if (Inspector != null) Inspector.OnSelectChange();

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            WriteGroupEase(Items, Eased);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            for (int i = 0; i < Items.Count; ++i) WriteRaw(Items[i], Items[i].OriginTime, Items[i].OriginDegree);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        AddToOperationSaver(OpSave);
        return true;
    }

    private static void WriteGroupEase(List<DragItem> Items, List<float> Degrees)
    {
        for (int i = 0; i < Items.Count; ++i) WriteRaw(Items[i], Items[i].OriginTime, Degrees[i]);
    }

    /// <summary>
    /// Everything selected, notes and holds together, in the order it is
    /// played, with where each one started recorded for the undo entry.
    /// </summary>
    private List<DragItem> CollectSelectionByTime()
    {
        List<DragItem> Items = new List<DragItem>();
        foreach (Lanotalium.Chart.LanotaTapNote Tap in SelectedTapNote)
        {
            Items.Add(new DragItem { Tap = Tap, OriginTime = Tap.Time, OriginDegree = Tap.Degree });
        }
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in SelectedHoldNote)
        {
            Items.Add(new DragItem { Hold = Hold, OriginTime = Hold.Time, OriginDegree = Hold.Degree });
        }
        Items.Sort((DragItem Left, DragItem Right) => { return Left.OriginTime.CompareTo(Right.OriginTime); });
        return Items;
    }
}
