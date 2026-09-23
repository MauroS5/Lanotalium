using UnityEngine;

/// <summary>
/// Dragging the tuner itself around the viewport, and handing where it ends
/// up to a motion.
///
/// Hold Ctrl and drag with the left button on the core, the small circle in
/// the middle of the ring, and the whole tuner follows the pointer. The core
/// is used as the handle because nothing else lives there: notes ride the
/// outer part of the ring, and click-to-create ignores the middle too. Hold
/// Alt as well and the middle of the ring is pulled onto the grid's lines,
/// landing exactly on a crossing when it is near one.
///
/// The drag always tracks the pointer underneath: the magnet is applied to
/// what is shown, not to what the drag has accumulated, so letting a line go
/// takes the ring straight back under the cursor instead of leaving it
/// trailing behind.
///
/// What happens on release depends on the selection:
///
/// * exactly one horizontal motion selected — where the tuner was left
///   becomes that motion's destination, which saves typing the target
///   coordinates by hand. It is one undo step, and the typed fields still
///   work as they always did. Grabbing the core moves the playhead to the
///   end of that motion first, so what is on screen is the destination
///   being placed; without that the ring would show some moment in the
///   middle of the motion and jump elsewhere the instant it was dropped.
/// * anything else selected, or nothing — the tuner springs back. Dragging
///   on its own is a way of looking around, not a change to the chart, and
///   with two motions selected there would be no saying which one was meant.
///
/// The drag itself never writes to the chart: it moves an offset the camera
/// is placed by, on top of whatever the chart's own motions say.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>How far from the middle still counts as the core, in ring units.</summary>
    private const float TunerCoreRadius = 2f;
    private const float TunerPanResetClickSeconds = 0.35f;

    private bool _TunerPanActive;
    private bool _TunerPanMoved;
    private Vector3 _TunerPanLastScreen;
    private float _TunerPanLastClickTime = -1;
    /// <summary>Where the pointer has taken the view, before the grid pulls on it.</summary>
    private Vector2 _TunerPanFreeOffset;

    /// <summary>True while the tuner is being dragged around the viewport.</summary>
    public bool IsPanningTuner { get { return _TunerPanActive; } }

    public void DetectTunerPan()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;
        if (TunerManager.CameraManager == null) return;

        if (Input.GetMouseButtonUp(0) && _TunerPanActive) { FinishTunerPan(); return; }
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
        {
            if (_TunerPanActive) FinishTunerPan();
            return;
        }

        if (Input.GetMouseButtonDown(0)) BeginTunerPan();
        else if (Input.GetMouseButton(0) && _TunerPanActive) UpdateTunerPan();
    }

    private void BeginTunerPan()
    {
        if (_PasteActive || IsNoteDragInProgress || _RailHandleActive) return;
        if (!LimMousePosition.IsMouseOverWindow(TunerWindowRect)) return;

        Vector3 Screen = LimTunerCoordinate.MouseToTunerScreen(TunerWindowRect);
        Vector3 World = LimTunerCoordinate.TunerScreenToWorld(Screen, TunerCamera);
        // Measured against the ring's own middle, which stays at the origin
        // however far the view has been dragged.
        if (Vector3.Distance(World, Vector3.zero) > TunerCoreRadius) return;

        // A second click on the core brings a lost ring back.
        if (UnityEngine.Time.unscaledTime - _TunerPanLastClickTime <= TunerPanResetClickSeconds)
        {
            TunerManager.CameraManager.ViewOffset = Vector2.zero;
            _TunerPanLastClickTime = -1;
            _TunerPanActive = false;
            return;
        }
        _TunerPanLastClickTime = UnityEngine.Time.unscaledTime;

        _TunerPanLastScreen = Screen;
        _TunerPanActive = true;
        _TunerPanMoved = false;
        _TunerPanFreeOffset = TunerManager.CameraManager.ViewOffset;
        ShowDestinationOfSelectedMotion();
    }

    /// <summary>
    /// Puts the playhead on the end of the motion about to be edited, so the
    /// ring on screen is the destination itself. Dropping it then leaves the
    /// motion exactly where it was dropped, rather than the ring springing
    /// off to whatever the chart says at some other moment.
    /// </summary>
    private void ShowDestinationOfSelectedMotion()
    {
        Lanotalium.Chart.LanotaCameraXZ Motion = SingleSelectedHorizontalMotion();
        if (Motion == null) return;
        LimMediaPlayerManager Player = TunerManager.MediaPlayerManager;
        if (Player == null) return;

        // A hair past the end, on purpose. A motion just created lasts ten
        // microseconds, and the playhead cannot be placed that precisely: land
        // a fraction short and the ring shows the motion not yet started,
        // which is what used to fling it away when the drag was applied. The
        // easing is clamped at both ends, so overshooting is harmless.
        float Destination = Motion.Time + Motion.Duration + 0.002f;
        if (Mathf.Abs(Player.Time - Destination) < 0.0005f) return;
        Player.IsPlaying = false;
        Player.Time = Mathf.Clamp(Destination, 0, Player.Length);
    }

    /// <summary>
    /// Keeps the spot that was grabbed under the pointer: both points are
    /// read through the same camera in the same frame, so the difference
    /// between them is exactly how far the view has to move, and the camera
    /// goes the other way to bring the ring along with the pointer.
    /// </summary>
    private void UpdateTunerPan()
    {
        Vector3 Screen = LimTunerCoordinate.MouseToTunerScreen(TunerWindowRect);
        if (Screen != _TunerPanLastScreen)
        {
            Vector3 From = LimTunerCoordinate.TunerScreenToWorld(_TunerPanLastScreen, TunerCamera);
            Vector3 To = LimTunerCoordinate.TunerScreenToWorld(Screen, TunerCamera);
            _TunerPanLastScreen = Screen;

            Vector2 Delta = new Vector2(To.x - From.x, To.z - From.z);
            if (Delta != Vector2.zero) _TunerPanMoved = true;
            // The pointer's travel goes into the free offset; the grid is
            // asked afterwards. Pulling on the free offset itself would eat
            // the movement made while a line held the ring.
            _TunerPanFreeOffset -= Delta;
        }

        bool Magnet = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        TunerManager.CameraManager.ViewOffset = Magnet ? SnapOffsetToGrid(_TunerPanFreeOffset) : _TunerPanFreeOffset;
    }

    /// <summary>
    /// Pulls the middle of the ring onto the grid, and answers with the
    /// offset that puts it there.
    ///
    /// The ring sits at the origin and the grid belongs to the view, so where
    /// the ring shows up on the view is simply minus the camera's position:
    /// that is the point offered to the grid, and the offset is worked back
    /// from what the grid answers.
    /// </summary>
    private Vector2 SnapOffsetToGrid(Vector2 Offset)
    {
        LimGridManager Grid = LimGridManager.Instance;
        if (Grid == null || !Grid.HasLines) return Offset;
        LimCameraManager Cam = TunerManager.CameraManager;

        Vector2 Chart = new Vector2(Cam.CurrentHorizontalX, Cam.CurrentHorizontalZ);
        Vector2 RingOnView = -(Chart + Offset);
        Vector2 Snapped = Grid.Snap(RingOnView);
        if (Snapped == RingOnView) return Offset;
        return -Snapped - Chart;
    }

    private void FinishTunerPan()
    {
        _TunerPanActive = false;
        if (!_TunerPanMoved) return;
        _TunerPanMoved = false;

        LimCameraManager Cam = TunerManager.CameraManager;
        Vector2 Offset = Cam.ViewOffset;
        if (Offset == Vector2.zero) return;

        Lanotalium.Chart.LanotaCameraXZ Motion = SingleSelectedHorizontalMotion();
        if (Motion == null)
        {
            // Nothing to hand it to: the look around ends where it started.
            Cam.ViewOffset = Vector2.zero;
            return;
        }
        ApplyOffsetToHorizontalMotion(Motion, Offset);
        Cam.ViewOffset = Vector2.zero;
    }

    /// <summary>
    /// The one selected motion, when it is a horizontal one. A vertical or a
    /// rotation motion has no place to put a position on the plane, and two
    /// motions leave no way of telling which was meant.
    /// </summary>
    private Lanotalium.Chart.LanotaCameraXZ SingleSelectedHorizontalMotion()
    {
        if (SelectedMotions.Count != 1) return null;
        return SelectedMotions[0] as Lanotalium.Chart.LanotaCameraXZ;
    }

    /// <summary>
    /// Moves a motion's destination by however far the view was dragged.
    ///
    /// The destination is read as the camera position the motion arrives at,
    /// shifted by the drag and written back the way that motion states it: a
    /// target motion is given the new place outright, and a delta motion is
    /// given the difference from where it starts. The ring's polar way of
    /// naming a position is the same one the camera uses, so the numbers land
    /// in the fields exactly as the inspector would have taken them.
    /// </summary>
    private void ApplyOffsetToHorizontalMotion(Lanotalium.Chart.LanotaCameraXZ Motion, Vector2 Offset)
    {
        LimCameraManager Cam = TunerManager.CameraManager;

        // Where the motion says it ends, in the chart's own terms.
        float EndRou, EndTheta;
        Cam.CalculateCameraHorizontal(Motion.Time + Motion.Duration, out EndRou, out EndTheta);

        // Where the ring actually is on screen, which is the live camera plus
        // the drag. Read from the live state rather than worked out again from
        // the chart: with the playhead parked on the motion's end the two are
        // the same, and when they are not, this is the one the eye trusts,
        // so the ring stays exactly where it was dropped.
        Vector2 Live = new Vector2(Cam.CurrentHorizontalX, Cam.CurrentHorizontalZ);
        Vector2 End = Live + Offset;

        float OriginCtp = Motion.ctp, OriginCtp1 = Motion.ctp1;
        float TargetCtp, TargetCtp1;
        float NewRou, NewTheta;
        if (Motion.Type == 11)
        {
            // A target motion states the destination outright, so it is
            // written outright, in the same terms the motion already used.
            PlaneToPolarLike(End, OriginCtp1, OriginCtp, out NewRou, out NewTheta);
            TargetCtp = NewTheta;
            TargetCtp1 = NewRou;
        }
        else
        {
            // A delta motion is nudged by the difference instead, so whatever
            // it was already doing survives: a turn and a half stays a turn
            // and a half.
            PlaneToPolarLike(End, EndRou, EndTheta, out NewRou, out NewTheta);
            TargetCtp = OriginCtp + (NewTheta - EndTheta);
            TargetCtp1 = OriginCtp1 + (NewRou - EndRou);
        }

        LimInspectorManager Inspector = InspectorManager;
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            Motion.ctp = TargetCtp; Motion.ctp1 = TargetCtp1;
            if (Inspector != null) Inspector.ComponentMotion.SetMode(Inspector.ComponentMotion.Mode, Inspector.ComponentMotion.Index);
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            Motion.ctp = OriginCtp; Motion.ctp1 = OriginCtp1;
            if (Inspector != null) Inspector.ComponentMotion.SetMode(Inspector.ComponentMotion.Mode, Inspector.ComponentMotion.Index);
        });
        OpSave.Forward();
        AddToOperationSaver(OpSave);
    }

    /// <summary>The camera's own way round: X runs backwards, Z forwards.</summary>
    private static Vector2 PolarToPlane(float Rou, float Theta)
    {
        return new Vector2(-Rou * Mathf.Cos(Theta * Mathf.Deg2Rad), Rou * Mathf.Sin(Theta * Mathf.Deg2Rad));
    }

    private static void PlaneToPolar(Vector2 Point, out float Rou, out float Theta)
    {
        Rou = Mathf.Sqrt(Point.x * Point.x + Point.y * Point.y);
        Theta = 180 - Mathf.Atan2(Point.y, Point.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// The same point named the way this motion already names positions.
    ///
    /// A place on the ring has two polar names: radius 9 at one angle is the
    /// same spot as radius -9 half a turn round, and charts do carry negative
    /// radii. Reading a dragged point back with the plain formula always
    /// answers with a positive radius, and against a motion holding a
    /// negative one that lands the destination on the opposite side; it only
    /// came right on a second try, once the first had turned the radius
    /// positive. So the answer is flipped to the sign the motion is written
    /// in, and the angle brought to the turn nearest the one it has, which
    /// keeps the change to the numbers as small as it really is.
    /// </summary>
    private static void PlaneToPolarLike(Vector2 Point, float LikeRou, float LikeTheta, out float Rou, out float Theta)
    {
        PlaneToPolar(Point, out Rou, out Theta);
        if (LikeRou < 0)
        {
            Rou = -Rou;
            Theta += 180;
        }
        Theta = LikeTheta + Mathf.DeltaAngle(LikeTheta, Theta);
    }
}
