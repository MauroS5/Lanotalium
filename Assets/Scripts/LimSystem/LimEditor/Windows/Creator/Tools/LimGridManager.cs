using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The grid drawn behind the tuner, and the ruler the tuner snaps to.
///
/// Two fields: V. is how many lines run up and down, H. how many run across.
/// They are spread evenly over the viewport and always symmetrical about its
/// middle, so one line is the middle itself, two sit either side of it, three
/// are the middle plus one on each side, and so on. V. 1 and H. 1 together
/// make a cross in the centre.
///
/// The lines belong to the view, not to the chart: they stay put on screen
/// while the tuner is dragged across them, which is what makes them worth
/// snapping to. They are drawn on the Tuner sorting layer just above the
/// backdrop, so the ring, the beatlines and the notes all stay in front.
///
/// The tool is built at runtime from a copy of the Angleline tool, so the
/// scene file does not have to carry it.
/// </summary>
public class LimGridManager : MonoBehaviour
{
    private const int MaximumLines = 32;
    private const float LineHalfThickness = 0.04f;
    /// <summary>
    /// The tuner camera only draws layers 8 to 13, so a line left on the
    /// default layer is simply never rendered. 8 is "Tuner", the one the
    /// ring's own backdrop uses.
    /// </summary>
    private const int TunerLayer = 8;

    /// <summary>
    /// The grid is drawn on the sorting layer below the ring's, so every part
    /// of the tuner passes in front of it. The song's own picture is not in
    /// here at all: it is an interface image behind the whole render, so
    /// anything this camera draws already sits on top of it.
    /// </summary>
    private const string GridSortingLayer = "Background";

    public LimCreatorToolBase ToolBase;
    public Text LabelText, VerticalLabel, HorizontalLabel, OpacityLabel;
    public InputField VerticalField, HorizontalField;
    public Slider OpacitySlider;
    public Toggle VerticalToggle, HorizontalToggle;
    public Image VerticalFieldImage, HorizontalFieldImage;
    public Color InvalidColor = Color.red, ValidColor = Color.green;

    private LimTunerManager _TunerManager;
    private Camera _TunerCamera;

    /// <summary>
    /// Shared by every grid line. Sprites/Default is transparent and does not
    /// write depth, which is the point: the ring's own material is opaque, and
    /// a grid hung closer to the camera than the ring would win the depth test
    /// and draw straight over it. Without depth in the way, the sorting layers
    /// decide, and the tuner keeps the front. It also reads the per-line
    /// colour, so the opacity slider is real transparency rather than a
    /// dimming of the colour.
    /// </summary>
    private static Material _SharedMaterial;
    private static Material SharedMaterial
    {
        get
        {
            if (_SharedMaterial == null)
            {
                Shader Sprites = Shader.Find("Sprites/Default");
                if (Sprites != null) _SharedMaterial = new Material(Sprites);
            }
            return _SharedMaterial;
        }
    }
    private GameObject _LineRoot;
    private readonly List<LineRenderer> _Vertical = new List<LineRenderer>();
    private readonly List<LineRenderer> _Horizontal = new List<LineRenderer>();

    private int _VerticalCount, _HorizontalCount;
    private float _Opacity = 1f;
    private float _HalfWidth, _HalfHeight;

    /// <summary>
    /// How far in front of the camera the grid is hung. Fixed on purpose: it
    /// used to be measured from the camera's height, which a vertical motion
    /// changes, and the lines stretched and thinned as the camera rose and
    /// fell. At a fixed distance they never change size at all.
    /// </summary>
    private float ReferenceDistance
    {
        get
        {
            if (_TunerManager != null && _TunerManager.CameraManager != null && _TunerManager.CameraManager.Default != null)
            {
                float Height = Mathf.Abs(_TunerManager.CameraManager.Default.CamHeight);
                if (Height > 1) return Height;
            }
            return 20f;
        }
    }

    /// <summary>
    /// A grid line is drawn near the camera, but snapping happens out on the
    /// ring's own plane, which is further away: one grid unit covers more
    /// ground out there, by exactly this much.
    /// </summary>
    private float PlaneScale
    {
        get
        {
            if (_TunerCamera == null) return 1;
            float Distance = Mathf.Abs(_TunerCamera.transform.position.y);
            return Distance > 0.01f ? Distance / ReferenceDistance : 1;
        }
    }

    /// <summary>Where the vertical lines sit, measured from the middle of the view.</summary>
    public readonly List<float> VerticalPositions = new List<float>();
    /// <summary>Where the horizontal lines sit, measured from the middle of the view.</summary>
    public readonly List<float> HorizontalPositions = new List<float>();

    public bool HasLines { get { return VerticalPositions.Count > 0 || HorizontalPositions.Count > 0; } }

