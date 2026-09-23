using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Copy and paste for motions, with the same manners as notes.
///
/// Ctrl+C keeps the selected motions, Ctrl+V raises a see-through copy of
/// them on the timeline that follows the pointer, a left click drops them
/// where they are, and a right click puts the preview away. Dropping does not
/// end it: the preview stays up so the same run of motions can be laid down
/// again and again until it is cancelled.
///
/// The preview sticks to beatlines while they are switched on, the same as
/// dragging a motion does, and the whole group keeps its spacing: the
/// earliest motion of the copy lands under the pointer and the rest follow at
/// the distance they had.
/// </summary>
public partial class LimTimeLineManager
{
    private const float MotionGhostOpacity = 0.55f;

    private class MotionClip
    {
        public LanotaCameraBase Data;
        /// <summary>Seconds after the earliest motion of the copy.</summary>
        public float OffsetTime;
        public GameObject Ghost;
    }

    private readonly List<MotionClip> _MotionClipboard = new List<MotionClip>();
    private bool _MotionPasteActive;

    /// <summary>True while a motion paste is waiting to be placed.</summary>
    public bool IsMotionPasting { get { return _MotionPasteActive; } }
    public bool HasCopiedMotions { get { return _MotionClipboard.Count > 0; } }

    public void CopySelectedMotions()
    {
        if (OperationManager.SelectedMotions.Count == 0) return;
        CancelMotionPaste();
        _MotionClipboard.Clear();

        float Anchor = float.MaxValue;
        foreach (LanotaCameraBase Motion in OperationManager.SelectedMotions)
            if (Motion.Time < Anchor) Anchor = Motion.Time;

        foreach (LanotaCameraBase Motion in OperationManager.SelectedMotions)
        {
            LanotaCameraBase Copy = CopyMotion(Motion, Motion.Time - Anchor, Motion.Duration);
            if (Copy != null) _MotionClipboard.Add(new MotionClip { Data = Copy, OffsetTime = Motion.Time - Anchor });
        }
        // This one lives in the text dictionary, not the notification one:
        // asking the wrong dictionary throws, which is what it did.
        LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Copier_Msg_Success"]);
    }

    /// <summary>
    /// Puts a set of motions in the clipboard and raises the preview. It is
    /// how a kept pattern is handed over, so that a saved group of motions is
    /// placed the way a saved group of notes is: it follows the pointer and a
    /// click drops it, rather than landing at the playhead the moment the
    /// button is pressed.
    ///
    /// The motions handed in are read for their shape only; the copies in the
    /// clipboard are the ones that get placed.
    /// </summary>
    public void LoadMotionsIntoClipboard(List<LanotaCameraBase> Motions)
    {
        if (Motions == null || Motions.Count == 0) return;
        CancelMotionPaste();
        _MotionClipboard.Clear();

        float Anchor = float.MaxValue;
        foreach (LanotaCameraBase Motion in Motions) if (Motion.Time < Anchor) Anchor = Motion.Time;

        foreach (LanotaCameraBase Motion in Motions)
        {
            LanotaCameraBase Copy = CopyMotion(Motion, Motion.Time - Anchor, Motion.Duration);
            if (Copy != null) _MotionClipboard.Add(new MotionClip { Data = Copy, OffsetTime = Motion.Time - Anchor });
        }
        if (_MotionClipboard.Count == 0) return;
        BeginMotionPaste();
    }

    public void BeginMotionPaste()
    {
        if (_MotionClipboard.Count == 0) return;
        CancelMotionPaste();

        foreach (MotionClip Clip in _MotionClipboard)
        {
            RectTransform Row = RowFor(Clip.Data.Type);
            if (Row == null) continue;
            Clip.Ghost = Instantiate(TimeLineObject, Row);
            // A preview is not a motion: it must not answer clicks or take
            // the selection with it.
            Button Btn = Clip.Ghost.GetComponent<Button>();
            if (Btn != null) Btn.interactable = false;
            Image Face = Clip.Ghost.GetComponent<Image>();
            if (Face != null)
            {
                Color Tint = ColourFor(Clip.Data.Type);
                Tint.a *= MotionGhostOpacity;
                Face.color = Tint;
                Face.raycastTarget = false;
            }
        }
        _MotionPasteActive = true;
    }

    public void CancelMotionPaste()
    {
        foreach (MotionClip Clip in _MotionClipboard)
        {
            if (Clip.Ghost != null) Destroy(Clip.Ghost);
            Clip.Ghost = null;
        }
        _MotionPasteActive = false;
    }

