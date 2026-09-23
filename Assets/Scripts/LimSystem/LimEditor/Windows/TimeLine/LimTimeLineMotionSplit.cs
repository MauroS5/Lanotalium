using Lanotalium.Chart;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Splitting a motion in two with the S key.
///
/// Point at a motion on the timeline and press S: it becomes two motions,
/// cut where the pointer was. The first keeps its start, the second begins
/// exactly at the cut, and everything else about them is identical, so the
/// pair does what the single motion did.
///
/// One motion at a time: with two or more selected the key does nothing,
/// because there would be no saying which one was meant.
/// </summary>
public partial class LimTimeLineManager
{
    private void DetectMotionSplit()
    {
        if (!Input.GetKeyDown(KeyCode.S)) return;
        if (IsMotionDragInProgress || IsMotionPasting) return;
        if (IsTypingInTextField()) return;
        // Ctrl+S saves the project; that shortcut stays as it was.
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return;
        if (OperationManager.SelectedMotions.Count >= 2) return;

        LanotaCameraBase Motion = MotionUnderPointer();
        if (Motion == null) return;
        SplitMotion(Motion, PointerTiming());
    }

    private static bool IsTypingInTextField()
    {
        EventSystem Events = EventSystem.current;
        if (Events == null || Events.currentSelectedGameObject == null) return false;
        return Events.currentSelectedGameObject.GetComponent<InputField>() != null;
    }

    private void SplitMotion(LanotaCameraBase Motion, float CutTime)
    {
        float Start = Motion.Time;
        float End = Motion.Time + Motion.Duration;
        // Too close to either end and one half would be nothing at all.
        if (CutTime - Start < MotionMinDuration || End - CutTime < MotionMinDuration) return;

        float OriginDuration = Motion.Duration;
        LanotaCameraBase Tail = CopyMotion(Motion, CutTime, End - CutTime);
        if (Tail == null) return;

        Motion.Duration = CutTime - Start;
        if (!AddMotion(Tail))
        {
            // The add reports why it refused; leave the motion as it was.
            Motion.Duration = OriginDuration;
            RebuildTimeLineAfterMotionDrag();
            return;
        }

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            Motion.Duration = CutTime - Start;
            AddMotion(Tail);
            RebuildTimeLineAfterMotionDrag();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            DeleteMotion(Tail);
            Motion.Duration = OriginDuration;
            RebuildTimeLineAfterMotionDrag();
        });
        OperationManager.AddToOperationSaver(OpSave);
        RebuildTimeLineAfterMotionDrag();
    }

    /// <summary>
    /// The second half: the same motion in every respect but its timing.
    /// </summary>
    private LanotaCameraBase CopyMotion(LanotaCameraBase Motion, float Time, float Duration)
    {
        LanotaCameraXZ Hor = Motion as LanotaCameraXZ;
        if (Hor != null)
        {
            LanotaCameraXZ Copy = Hor.DeepCopy();
            Copy.Time = Time; Copy.Duration = Duration;
            return Copy;
        }
        LanotaCameraY Ver = Motion as LanotaCameraY;
        if (Ver != null)
        {
            LanotaCameraY Copy = Ver.DeepCopy();
            Copy.Time = Time; Copy.Duration = Duration;
            return Copy;
        }
        LanotaCameraRot Rot = Motion as LanotaCameraRot;
        if (Rot != null)
        {
            LanotaCameraRot Copy = Rot.DeepCopy();
            Copy.Time = Time; Copy.Duration = Duration;
            return Copy;
        }
        LanotaCameraTrs Trs = Motion as LanotaCameraTrs;
        if (Trs != null)
        {
            LanotaCameraTrs Copy = Trs.DeepCopy();
            Copy.Time = Time; Copy.Duration = Duration;
            return Copy;
        }
        return null;
    }

    private bool AddMotion(LanotaCameraBase Motion)
    {
        LanotaCameraXZ Hor = Motion as LanotaCameraXZ;
        if (Hor != null) return OperationManager.AddHorizontal(Hor, false, false, false);
        LanotaCameraY Ver = Motion as LanotaCameraY;
        if (Ver != null) return OperationManager.AddVertical(Ver, false, false, false);
        LanotaCameraRot Rot = Motion as LanotaCameraRot;
        if (Rot != null) return OperationManager.AddRotation(Rot, false, false, false);
        LanotaCameraTrs Trs = Motion as LanotaCameraTrs;
        if (Trs != null) return OperationManager.AddTransparency(Trs, false, false, false);
        return false;
    }

    private void DeleteMotion(LanotaCameraBase Motion)
    {
        LanotaCameraXZ Hor = Motion as LanotaCameraXZ;
        if (Hor != null) { OperationManager.DeleteHorizontal(Hor, false); return; }
        LanotaCameraY Ver = Motion as LanotaCameraY;
        if (Ver != null) { OperationManager.DeleteVertical(Ver, false); return; }
        LanotaCameraRot Rot = Motion as LanotaCameraRot;
        if (Rot != null) { OperationManager.DeleteRotation(Rot, false); return; }
        LanotaCameraTrs Trs = Motion as LanotaCameraTrs;
        if (Trs != null) OperationManager.DeleteTransparency(Trs, false);
    }
}
