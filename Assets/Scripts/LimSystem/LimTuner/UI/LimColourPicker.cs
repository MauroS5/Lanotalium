using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A colour picker built at runtime: a square of saturation (left to right)
/// and brightness (bottom to top) for the chosen hue, and beside it a strip
/// of every hue, red at the top running through magenta, blue, cyan, green
/// and yellow back to red, like the pickers most programs have. Drag in
/// either to choose; <see cref="Changed"/> hears every change.
/// </summary>
public class LimColourPicker : MonoBehaviour
{
    public Action<Color> Changed;

    private float Hue, Saturation = 1, Brightness = 1;
    private RectTransform Square, Strip, SquareMark, StripMark;
    private Texture2D SquareTexture;
    private const int SquareResolution = 64;
    private static Texture2D StripTexture;
    private static Sprite RingSprite;

    public Color Value
    {
        get { return Color.HSVToRGB(Hue, Saturation, Brightness); }
        set
        {
            Color.RGBToHSV(value, out Hue, out Saturation, out Brightness);
            Redraw(true);
        }
    }

    /// <summary>A picker filling Parent: the square as tall as Parent, the strip StripWidth wide beside it.</summary>
    public static LimColourPicker Create(RectTransform Parent, float StripWidth, float Gap)
    {
        LimColourPicker Picker = Parent.gameObject.AddComponent<LimColourPicker>();
        float Side = Parent.rect.height;
        Picker.Square = Picker.NewArea("Square", Parent, 0, Side, (Vector2 At) =>
        {
            Picker.Saturation = At.x;
            Picker.Brightness = At.y;
            Picker.Redraw(false);
        });
        Picker.Strip = Picker.NewArea("Hue", Parent, Side + Gap, StripWidth, (Vector2 At) =>
        {
            Picker.Hue = Mathf.Clamp(At.y, 0, 0.9999f);
            Picker.Redraw(true);
        });
        Picker.SquareTexture = new Texture2D(SquareResolution, SquareResolution, TextureFormat.RGB24, false);
        Picker.SquareTexture.wrapMode = TextureWrapMode.Clamp;
        Picker.Square.GetComponent<RawImage>().texture = Picker.SquareTexture;
        Picker.Strip.GetComponent<RawImage>().texture = HueStrip();

        // A ring on the square, a bar across the strip; both white edged in dark.
        Picker.SquareMark = NewMark("Mark", Picker.Square, new Vector2(14, 14));
        Image Ring = Picker.SquareMark.GetComponent<Image>();
        Ring.sprite = MakeRing();
        Picker.StripMark = NewMark("Mark", Picker.Strip, new Vector2(StripWidth + 6, 4));
        Picker.StripMark.anchorMin = new Vector2(0.5f, 0);
        Picker.StripMark.anchorMax = new Vector2(0.5f, 0);
        Picker.Redraw(true);
        return Picker;
    }

    private RectTransform NewArea(string Name, RectTransform Parent, float X, float Width, Action<Vector2> Picked)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        Holder.layer = Parent.gameObject.layer;
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = new Vector2(0, 0);
        Rect.anchorMax = new Vector2(0, 1);
        Rect.pivot = new Vector2(0, 0.5f);
        Rect.sizeDelta = new Vector2(Width, 0);
        Rect.anchoredPosition = new Vector2(X, 0);
        Holder.AddComponent<RawImage>();
        Outline Edge = Holder.AddComponent<Outline>();
        Edge.effectColor = new Color(0, 0, 0, 0.6f);
        Edge.effectDistance = new Vector2(1, -1);
        Holder.AddComponent<LimPickerArea>().Picked = Picked;
        return Rect;
    }

    private static RectTransform NewMark(string Name, RectTransform Parent, Vector2 Size)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        Holder.layer = Parent.gameObject.layer;
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = Rect.anchorMax = Vector2.zero;
        Rect.pivot = new Vector2(0.5f, 0.5f);
        Rect.sizeDelta = Size;
        Image Look = Holder.AddComponent<Image>();
        Look.color = Color.white;
        Look.raycastTarget = false;
        Outline Edge = Holder.AddComponent<Outline>();
        Edge.effectColor = new Color(0, 0, 0, 0.8f);
        Edge.effectDistance = new Vector2(1, -1);
        return Rect;
    }

    private void Redraw(bool HueChanged)
    {
        if (Square == null) return;
        if (HueChanged)
        {
            Color[] Pixels = new Color[SquareResolution * SquareResolution];
            for (int Y = 0; Y < SquareResolution; ++Y)
                for (int X = 0; X < SquareResolution; ++X)
                    Pixels[Y * SquareResolution + X] = Color.HSVToRGB(Hue, X / (SquareResolution - 1f), Y / (SquareResolution - 1f));
            SquareTexture.SetPixels(Pixels);
            SquareTexture.Apply();
        }
        Rect Area = Square.rect;
        SquareMark.anchoredPosition = new Vector2(Saturation * Area.width, Brightness * Area.height);
        StripMark.anchoredPosition = new Vector2(0, Hue * Strip.rect.height);
        if (Changed != null) Changed(Value);
    }

    private static Texture2D HueStrip()
    {
        if (StripTexture != null) return StripTexture;
        const int Tall = 180;
        StripTexture = new Texture2D(1, Tall, TextureFormat.RGB24, false);
        StripTexture.wrapMode = TextureWrapMode.Clamp;
        for (int Y = 0; Y < Tall; ++Y) StripTexture.SetPixel(0, Y, Color.HSVToRGB(Y / (Tall - 1f) * 0.9999f, 1, 1));
        StripTexture.Apply();
        return StripTexture;
    }

    private static Sprite MakeRing()
    {
        if (RingSprite != null) return RingSprite;
        const int Side = 32;
        Texture2D Picture = new Texture2D(Side, Side, TextureFormat.RGBA32, false);
        for (int Y = 0; Y < Side; ++Y)
        {
            for (int X = 0; X < Side; ++X)
            {
                float Distance = Vector2.Distance(new Vector2(X + 0.5f, Y + 0.5f), new Vector2(Side / 2f, Side / 2f));
                float Alpha = Mathf.Clamp01(1.5f - Mathf.Abs(Distance - Side * 0.36f) / 1.5f);
                Picture.SetPixel(X, Y, new Color(1, 1, 1, Alpha));
            }
        }
        Picture.Apply();
        RingSprite = Sprite.Create(Picture, new Rect(0, 0, Side, Side), new Vector2(0.5f, 0.5f), 100);
        return RingSprite;
    }
}

/// <summary>Reports where in its rect the pointer is pressed or dragged, as 0 to 1 on each axis.</summary>
public class LimPickerArea : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public Action<Vector2> Picked;

    public void OnPointerDown(PointerEventData Data) { Report(Data); }
    public void OnDrag(PointerEventData Data) { Report(Data); }

    private void Report(PointerEventData Data)
    {
        RectTransform Rect = transform as RectTransform;
        Vector2 Local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, Data.position, Data.pressEventCamera, out Local)) return;
        Rect Area = Rect.rect;
        Vector2 At = new Vector2(Mathf.Clamp01((Local.x - Area.xMin) / Area.width), Mathf.Clamp01((Local.y - Area.yMin) / Area.height));
        if (Picked != null) Picked(At);
    }
}