    /// <summary>The gap between neighbouring lines, used to size the snapping reach.</summary>
    public float VerticalSpacing { get { return _Vertical.Count > 0 ? 2 * _HalfWidth / (_Vertical.Count + 1) : 0; } }
    public float HorizontalSpacing { get { return _Horizontal.Count > 0 ? 2 * _HalfHeight / (_Horizontal.Count + 1) : 0; } }

    /// <summary>The one grid tool, so the tuner drag can ask it where its lines are.</summary>
    public static LimGridManager Instance { get; private set; }

    public void Setup(LimTunerManager TunerManager, Camera TunerCamera)
    {
        Instance = this;
        _TunerManager = TunerManager;
        _TunerCamera = TunerCamera;
    }

    public void SetTexts()
    {
        if (ToolBase != null && LabelText != null) LabelText.text = LimLanguageManager.TextDict["Window_Creator_Grid"];
        if (VerticalLabel != null) VerticalLabel.text = LimLanguageManager.TextDict["Window_Creator_Grid_Vertical"];
        if (HorizontalLabel != null) HorizontalLabel.text = LimLanguageManager.TextDict["Window_Creator_Grid_Horizontal"];
        if (OpacityLabel != null) OpacityLabel.text = LimLanguageManager.TextDict["Window_Creator_Opacity"];
        SetPlaceholder(VerticalField);
        SetPlaceholder(HorizontalField);
    }

    private static void SetPlaceholder(InputField Field)
    {
        if (Field == null) return;
        Text Placeholder = Field.placeholder as Text;
        if (Placeholder != null) Placeholder.text = LimLanguageManager.TextDict["Window_Creator_Grid_Placeholder"];
    }

    public void OnVerticalFieldChange()
    {
        _VerticalCount = ReadCount(VerticalField, VerticalFieldImage, _VerticalCount);
        RebuildLines();
    }

    public void OnHorizontalFieldChange()
    {
        _HorizontalCount = ReadCount(HorizontalField, HorizontalFieldImage, _HorizontalCount);
        RebuildLines();
    }

    public void OnOpacityChange()
    {
        if (OpacitySlider != null) _Opacity = OpacitySlider.value;
    }

    /// <summary>
    /// An empty field means no lines rather than a mistake, so clearing it
    /// puts the grid away without the field turning red.
    /// </summary>
    private int ReadCount(InputField Field, Image Background, int Current)
    {
        if (Field == null) return 0;
        string Text = Field.text.Trim();
        if (Text.Length == 0)
        {
            if (Background != null) Background.color = ValidColor;
            return 0;
        }
        float Value;
        if (!LimNumber.TryParseFloat(Text, out Value) || Value < 0)
        {
            if (Background != null) Background.color = InvalidColor;
            return Current;
        }
        if (Background != null) Background.color = ValidColor;
        return Mathf.Clamp(Mathf.RoundToInt(Value), 0, MaximumLines);
    }

    private void Update()
    {
        if (_TunerManager == null || _TunerCamera == null) return;
        if (!_TunerManager.isInitialized) { HideLines(); return; }
        // Counts rather than the drawn lines: a chart being loaded takes the
        // lines away, and they have to come back on their own afterwards.
        if (_Vertical.Count != WantedVertical || _Horizontal.Count != WantedHorizontal) RebuildLines();
        if (_LineRoot == null) return;

        MeasureView();
        LayoutLines();
    }

