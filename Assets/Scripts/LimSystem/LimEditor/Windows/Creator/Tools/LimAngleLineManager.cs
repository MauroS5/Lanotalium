using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class LimAngleLineManager : MonoBehaviour
{
    public LimCreatorToolBase ToolBase;
    public LimTunerManager TunerManager;
    public InputField AnglelineInputField;
    public Image AnglelineImg;
    public Color InvalidColor, ValidColor;
    public GameObject AnglelinePrefab;
    public Transform AnglelineTransform;
    public Toggle EnableToggle;
    private List<float> Angles = new List<float>();
    private List<float> RotatedAngles = new List<float>();
    private List<LineRenderer> Anglelines = new List<LineRenderer>();

    // The quarters of the ring in gold, the diagonals between them in light
    // bronze; every other angleline keeps the white it always had.
    private static readonly Color QuarterColor = new Color(1f, 0.84f, 0.25f);
    private static readonly Color DiagonalColor = new Color(0.85f, 0.63f, 0.42f);
    private const float LandmarkTolerance = 0.01f;
    /// <summary>Set by the tool's own slider; 1 is the look these lines always had.</summary>
    public float LineOpacity = 1f;
    private Material AnglelineMaterial;

    public bool Enable
    {
        get
        {
            return EnableToggle.isOn;
        }
        set
        {
            EnableToggle.isOn = value;
            if (!value)
            { foreach (LineRenderer g in Anglelines) Destroy(g.gameObject); Anglelines.Clear(); }
        }
    }

    private void Update()
    {
        if (!Enable) return;
        UpdateRotatedAngles();
        UpdateAnglelines();
    }

    private float CalculateRoundedDegree(float Degree)
    {
        while (Degree > 360) Degree -= 360;
        while (Degree < 0) Degree += 360;
        return Degree;
    }
    public float FindAttachToAnglelineByDegree(float Degree, float Threshold)
    {
        for (int i = 0; i < RotatedAngles.Count; ++i)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(Degree, RotatedAngles[i])) < Threshold) return RotatedAngles[i];
        }
        return Degree;
    }

    /// <summary>Number of anglelines currently on screen; zero while the tool is off.</summary>
    public int AnglelineCount { get { return RotatedAngles.Count; } }

    /// <summary>
    /// The angles as they were typed, without the camera's rotation, for
    /// anything that has to work out where a line falls on the chart rather
    /// than on the screen. Read only; the list is the tool's own.
    /// </summary>
    public List<float> AnglelineAngles { get { return Angles; } }

    /// <summary>
    /// Nearest angleline to an absolute on-screen degree, with no distance
    /// threshold. Used while dragging notes, where the note should hop from
    /// one line to the next rather than only snapping when already close.
    /// Returns Degree unchanged when no anglelines exist.
    /// </summary>
    public float FindNearestAnglelineByDegree(float Degree)
    {
        if (RotatedAngles.Count == 0) return Degree;
        float Best = Degree, BestDelta = float.MaxValue;
        for (int i = 0; i < RotatedAngles.Count; ++i)
        {
            float Delta = Mathf.Abs(Mathf.DeltaAngle(Degree, RotatedAngles[i]));
            if (Delta < BestDelta) { BestDelta = Delta; Best = RotatedAngles[i]; }
        }
        return Best;
    }

    /// <summary>
    /// Nearest angleline to a note's own degree, the one the inspector shows
    /// and the one typed in the tool, with no distance threshold.
    ///
    /// Compared without the camera's rotation in the way, so a note lands on
    /// exactly the degree written in the field however the camera happens to
    /// be turned at that moment. The answer is kept near the degree it came
    /// from, so a note at 370 stays at 370 rather than dropping to 10.
    /// Returns Degree unchanged when no anglelines exist.
    /// </summary>
    public float FindNearestAnglelineByRelativeDegree(float Degree)
    {
        if (Angles.Count == 0) return Degree;
        float Best = 0, BestDelta = float.MaxValue;
        for (int i = 0; i < Angles.Count; ++i)
        {
            float Delta = Mathf.DeltaAngle(Degree, Angles[i]);
            if (Mathf.Abs(Delta) < Mathf.Abs(BestDelta)) { BestDelta = Delta; Best = Delta; }
        }
        return BestDelta == float.MaxValue ? Degree : Degree + Best;
    }

    /// <summary>
    /// How far a note has to turn to reach the angleline before or after it.
    ///
    /// Works on the angles as typed in the tool, not on the rotated copies
    /// drawn on screen, so a note lands on exactly the degree written in the
    /// field whatever the camera is doing at that moment. The answer is a
    /// signed delta rather than the line itself, which keeps a note that sits
    /// at 370 or -20 near where it was instead of snapping it into 0..360.
    ///
    /// Lines closer than Epsilon count as the line the note is already on, so
    /// a note sitting on one always moves off it. Walking past the last line
    /// wraps around the ring. False when there are no anglelines at all.
    /// </summary>
    public bool TryFindPrevOrNextAngleline(float Degree, bool Forward, float Epsilon, out float Delta)
    {
        Delta = 0;
        if (Angles.Count == 0) return false;
        // Deltas run from -180 to 180, so "ahead" is the smallest positive
        // one and, once past 180, the most negative one.
        bool Found = false, Wrapped = false;
        for (int i = 0; i < Angles.Count; ++i)
        {
            float Candidate = Mathf.DeltaAngle(Degree, Angles[i]);
            if (Mathf.Abs(Candidate) < Epsilon) continue;
            bool Ahead = Forward ? Candidate > 0 : Candidate < 0;
            if (Ahead)
            {
                if (!Found || Wrapped || Mathf.Abs(Candidate) < Mathf.Abs(Delta)) Delta = Candidate;
                Found = true; Wrapped = false;
            }
            else if (!Found)
            {
                // Nothing ahead yet: the far side of the ring is the fallback.
                if (!Wrapped || Mathf.Abs(Candidate) > Mathf.Abs(Delta)) Delta = Candidate;
                Wrapped = true;
            }
        }
        return Found || Wrapped;
    }

    private void GenerateCorrectQuantityAngleline(int AnglelineCount)
    {
        int DeltaQuantity = AnglelineCount - Anglelines.Count;
        if (DeltaQuantity == 0) return;
        if (DeltaQuantity < 0)
        {
            for (int i = 0; i > DeltaQuantity; i--)
            {
                Destroy(Anglelines[Anglelines.Count - 1].gameObject);
                Anglelines.RemoveAt(Anglelines.Count - 1);
            }
        }
        else if (DeltaQuantity > 0)
        {
            for (int i = 0; i < DeltaQuantity; ++i)
            {
                Anglelines.Add(Instantiate(AnglelinePrefab, AnglelineTransform).GetComponent<LineRenderer>());
            }
        }
    }
    private void UpdateRotatedAngles()
    {
        RotatedAngles.Clear();
        foreach (float a in Angles) RotatedAngles.Add(CalculateRoundedDegree(a + TunerManager.CameraManager.CurrentRotation));
    }
    private void UpdateAnglelines()
    {
        GenerateCorrectQuantityAngleline(Angles.Count);
        int Index = 0;
        foreach (LineRenderer Line in Anglelines)
        {
            float RotatedDegree = RotatedAngles[Index];
            Line.SetPosition(1, new Vector3(-10 * Mathf.Sin(RotatedDegree * Mathf.Deg2Rad), 0, -10 * Mathf.Cos(RotatedDegree * Mathf.Deg2Rad)));
            PaintAngleline(Line, Angles[Index]);
            Index++;
        }
    }

    /// <summary>
    /// Marks the landmarks of the ring. Read on the angle as typed, not on
    /// the rotated copy drawn on screen, so 90 is gold because it is 90 and
    /// not because of where the camera happens to be looking.
    /// </summary>
    private void PaintAngleline(LineRenderer Line, float Angle)
    {
        Material Source = GetAnglelineMaterial();
        if (Source == null) return;
        float Fade = Mathf.Clamp01(LineOpacity);
        float Offset = Angle % 90;
        if (Offset < LandmarkTolerance || 90 - Offset < LandmarkTolerance) LimLineColor.Apply(Line, Source, QuarterColor * Fade);
        else if (Mathf.Abs(Offset - 45) < LandmarkTolerance) LimLineColor.Apply(Line, Source, DiagonalColor * Fade);
        // Full strength keeps the material the prefab shipped with.
        else if (Fade > 0.999f) LimLineColor.Reset(Line, Source);
        else LimLineColor.Apply(Line, Source, Color.white * Fade);
    }

    private Material GetAnglelineMaterial()
    {
        if (AnglelineMaterial != null) return AnglelineMaterial;
        if (AnglelinePrefab == null) return null;
        LineRenderer Line = AnglelinePrefab.GetComponent<LineRenderer>();
        if (Line != null) AnglelineMaterial = Line.sharedMaterial;
        return AnglelineMaterial;
    }

    private bool isAngleExisted(float Angle)
    {
        foreach (float iAngle in Angles) if (iAngle == Angle) return true;
        return false;
    }
    private void SortAngles()
    {
        if (Angles.Count == 0 || Angles.Count == 1) return;
        Angles.Sort((float a, float b) => { return a.CompareTo(b); });
    }
    private void TryAddAngle(float Angle)
    {
        while (Angle > 360) Angle -= 360;
        while (Angle < 0) Angle += 360;
        if (!isAngleExisted(Angle)) Angles.Add(Angle);
    }
    private bool TryParseRange(string s)
    {
        float Begin, End;
        int Part;
        string[] Split1 = s.Split('>');
        if (Split1.Length != 2) return false;
        string[] Split2 = Split1[1].Split('/');
        if (Split2.Length != 2) return false;
        if (!LimNumber.TryParseFloat(Split1[0], out Begin)) return false;
        if (!LimNumber.TryParseFloat(Split2[0], out End)) return false;
        if (!int.TryParse(Split2[1], out Part)) return false;
        if (Begin >= End) return false;
        if (Part <= 0 || Part > 24) return false;
        float DeltaAngle = (End - Begin) / Part;
        TryAddAngle(Begin);
        for (int i = 1; i <= Part; ++i)
        {
            TryAddAngle(Begin + i * DeltaAngle);
        }
        return true;
    }
    private bool TryParseAngle(string s)
    {
        float Angle;
        if (!LimNumber.TryParseFloat(s, out Angle)) return false;
        TryAddAngle(Angle);
        return true;
    }
    private bool TryParse(string s)
    {
        Angles.Clear();
        string[] AngleStrs = s.Split(';');
        foreach (string AngleStr in AngleStrs)
        {
            if (!TryParseAngle(AngleStr))
                if (!TryParseRange(AngleStr))
                    return false;
        }
        SortAngles();
        return true;
    }

    public void OnAnglelineInputFieldChange()
    {
        if (!TryParse(AnglelineInputField.text))
        {
            AnglelineImg.color = InvalidColor;
            return;
        }
        AnglelineImg.color = ValidColor;
    }
    public void OnEnableToggleChange()
    {
        Enable = EnableToggle.isOn;
    }
}
