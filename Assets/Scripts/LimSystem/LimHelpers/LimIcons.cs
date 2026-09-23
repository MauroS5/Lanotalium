using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The editor's icons, loaded from Assets/Resources/Icons.
///
/// Each file is a white shape with the drawing in its alpha channel, so one
/// file serves any colour: the Image is tinted rather than the picture
/// recoloured. They come from the Bigmug interface set on svgrepo, which is
/// CC0, rasterised to 256 px, and the script that fetched and installed them
/// is kept in the session scratchpad.
///
/// A missing file is not an error: TryPlace answers false and the caller
/// keeps whatever label it had before, so the editor still works with the
/// Icons folder empty.
/// </summary>
public static class LimIcons
{
    public const string Favourite = "Icons/favourite";
    public const string Delete = "Icons/delete";
    public const string Edit = "Icons/edit";
    public const string Copy = "Icons/copy";
    public const string Export = "Icons/export";
    public const string Import = "Icons/import";

    private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();

    public static Sprite Get(string Name)
    {
        if (string.IsNullOrEmpty(Name)) return null;
        Sprite Icon;
        // A scene change can destroy what was loaded, leaving the key behind.
        if (Loaded.TryGetValue(Name, out Icon) && Icon != null) return Icon;
        Icon = Resources.Load<Sprite>(Name);
        Loaded[Name] = Icon;
        return Icon;
    }

    /// <summary>
    /// Lays an icon over the whole of a control, inset a little so it does
    /// not touch the edges. False when that icon is not installed.
    /// </summary>
    public static bool TryPlace(RectTransform Parent, string Name, Color Tint, float Inset = 6f)
    {
        if (Parent == null) return false;
        Sprite Icon = Get(Name);
        if (Icon == null) return false;

        GameObject Holder = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = Vector2.zero;
        Rect.anchorMax = Vector2.one;
        Rect.offsetMin = new Vector2(Inset, Inset);
        Rect.offsetMax = new Vector2(-Inset, -Inset);

        Image Graphic = Holder.GetComponent<Image>();
        Graphic.sprite = Icon;
        Graphic.color = Tint;
        Graphic.preserveAspect = true;
        Graphic.raycastTarget = false;
        return true;
    }
}
