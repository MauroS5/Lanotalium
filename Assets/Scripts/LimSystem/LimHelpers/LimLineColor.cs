using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tints the ring's guide lines, the beatlines and the anglelines.
///
/// Those lines are drawn with a Standard-shader material that ignores the
/// per-renderer vertex colours, so what is seen is the material itself. One
/// material per colour is therefore built on demand from the prefab's own
/// material and shared by every line of that colour: four or five materials
/// for the whole ring, however many lines are on screen.
///
/// Lines that keep the look they always had are handed their prefab material
/// back instead of a copy, so the default appearance is untouched.
/// </summary>
public static class LimLineColor
{
    // Roughly the strength of the emission the prefab materials ship with:
    // these lines are read against a dark tuner, where emission is what shows.
    private const float EmissionStrength = 0.9f;

    /// <summary>Colours are kept to 33 steps a channel, which the eye cannot tell apart.</summary>
    private static float Quantise(float Value)
    {
        return Mathf.Round(Mathf.Clamp01(Value) * 32f) / 32f;
    }

    private class TintedMaterial
    {
        public Material Source;
        public Color Tint;
        public Material Result;
    }

    // A handful of entries, scanned rather than hashed: this runs for every
    // line on screen every frame, and a dictionary key would mean building a
    // string each time.
    private static readonly List<TintedMaterial> Tinted = new List<TintedMaterial>();

    public static void Apply(LineRenderer Line, Material Source, Color Tint)
    {
        if (Line == null || Source == null) return;
        Material Painted = GetTinted(Source, Tint);
        if (Line.sharedMaterial != Painted) Line.sharedMaterial = Painted;
        // Harmless for the Standard shader, correct if the material is ever
        // swapped for one that does read vertex colours.
        Line.startColor = Tint;
        Line.endColor = Tint;
    }

    /// <summary>Puts a line back to the material its prefab came with.</summary>
    public static void Reset(LineRenderer Line, Material Source)
    {
        if (Line == null || Source == null) return;
        if (Line.sharedMaterial != Source) Line.sharedMaterial = Source;
        Line.startColor = Color.white;
        Line.endColor = Color.white;
    }

    private static Material GetTinted(Material Source, Color Tint)
    {
        // Rounded before it is looked up: the opacity sliders would otherwise
        // ask for a slightly different colour on every frame of a drag and
        // leave a new material behind each time.
        Tint = new Color(Quantise(Tint.r), Quantise(Tint.g), Quantise(Tint.b), Quantise(Tint.a));

        for (int i = 0; i < Tinted.Count; ++i)
        {
            TintedMaterial Entry = Tinted[i];
            if (Entry.Source != Source || Entry.Tint != Tint) continue;
            // The copy is made at runtime, so a scene change can leave a
            // destroyed object behind in an entry that is still here.
            if (Entry.Result != null) return Entry.Result;
            Tinted.RemoveAt(i);
            break;
        }

        Material Result = new Material(Source);
        if (Result.HasProperty("_Color")) Result.SetColor("_Color", Tint);
        if (Result.HasProperty("_EmissionColor"))
        {
            Result.EnableKeyword("_EMISSION");
            Result.SetColor("_EmissionColor", Tint * EmissionStrength);
        }
        Tinted.Add(new TintedMaterial { Source = Source, Tint = Tint, Result = Result });
        return Result;
    }
}
