using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;

/// <summary>
/// The two things drawn on the ring to make rails easier to edit, both of
/// them switched off together by Rail Note Visual Guide in Preferences.
///
/// A small yellow dot sits on the end of every rail on screen. Nothing else
/// marks where a rail finishes, so without it the place to take hold of with
/// Ctrl would have to be guessed at. It is a plain filled circle, drawn here
/// rather than taken from the joint, whose sprite is a wide bar that reads as
/// a note left behind by accident; and it is yellow rather than any of the
/// colours notes come in, so it is plainly a handle and not part of the chart.
///
/// A thin red line lies across a selected rail wherever the pointer is, which
/// is where S would cut it. It only appears on a rail that has been picked
/// up, so passing the pointer over a chart full of rails does not light them
/// all up; with Ctrl held it jumps to the beatlines and anglelines the rail
/// crosses, and cuts there.
///
/// Both are drawn from prefabs the scene already has, the joint for the bead
/// and the angleline for the cut, so nothing new had to be put in the scene
/// for either of them.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>The dot's width in ring units where a rail is widest, which is 1.</summary>
    private const float RailHandleScale = 0.55f;
    /// <summary>Across the dot's texture, in pixels.</summary>
    private const int RailHandleDotPixels = 64;
    private static readonly Color RailHandleColor = new Color(1f, 0.93f, 0.1f);
    private static readonly Color RailCutColor = new Color(0.95f, 0.28f, 0.28f);
    /// <summary>The cut line's thickness in ring units; the anglelines are 0.1.</summary>
    private const float RailCutLineWidth = 0.05f;
    /// <summary>
    /// How far the cut line is lifted off the ring plane, towards the camera.
    /// It is drawn with the anglelines' opaque material, which depth-tests
    /// against the rail lying at exactly the same height, so at the same
    /// height it is a coin toss which of the two is seen.
    ///
    /// Towards, not up: the tuner camera sits **below** the ring and looks up
    /// at it, so lifting the line in +y buried it behind the rail instead,
    /// which is why it could not be seen at all.
    /// </summary>
    private const float RailCutLineLift = 0.05f;

    private readonly List<GameObject> _RailBeads = new List<GameObject>();
    private LineRenderer _RailCutLine;
    private Sprite _RailDotSprite;

    /// <summary>
    /// Which way the ring has to be left to come nearer the camera. The
    /// camera looks along its own forward, straight at the plane, so the sign
    /// of where it sits is the side it is on.
    /// </summary>
    private float RailLiftDirection
    {
        get { return TunerCamera != null && TunerCamera.transform.position.y > 0 ? 1f : -1f; }
    }

    private void UpdateRailGuides()
    {
        if (!LimSystem.Preferences.RailNoteVisualGuide) { HideRailGuides(); return; }
        if (TunerManager == null || !TunerManager.isInitialized) { HideRailGuides(); return; }
        if (TunerManager.HoldNoteManager == null || TunerManager.HoldNoteManager.HoldNote == null) { HideRailGuides(); return; }
        UpdateRailBeads();
        UpdateRailCutLine();
    }

    private void HideRailGuides()
    {
        for (int i = 0; i < _RailBeads.Count; ++i)
            if (_RailBeads[i] != null && _RailBeads[i].activeSelf) _RailBeads[i].SetActive(false);
        if (_RailCutLine != null && _RailCutLine.gameObject.activeSelf) _RailCutLine.gameObject.SetActive(false);
    }

    private void UpdateRailBeads()
    {
        int Used = 0;
        foreach (LanotaHoldNote Hold in TunerManager.HoldNoteManager.HoldNote)
        {
            if (!Hold.shouldUpdate) continue;
            Vector3 Position; float Scale;
            if (!TryRailEndPoint(Hold, out Position, out Scale)) continue;

            GameObject Bead = TakeRailBead(Used);
            if (Bead == null) break;
            Used++;
            float Degree = RailEndDegree(Hold) + TunerManager.CameraManager.CurrentRotation;
            // Laid flat on the ring the way a note is, and a hair towards the
            // camera so the rail underneath cannot win the depth test.
            Bead.transform.rotation = Quaternion.Euler(new Vector3(90, Degree, 0));
            Bead.transform.position = Position + new Vector3(0, RailCutLineLift * RailLiftDirection, 0);
            Bead.transform.localScale = new Vector3(Scale * RailHandleScale, Scale * RailHandleScale, 1);
            if (!Bead.activeSelf) Bead.SetActive(true);
        }
        for (int i = Used; i < _RailBeads.Count; ++i)
            if (_RailBeads[i] != null && _RailBeads[i].activeSelf) _RailBeads[i].SetActive(false);
    }

    /// <summary>
    /// A dot from the pool, making one more when the pool runs out. Each is
    /// its own object rather than a copy of the joint, so that it can be a
    /// circle; it borrows the joint's layer, material and sorting so that it
    /// is drawn by the tuner camera at all, and goes in the layer
    /// click-to-create draws its cursor in so a dot is never buried under
    /// the rail it marks.
    /// </summary>
    private GameObject TakeRailBead(int Index)
    {
        while (_RailBeads.Count <= Index) _RailBeads.Add(null);
        if (_RailBeads[Index] != null) return _RailBeads[Index];

        GameObject Prefab = TunerManager.HoldNoteManager.oTJoint;
        if (Prefab == null) return null;
        SpriteRenderer Pattern = Prefab.GetComponentInChildren<SpriteRenderer>(true);

        GameObject Dot = new GameObject("RailEndHandle");
        Dot.transform.SetParent(TunerManager.HoldNoteManager.transform, false);
        // The tuner camera draws layers 8 to 13 only, and a new object
        // arrives on layer 0, where it would never be seen.
        Dot.layer = Pattern != null ? Pattern.gameObject.layer : Prefab.layer;
        SpriteRenderer Renderer = Dot.AddComponent<SpriteRenderer>();
        if (Pattern != null) Renderer.sharedMaterial = Pattern.sharedMaterial;
        Renderer.sprite = GetRailDotSprite();
        Renderer.color = RailHandleColor;
        Renderer.sortingLayerName = "ClickToCreate";
        Dot.SetActive(false);
        _RailBeads[Index] = Dot;
        return Dot;
    }

    /// <summary>
    /// A filled circle, drawn once into a small texture. One unit across, so
    /// the scale it is placed at is the width it comes out; the rim fades
    /// over the last pixel, which is enough to keep it from looking cut out
    /// of squared paper.
    /// </summary>
    private Sprite GetRailDotSprite()
    {
        if (_RailDotSprite != null) return _RailDotSprite;

        int Size = RailHandleDotPixels;
        Texture2D Paper = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
        Paper.wrapMode = TextureWrapMode.Clamp;
        Paper.filterMode = FilterMode.Bilinear;
        float Middle = (Size - 1) * 0.5f;
        float Radius = Middle - 1f;
        Color[] Pixels = new Color[Size * Size];
        for (int y = 0; y < Size; ++y)
        {
            for (int x = 0; x < Size; ++x)
            {
                float Across = x - Middle, Down = y - Middle;
                float Distance = Mathf.Sqrt(Across * Across + Down * Down);
                Pixels[y * Size + x] = new Color(1, 1, 1, Mathf.Clamp01(Radius - Distance));
            }
        }
        Paper.SetPixels(Pixels);
        Paper.Apply();
        _RailDotSprite = Sprite.Create(Paper, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        return _RailDotSprite;
    }

    private void UpdateRailCutLine()
    {
        if (!_RailPointerValid || _RailUnderPointer == null || _RailHandleActive) { HideRailCutLine(); return; }
        if (!IsHoldNoteSelected(_RailUnderPointer)) { HideRailCutLine(); return; }

        LineRenderer Line = TakeRailCutLine();
        if (Line == null) return;

        float Percent = LimTunerCoordinate.EasedPercent(LimTunerCoordinate.TimeToMovePercent(_RailCutTime, TunerManager));
        if (Percent < RailHandleMinPercent) { HideRailCutLine(); return; }

        float Degree = RailDegreeAtTime(_RailUnderPointer, _RailCutTime) + TunerManager.CameraManager.CurrentRotation;
        float Radians = Degree * Mathf.Deg2Rad;
        Vector3 Centre = new Vector3(-Percent / 10 * Mathf.Sin(Radians), RailCutLineLift * RailLiftDirection, -Percent / 10 * Mathf.Cos(Radians));
        // Across the rail rather than along it: the way the point would move
        // if the degree were turned up a little, which is the rail's width.
        Vector3 Across = new Vector3(-Mathf.Cos(Radians), 0, Mathf.Sin(Radians));
        // A rail is drawn a hundredth of its distance out across, so half of
        // that reaches from the middle of it to either edge.
        float Half = Percent / 200;

        Line.SetPosition(0, Centre - Across * Half);
        Line.SetPosition(1, Centre + Across * Half);
        if (!Line.gameObject.activeSelf) Line.gameObject.SetActive(true);
    }

    private void HideRailCutLine()
    {
        if (_RailCutLine != null && _RailCutLine.gameObject.activeSelf) _RailCutLine.gameObject.SetActive(false);
    }

    private LineRenderer TakeRailCutLine()
    {
        if (_RailCutLine != null) return _RailCutLine;

        // The Creator hands its angleline tool round once a frame; looked up
        // directly as well, so the very first frame of a session does not
        // decide whether the line is ever built.
        LimAngleLineManager Angleline = LimClickToCreateManager.SharedAnglelineManager;
        if (Angleline == null) Angleline = FindObjectOfType<LimAngleLineManager>();
        if (Angleline == null || Angleline.AnglelinePrefab == null) return null;
        GameObject Made = Instantiate(Angleline.AnglelinePrefab, TunerManager.HoldNoteManager.transform);
        Made.name = "RailCutLine";
        LineRenderer Line = Made.GetComponent<LineRenderer>();
        if (Line == null) { Destroy(Made); return null; }

        LineRenderer Source = Angleline.AnglelinePrefab.GetComponent<LineRenderer>();
        Material Paper = Source != null ? Source.sharedMaterial : null;
        Line.useWorldSpace = true;
        Line.positionCount = 2;
        Line.widthMultiplier = RailCutLineWidth;
        Line.sortingLayerName = "ClickToCreate";
        LimLineColor.Apply(Line, Paper, RailCutColor);
        Made.SetActive(false);
        _RailCutLine = Line;
        return _RailCutLine;
    }
}
