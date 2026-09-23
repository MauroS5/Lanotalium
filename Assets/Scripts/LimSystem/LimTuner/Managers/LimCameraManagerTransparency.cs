using UnityEngine;

/// <summary>
/// The transparency motion: how see-through the tuner itself is drawn.
///
/// The tuner is the five sprites under the Tuner object (Background, Border,
/// JudgeLine, Arrow and Core) and nothing else. The notes are not its
/// children and are never touched, so a chart can fade the ring away and
/// leave the notes playing on their own.
///
/// A transparency motion is a destination, the way a type 11 horizontal is:
/// it carries the ring from wherever it was to its own number, 0 being gone
/// and 100 the opaque ring the editor has always drawn. Before the first one
/// the ring is at 100.
///
/// The background sprite is the one part that is see-through to begin with.
/// How much, the Tuner Background Opacity preference says; the motion then
/// fades it along with the rest.
/// </summary>
public partial class LimCameraManager
{
    public const float OpaqueTransparency = 100f;

    /// <summary>Where the ring is right now, 0 to 100.</summary>
    public float CurrentTransparency = OpaqueTransparency;

    /// <summary>
    /// Found by name rather than wired in the scene, which this branch does
    /// not edit. The skins swap their sprites (Ritmo and Física two of them,
    /// a TunerSkin folder all five) but never their colour, so nothing else
    /// is writing to these.
    /// </summary>
    private static readonly string[] TunerPartNames = { "Background", "Border", "JudgeLine", "Arrow", "Core" };
    private const string TunerBackgroundPartName = "Background";
    private SpriteRenderer[] TunerParts;
    private Color[] TunerPartColors;
    private bool[] TunerPartIsBackground;
    private float AppliedFade = -1f, AppliedBackground = -1f;

    private void UpdateTunerTransparency()
    {
        // Motions switched off put the camera back where it starts, and the
        // ring goes back to opaque with it.
        CurrentTransparency = DisableMotion ? OpaqueTransparency : CalculateTransparency(Tuner.ChartTime);
        ApplyTunerTransparency(CurrentTransparency);
    }

    /// <summary>
    /// The ring's transparency at a moment of the chart. The same walk the
    /// other three motions make: every motion that has started is played to
    /// its end, or to where the next one took over, whichever came first.
    /// </summary>
    public float CalculateTransparency(float Time)
    {
        float Value = OpaqueTransparency;
        if (Transparency == null) return Value;
        for (int i = 0; i < Transparency.Count; ++i)
        {
            Lanotalium.Chart.LanotaCameraTrs Trs = Transparency[i];
            if (Time < Trs.Time) break;
            float Reached = Time;
            if (i + 1 < Transparency.Count && Transparency[i + 1].Time <= Time) Reached = Transparency[i + 1].Time;
            // A motion of no length at all has simply arrived; dividing by
            // its duration would hand the ease curve a NaN.
            float Percent = Trs.Duration > 0 ? (Reached - Trs.Time) / Trs.Duration : 1f;
            float Target = Mathf.Clamp(Trs.ctp, 0, OpaqueTransparency);
            Value += (Target - Value) * CalculateEasedCurve(Percent, Trs.cfmi);
        }
        return Mathf.Clamp(Value, 0, OpaqueTransparency);
    }

    public void SortTransparencyList()
    {
        if (Transparency == null) return;
        Transparency.Sort((Lanotalium.Chart.LanotaCameraTrs A, Lanotalium.Chart.LanotaCameraTrs B) =>
        {
            return A.Time.CompareTo(B.Time);
        });
    }

    private void ApplyTunerTransparency(float Percent)
    {
        if (!FindTunerParts()) return;
        float Fade = Mathf.Clamp01(Percent / OpaqueTransparency);
        float Background = Mathf.Clamp01(LimSystem.Preferences.TunerBackgroundAlpha / 100f);
        // Written only when something changed, not every frame.
        if (Fade == AppliedFade && Background == AppliedBackground) return;
        AppliedFade = Fade;
        AppliedBackground = Background;
        for (int i = 0; i < TunerParts.Length; ++i)
        {
            if (TunerParts[i] == null) continue;
            Color Shade = TunerPartColors[i];
            Shade.a = (TunerPartIsBackground[i] ? Background : Shade.a) * Fade;
            TunerParts[i].color = Shade;
        }
    }

    /// <summary>
    /// Looks the parts up once, keeping the colour each was given in the
    /// scene, so the fade always works from the original and never from a
    /// colour it wrote itself the frame before.
    /// </summary>
    private bool FindTunerParts()
    {
        if (TunerParts != null) return TunerParts.Length != 0;
        if (TunerGameObject == null) return false;
        TunerParts = new SpriteRenderer[TunerPartNames.Length];
        TunerPartColors = new Color[TunerPartNames.Length];
        TunerPartIsBackground = new bool[TunerPartNames.Length];
        int Found = 0;
        for (int i = 0; i < TunerPartNames.Length; ++i)
        {
            Transform Part = TunerGameObject.transform.Find(TunerPartNames[i]);
            SpriteRenderer Sprite = Part != null ? Part.GetComponent<SpriteRenderer>() : null;
            TunerParts[i] = Sprite;
            TunerPartColors[i] = Sprite != null ? Sprite.color : Color.white;
            TunerPartIsBackground[i] = TunerPartNames[i] == TunerBackgroundPartName;
            if (Sprite != null) ++Found;
        }
        if (Found == 0) TunerParts = new SpriteRenderer[0];
        return Found != 0;
    }
}