    private void DetectMotionPaste()
    {
        if (!_MotionPasteActive) return;

        // Right click abandons a paste that has not been dropped yet, and
        // puts the selection down with it, the way it does for notes.
        if (Input.GetMouseButtonDown(1))
        {
            CancelMotionPaste();
            OperationManager.SelectNothing();
            OperationManager.DeSelectAllMotions();
            return;
        }

        float Anchor = PasteAnchorTime();
        foreach (MotionClip Clip in _MotionClipboard)
        {
            if (Clip.Ghost == null) continue;
            RectTransform Rect = Clip.Ghost.GetComponent<RectTransform>();
            Rect.anchoredPosition = new Vector2((Anchor + Clip.OffsetTime) * Scale, 0);
            Rect.sizeDelta = new Vector2(Clip.Data.Duration * Scale, 30);
        }

        if (Input.GetMouseButtonDown(0) && IsPointerOverTimeLine()) CommitMotionPaste(Anchor);
    }

    private float PasteAnchorTime()
    {
        float Anchor = IsPointerOverTimeLine() ? PointerTiming() : TunerManager.ChartTime;
        Anchor = SnapMotionTime(Anchor, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        return Mathf.Max(0, Anchor);
    }

    /// <summary>
    /// Lays the copy down. Anything that would land on top of a motion
    /// already there is turned away by the editor's own checks, with its
    /// message, and the rest still arrive. The preview stays up afterwards,
    /// so the same run can be placed again further along.
    /// </summary>
    private void CommitMotionPaste(float Anchor)
    {
        List<LanotaCameraXZ> NewHorizontal = new List<LanotaCameraXZ>();
        List<LanotaCameraY> NewVertical = new List<LanotaCameraY>();
        List<LanotaCameraRot> NewRotation = new List<LanotaCameraRot>();
        List<LanotaCameraTrs> NewTransparency = new List<LanotaCameraTrs>();

        foreach (MotionClip Clip in _MotionClipboard)
        {
            LanotaCameraBase Placed = CopyMotion(Clip.Data, Anchor + Clip.OffsetTime, Clip.Data.Duration);
            LanotaCameraXZ Hor = Placed as LanotaCameraXZ;
            if (Hor != null) { if (OperationManager.AddHorizontal(Hor, false, false, false)) NewHorizontal.Add(Hor); continue; }
            LanotaCameraY Ver = Placed as LanotaCameraY;
            if (Ver != null) { if (OperationManager.AddVertical(Ver, false, false, false)) NewVertical.Add(Ver); continue; }
            LanotaCameraRot Rot = Placed as LanotaCameraRot;
            if (Rot != null) { if (OperationManager.AddRotation(Rot, false, false, false)) NewRotation.Add(Rot); continue; }
            LanotaCameraTrs Trs = Placed as LanotaCameraTrs;
            if (Trs != null) { if (OperationManager.AddTransparency(Trs, false, false, false)) NewTransparency.Add(Trs); }
        }
        if (NewHorizontal.Count + NewVertical.Count + NewRotation.Count + NewTransparency.Count == 0) return;

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            foreach (LanotaCameraXZ M in NewHorizontal) OperationManager.AddHorizontal(M, false, false, false);
            foreach (LanotaCameraY M in NewVertical) OperationManager.AddVertical(M, false, false, false);
            foreach (LanotaCameraRot M in NewRotation) OperationManager.AddRotation(M, false, false, false);
            foreach (LanotaCameraTrs M in NewTransparency) OperationManager.AddTransparency(M, false, false, false);
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            foreach (LanotaCameraXZ M in NewHorizontal) OperationManager.DeleteHorizontal(M, false);
            foreach (LanotaCameraY M in NewVertical) OperationManager.DeleteVertical(M, false);
            foreach (LanotaCameraRot M in NewRotation) OperationManager.DeleteRotation(M, false);
            foreach (LanotaCameraTrs M in NewTransparency) OperationManager.DeleteTransparency(M, false);
            OperationManager.DeSelectAllMotions();
        });
        OperationManager.AddToOperationSaver(OpSave);
    }

    private RectTransform RowFor(int Type)
    {
        if (Type == 8 || Type == 11) return HorTransform;
        if (Type == 10) return VerTransform;
        if (Type == 13) return RotTransform;
        if (Type == 14) return TrsTransform;
        return null;
    }

    private Color ColourFor(int Type)
    {
        switch (Type)
        {
            case 8: return Tp8;
            case 10: return Tp10;
            case 11: return Tp11;
            case 13: return Tp13;
            case 14: return Tp14;
        }
        return Color.white;
    }
}
