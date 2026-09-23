using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the few controls the editor grows at runtime.
///
/// The scene carries the windows it always had; anything added since is put
/// together here, borrowing the look of a control that is already on screen
/// so the new pieces match the rest instead of arriving in Unity's default
/// grey.
/// </summary>
public static class LimUiBuilder
{
    public static Text CreateLabel(RectTransform Parent, string Name, Font Font, int FontSize,
                                   Color Colour, TextAnchor Alignment)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(0, 1);
        Rect.pivot = new Vector2(0, 1);

        Text Label = Holder.GetComponent<Text>();
        if (Font != null) Label.font = Font;
        Label.fontSize = FontSize;
        Label.color = Colour;
        Label.alignment = Alignment;
        Label.raycastTarget = false;
        return Label;
    }

    /// <summary>
    /// A horizontal slider wearing the sprite of Reference, which should be a
    /// graphic already in the same panel.
    /// </summary>
    public static Slider CreateSlider(RectTransform Parent, string Name, Image Reference,
                                      float Min, float Max, float Value)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(0, 1);
        Rect.pivot = new Vector2(0, 1);

        Image Background = Holder.GetComponent<Image>();
        Dress(Background, Reference, new Color(0.25f, 0.25f, 0.25f, 1));

        // Fill: the part left of the handle.
        RectTransform FillArea = CreateStretchedChild(Rect, "Fill Area", new Vector2(5, 10), new Vector2(-5, -10));
        Image Fill = CreateStretchedChild(FillArea, "Fill", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        Dress(Fill, Reference, new Color(0.65f, 0.65f, 0.65f, 1));

        RectTransform HandleArea = CreateStretchedChild(Rect, "Handle Slide Area", new Vector2(5, 0), new Vector2(-5, 0));
        GameObject HandleHolder = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform Handle = HandleHolder.GetComponent<RectTransform>();
        Handle.SetParent(HandleArea, false);
        Handle.sizeDelta = new Vector2(12, 0);
        Image HandleImage = HandleHolder.GetComponent<Image>();
        Dress(HandleImage, Reference, Color.white);

        Slider Bar = Holder.GetComponent<Slider>();
        Bar.fillRect = Fill.rectTransform;
        Bar.handleRect = Handle;
        Bar.targetGraphic = HandleImage;
        Bar.direction = Slider.Direction.LeftToRight;
        Bar.minValue = Min;
        Bar.maxValue = Max;
        Bar.wholeNumbers = false;
        Bar.value = Value;
        return Bar;
    }

    private static RectTransform CreateStretchedChild(RectTransform Parent, string Name, Vector2 OffsetMin, Vector2 OffsetMax)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = Vector2.zero;
        Rect.anchorMax = Vector2.one;
        Rect.offsetMin = OffsetMin;
        Rect.offsetMax = OffsetMax;
        return Rect;
    }

    private static void Dress(Image Target, Image Reference, Color Colour)
    {
        if (Reference != null)
        {
            Target.sprite = Reference.sprite;
            Target.type = Reference.type;
        }
        Target.color = Colour;
    }
}