    /// <summary>
    /// How much of the chart plane the viewport covers right now. The camera
    /// looks along Y at the plane, so its distance and field of view give the
    /// half height, and the render texture's shape gives the half width.
    /// </summary>
    private void MeasureView()
    {
        // Measured at the fixed distance, so only the window's shape can
        // change these and the lines stand still while the chart plays.
        if (_TunerCamera.orthographic) _HalfHeight = _TunerCamera.orthographicSize;
        else _HalfHeight = ReferenceDistance * Mathf.Tan(_TunerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        _HalfWidth = _HalfHeight * _TunerCamera.aspect;
    }

    /// <summary>
    /// The ticks switch each direction off without losing what was typed, so
    /// a set of lines can be put away and brought back.
    /// </summary>
    private int WantedVertical { get { return VerticalToggle != null && !VerticalToggle.isOn ? 0 : _VerticalCount; } }
    private int WantedHorizontal { get { return HorizontalToggle != null && !HorizontalToggle.isOn ? 0 : _HorizontalCount; } }

    public void OnToggleChange()
    {
        RebuildLines();
    }

    private void RebuildLines()
    {
        if (_LineRoot == null)
        {
            _LineRoot = new GameObject("GridLines");
            _LineRoot.layer = TunerLayer;
            // Hung off the camera at a fixed distance: the grid then belongs
            // to the view, standing still on screen while the tuner is
            // dragged across it, whatever the chart does to the camera.
            _LineRoot.transform.SetParent(_TunerCamera != null ? _TunerCamera.transform : null, false);
            _LineRoot.transform.localPosition = new Vector3(0, 0, ReferenceDistance);
            _LineRoot.transform.localRotation = Quaternion.identity;
        }
        AcquireLines(_Vertical, WantedVertical);
        AcquireLines(_Horizontal, WantedHorizontal);
    }

    private void AcquireLines(List<LineRenderer> Lines, int Count)
    {
        while (Lines.Count > Count)
        {
            if (Lines[Lines.Count - 1] != null) Destroy(Lines[Lines.Count - 1].gameObject);
            Lines.RemoveAt(Lines.Count - 1);
        }
        while (Lines.Count < Count)
        {
            GameObject Holder = new GameObject("GridLine", typeof(LineRenderer));
            Holder.layer = TunerLayer;
            Holder.transform.SetParent(_LineRoot.transform, false);
            LineRenderer Line = Holder.GetComponent<LineRenderer>();
            Line.useWorldSpace = false;
            Line.positionCount = 2;
            Line.widthMultiplier = LineHalfThickness * 2;
            Line.numCapVertices = 0;
            Line.sortingLayerName = GridSortingLayer;
            Line.sortingOrder = 0;
            Line.receiveShadows = false;
            Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (SharedMaterial != null) Line.sharedMaterial = SharedMaterial;
            Lines.Add(Line);
        }
    }

    /// <summary>
    /// Takes the lines off screen without forgetting how many were asked
    /// for, so they return once there is a chart again.
    /// </summary>
    private void HideLines()
    {
        if (_Vertical.Count == 0 && _Horizontal.Count == 0) return;
        AcquireLines(_Vertical, 0);
        AcquireLines(_Horizontal, 0);
        VerticalPositions.Clear();
        HorizontalPositions.Clear();
    }

    /// <summary>
    /// Spreads the lines over the view. With N lines the view is cut into
    /// N + 1 equal strips, so the lines land on the joins: one line falls in
    /// the middle, two straddle it, three are middle plus one each side.
    /// </summary>
    private void LayoutLines()
    {
        // White with the slider in the alpha: the material reads it directly.
        Color Tint = new Color(1, 1, 1, Mathf.Clamp01(_Opacity));

        VerticalPositions.Clear();
        for (int i = 0; i < _Vertical.Count; ++i)
        {
            float X = -_HalfWidth + (i + 1) * (2 * _HalfWidth / (_Vertical.Count + 1));
            VerticalPositions.Add(X);
            _Vertical[i].SetPosition(0, new Vector3(X, -_HalfHeight, 0));
            _Vertical[i].SetPosition(1, new Vector3(X, _HalfHeight, 0));
            _Vertical[i].startColor = Tint;
            _Vertical[i].endColor = Tint;
        }

        HorizontalPositions.Clear();
        for (int i = 0; i < _Horizontal.Count; ++i)
        {
            float Y = -_HalfHeight + (i + 1) * (2 * _HalfHeight / (_Horizontal.Count + 1));
            HorizontalPositions.Add(Y);
            _Horizontal[i].SetPosition(0, new Vector3(-_HalfWidth, Y, 0));
            _Horizontal[i].SetPosition(1, new Vector3(_HalfWidth, Y, 0));
            _Horizontal[i].startColor = Tint;
            _Horizontal[i].endColor = Tint;
        }
    }

    /// <summary>
    /// Pulls a point on the view towards the nearest line on each axis, the
    /// way a dragged note is pulled onto a beatline. Reach is how close it
    /// has to be to be caught; near a crossing both axes catch at once and
    /// the point lands exactly on it.
    /// </summary>
    public Vector2 Snap(Vector2 ViewPoint)
    {
        float Scale = PlaneScale;
        float X = SnapTo(VerticalPositions, ViewPoint.x, VerticalSpacing, Scale);
        float Z = SnapTo(HorizontalPositions, ViewPoint.y, HorizontalSpacing, Scale);
        return new Vector2(X, Z);
    }

    /// <summary>
    /// The reach is deliberately short, a seventh of the gap and never more
    /// than a little: a wide one holds on long after the pointer has moved
    /// away, and the ring stops following the cursor.
    /// </summary>
    private static float SnapTo(List<float> Positions, float Value, float Spacing, float Scale)
    {
        if (Positions.Count == 0) return Value;
        float Reach = Mathf.Min(0.7f, Spacing * Scale / 7f);
        float Best = Value, BestDelta = float.MaxValue;
        foreach (float Position in Positions)
        {
            float OnPlane = Position * Scale;
            float Delta = Mathf.Abs(OnPlane - Value);
            if (Delta < BestDelta) { BestDelta = Delta; Best = OnPlane; }
        }
        return BestDelta <= Reach ? Best : Value;
    }
}
