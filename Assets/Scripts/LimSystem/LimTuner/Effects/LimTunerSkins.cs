using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Tuner skins made by the community, from Flowaria's UiTweak "Tuner Skin":
/// a folder in StreamingAssets/TunerSkin holding up to five pictures, one per
/// part of the ring (Background, Border, JudgeLine, Arrow, Core .png, in any
/// case). The folder's name is the skin's name. A part the folder lacks keeps
/// the picture it had.
///
/// The editor's own two skins, Ritmo and Física, only change Background and
/// Border. A skin from a folder goes on top of whichever of them is chosen,
/// and choosing either of them again takes it off (see
/// <see cref="LimTunerWindowManager.UseRitmoSkin"/>).
///
/// Every picture is drawn at the size of the part it replaces, whatever its
/// resolution. The plugin gave them all 100 pixels to the unit, which is
/// right for pictures cut to the editor's own sizes but made the 1024 pixel
/// cores of the "HD" skins five times too big, covering the ring.
///
/// How see-through the background is stays with the Tuner Background Opacity
/// preference, which the plugin's own Alpha setting duplicated.
///
/// The HD core (Flowaria's AUTO_CorePatch: the same core drawn at 1024
/// pixels) takes the Core's place whenever the skin in use brings no core of
/// its own. The plugin also darkened it to 70 per cent; that is left out,
/// since the transparency motion owns the ring's colours and the core is
/// the same picture as before, only sharper.
/// </summary>
public static class LimTunerSkins
{
    private static readonly string[] PartNames = { "Background", "Border", "JudgeLine", "Arrow", "Core" };
    /// <summary>The parts Ritmo and Física paint; the others belong only to the scene.</summary>
    private const int BuiltInParts = 2;
    private const int CorePart = 4;

    private static SpriteRenderer[] Parts;
    private static Sprite[] SceneSprites;
    private static string AppliedName;
    /// <summary>The skin's picture for each part, or null where the part keeps its own.</summary>
    private static readonly Sprite[] Shown = new Sprite[5];
    private static Sprite HdCore;
    private static readonly List<Object> Made = new List<Object>();

    public static string Folder
    {
        get { return Path.Combine(Application.streamingAssetsPath, "TunerSkin"); }
    }

    /// <summary>The skins there are, by folder name, in alphabetical order.</summary>
    public static List<string> Available()
    {
        List<string> Names = new List<string>();
        if (!Directory.Exists(Folder)) return Names;
        foreach (string SkinFolder in Directory.GetDirectories(Folder))
        {
            bool HasPart = false;
            foreach (string Part in PartNames) HasPart |= FindPart(SkinFolder, Part) != null;
            if (HasPart) Names.Add(Path.GetFileName(SkinFolder));
        }
        Names.Sort(System.StringComparer.OrdinalIgnoreCase);
        return Names;
    }

    /// <summary>
    /// Puts the skin the preferences name on the ring, if it is not there
    /// already; with <paramref name="Force"/>, again anyway, which is what the
    /// tuner window asks for after painting Ritmo or Física over two parts.
    /// </summary>
    public static void Refresh(bool Force)
    {
        if (!FindParts()) return;
        string Wanted = LimSystem.Preferences.CustomTunerSkin ?? string.Empty;
        string Key = Wanted + (LimSystem.Preferences.HdCore ? "|hd" : string.Empty);
        if (Key == AppliedName)
        {
            // Already loaded: only put back what was painted over.
            if (!Force) return;
            RestoreBuiltIn();
            Show();
            return;
        }
        AppliedName = Key;

        string SkinFolder = Wanted.Length != 0 ? Path.Combine(Folder, Wanted) : null;
        if (SkinFolder != null && !Directory.Exists(SkinFolder)) SkinFolder = null;
        List<Object> Old = new List<Object>(Made);
        Made.Clear();
        // Made again below if wanted: the old one goes with the rest.
        HdCore = null;
        for (int i = 0; i < PartNames.Length; ++i) Shown[i] = SkinFolder != null && Parts[i] != null ? LoadPart(SkinFolder, i) : null;
        // Ritmo or Física first, so a skin without a Background or a Border
        // shows the chosen one's under it.
        RestoreBuiltIn();
        Show();
        foreach (Object Gone in Old) if (Gone != null) Object.Destroy(Gone);
    }

