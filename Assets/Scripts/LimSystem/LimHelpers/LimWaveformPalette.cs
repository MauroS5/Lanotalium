using UnityEngine;

namespace Lanotalium.Editor
{
    /// <summary>
    /// The colours the waveform can be drawn in, chosen in Preferences.
    ///
    /// The order is the order of the dropdown and of the labels in the
    /// language packs, so entries are only ever added at the end: the value is
    /// what ends up written in the preferences file.
    /// </summary>
    public enum WaveformColor
    {
        Cyan = 0,
        Red = 1,
        Yellow = 2,
        LightGreen = 3,
        DarkGreen = 4,
        Pink = 5,
        Lilac = 6,
        DarkBlue = 7,
        Orange = 8,
        White = 9
    }
}

/// <summary>
/// Turns a chosen waveform colour into the colour it is drawn with.
///
/// The waveform is read against the dark grey of the TimeLine window, so every
/// one of these is kept bright: a colour that looks right on paper disappears
/// against that background.
/// </summary>
public static class LimWaveformPalette
{
    /// <summary>How many colours there are, for building the dropdown.</summary>
    public const int Count = 10;

    public static Color Get(Lanotalium.Editor.WaveformColor Choice)
    {
        switch (Choice)
        {
            case Lanotalium.Editor.WaveformColor.Red: return new Color(1f, 0.35f, 0.35f);
            case Lanotalium.Editor.WaveformColor.Yellow: return new Color(1f, 0.88f, 0.35f);
            case Lanotalium.Editor.WaveformColor.LightGreen: return new Color(0.55f, 1f, 0.55f);
            case Lanotalium.Editor.WaveformColor.DarkGreen: return new Color(0.25f, 0.7f, 0.35f);
            case Lanotalium.Editor.WaveformColor.Pink: return new Color(1f, 0.5f, 0.8f);
            case Lanotalium.Editor.WaveformColor.Lilac: return new Color(0.75f, 0.6f, 1f);
            case Lanotalium.Editor.WaveformColor.DarkBlue: return new Color(0.35f, 0.5f, 1f);
            case Lanotalium.Editor.WaveformColor.Orange: return new Color(1f, 0.6f, 0.25f);
            case Lanotalium.Editor.WaveformColor.White: return new Color(1f, 1f, 1f);
            // ArcCreate's own waveform, and what this starts out as.
            default: return new Color(0.25f, 0.85f, 0.9f);
        }
    }

    /// <summary>The language key holding this colour's name.</summary>
    public static string TextKey(int Value)
    {
        switch (Value)
        {
            case 1: return "Preferences_WaveformColor_Red";
            case 2: return "Preferences_WaveformColor_Yellow";
            case 3: return "Preferences_WaveformColor_LightGreen";
            case 4: return "Preferences_WaveformColor_DarkGreen";
            case 5: return "Preferences_WaveformColor_Pink";
            case 6: return "Preferences_WaveformColor_Lilac";
            case 7: return "Preferences_WaveformColor_DarkBlue";
            case 8: return "Preferences_WaveformColor_Orange";
            case 9: return "Preferences_WaveformColor_White";
            default: return "Preferences_WaveformColor_Cyan";
        }
    }
}