    private static void Show()
    {
        for (int i = 0; i < PartNames.Length; ++i)
        {
            if (Parts[i] == null) continue;
            if (Shown[i] != null) Parts[i].sprite = Shown[i];
            else if (i == CorePart && LimSystem.Preferences.HdCore && HdCoreSprite() != null) Parts[i].sprite = HdCore;
            // The built-in skin's two parts are already right; the rest go
            // back to the scene's pictures.
            else if (i >= BuiltInParts) Parts[i].sprite = SceneSprites[i];
        }
    }

    /// <summary>
    /// The ring's five sprites in the tuner scene now open, and the pictures
    /// the scene gave them. A new scene (another project opened) has new
    /// objects, and is looked up afresh.
    /// </summary>
    private static bool FindParts()
    {
        if (Parts != null && Parts[0] != null) return true;
        LimTunerManager Tuner = LimTunerManager.Instance;
        if (Tuner == null || Tuner.BpmManager == null) return false;
        Transform[] Places = { Tuner.BpmManager.Background, Tuner.BpmManager.Border, Tuner.BpmManager.JudgeLine, Tuner.BpmManager.Arrow, Tuner.BpmManager.Core };
        Parts = new SpriteRenderer[PartNames.Length];
        SceneSprites = new Sprite[PartNames.Length];
        for (int i = 0; i < Places.Length; ++i)
        {
            Parts[i] = Places[i] != null ? Places[i].GetComponent<SpriteRenderer>() : null;
            SceneSprites[i] = Parts[i] != null ? Parts[i].sprite : null;
        }
        AppliedName = null;
        HdCore = null;
        for (int i = 0; i < Shown.Length; ++i) Shown[i] = null;
        foreach (Object Gone in Made) if (Gone != null) Object.Destroy(Gone);
        Made.Clear();
        return Parts[0] != null;
    }

    /// <summary>Ritmo or Física on Background and Border, whichever the tuner window has chosen.</summary>
    private static void RestoreBuiltIn()
    {
        LimTunerWindowManager Window = Object.FindObjectOfType<LimTunerWindowManager>();
        if (Window != null) Window.PaintBuiltInSkin();
        else for (int i = 0; i < BuiltInParts; ++i) if (Parts[i] != null) Parts[i].sprite = SceneSprites[i];
    }

    /// <summary>The HD core, made the size of the scene's core once per scene.</summary>
    private static Sprite HdCoreSprite()
    {
        if (HdCore != null) return HdCore;
        Sprite Source = Resources.Load<Sprite>("UiTweak/core_big");
        Sprite Reference = SceneSprites[CorePart];
        if (Source == null || Reference == null) return null;
        float PixelsPerUnit = Reference.pixelsPerUnit * Source.rect.width / Reference.rect.width;
        HdCore = Sprite.Create(Source.texture, Source.rect, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        HdCore.name = "CoreHD";
        Made.Add(HdCore);
        return HdCore;
    }

    private static string FindPart(string SkinFolder, string Part)
    {
        foreach (string File in Directory.GetFiles(SkinFolder, "*.png"))
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(File), Part, System.StringComparison.OrdinalIgnoreCase)) return File;
        }
        return null;
    }

    /// <summary>
    /// One part of a skin as a sprite the size of the part it replaces:
    /// its pixels per unit are scaled by how much wider it is than the
    /// scene's picture.
    /// </summary>
    private static Sprite LoadPart(string SkinFolder, int Index)
    {
        string File = FindPart(SkinFolder, PartNames[Index]);
        if (File == null) return null;
        Sprite Reference = SceneSprites[Index];
        Texture2D Picture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        try
        {
            if (!Picture.LoadImage(System.IO.File.ReadAllBytes(File), true))
            {
                Object.Destroy(Picture);
                return null;
            }
        }
        catch (System.Exception Error)
        {
            Debug.LogWarning("Tuner skin: could not read " + File + ": " + Error.Message);
            Object.Destroy(Picture);
            return null;
        }
        Picture.name = PartNames[Index];
        Picture.wrapMode = TextureWrapMode.Clamp;
        Picture.filterMode = FilterMode.Trilinear;
        Picture.anisoLevel = 4;
        float PixelsPerUnit = 100;
        if (Reference != null && Reference.rect.width > 0) PixelsPerUnit = Reference.pixelsPerUnit * Picture.width / Reference.rect.width;
        Sprite Result = Sprite.Create(Picture, new Rect(0, 0, Picture.width, Picture.height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        Result.name = PartNames[Index];
        Made.Add(Picture);
        Made.Add(Result);
        return Result;
    }
}
