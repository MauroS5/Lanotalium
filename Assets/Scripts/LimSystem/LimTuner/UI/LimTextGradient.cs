using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Paints each character of a Text with colours running down it, from its
/// top to its foot, like the lettering in Lanota's header. From Flowaria's
/// UiTweak ("EffectTest", which took a whole Gradient and a range of
/// characters and dirtied the mesh every frame, shading each letter on its
/// own); here a few colour stops over the whole line, and a rebuild only
/// when the text itself changes.
/// </summary>
public class LimTextGradient : BaseMeshEffect
{
    /// <summary>Colours from the top of a character (at 0) to its foot (at 1).</summary>
    public Color[] Colours = { Color.white, Color.white };
    /// <summary>Where each colour sits, 0 to 1, in step with <see cref="Colours"/>; evenly spread when left empty.</summary>
    public float[] At;

    private readonly List<UIVertex> Vertices = new List<UIVertex>();

    public void Set(Color[] NewColours, float[] NewAt)
    {
        Colours = NewColours;
        At = NewAt;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    private Color Sample(float T)
    {
        if (Colours == null || Colours.Length == 0) return Color.white;
        if (Colours.Length == 1) return Colours[0];
        for (int i = 1; i < Colours.Length; ++i)
        {
            float From = Position(i - 1), To = Position(i);
            if (T <= To || i == Colours.Length - 1)
                return Color.Lerp(Colours[i - 1], Colours[i], To > From ? Mathf.Clamp01((T - From) / (To - From)) : 1);
        }
        return Colours[Colours.Length - 1];
    }

    private float Position(int Index)
    {
        if (At != null && Index < At.Length) return At[Index];
        return (float)Index / (Colours.Length - 1);
    }

    private readonly List<UIVertex> Sliced = new List<UIVertex>();
    private readonly List<float> Cuts = new List<float>();

    /// <summary>
    /// A letter is one quad, and a quad only blends the colours of its four
    /// corners, which would flatten the stops into a single straight blend.
    /// So each letter is cut into horizontal bands at the stops that cross
    /// it, and every band's corners take the colour for their own height.
    /// </summary>
    public override void ModifyMesh(VertexHelper Mesh)
    {
        if (!IsActive()) return;
        Vertices.Clear();
        Mesh.GetUIVertexStream(Vertices);
        // Over the whole line, so lower-case letters take the lower part of
        // the shading just as they sit lower than the capitals.
        float High = float.MinValue, Low = float.MaxValue;
        foreach (UIVertex Vertex in Vertices)
        {
            High = Mathf.Max(High, Vertex.position.y);
            Low = Mathf.Min(Low, Vertex.position.y);
        }
        Sliced.Clear();
        // Six vertices to a letter: top left, top right, bottom right, bottom right, bottom left, top left.
        for (int Start = 0; Start + 6 <= Vertices.Count; Start += 6)
        {
            UIVertex TopLeft = Vertices[Start], TopRight = Vertices[Start + 1], BottomRight = Vertices[Start + 2], BottomLeft = Vertices[Start + 4];
            float Top = Mathf.Max(TopLeft.position.y, TopRight.position.y), Bottom = Mathf.Min(BottomLeft.position.y, BottomRight.position.y);
            Cuts.Clear();
            Cuts.Add(0);
            for (int i = 0; i < Colours.Length; ++i)
            {
                float Y = Mathf.Lerp(High, Low, Position(i));
                if (Y < Top && Y > Bottom) Cuts.Add((Top - Y) / (Top - Bottom));
            }
            Cuts.Add(1);
            for (int i = 0; i + 1 < Cuts.Count; ++i)
            {
                UIVertex A = Shaded(Blend(TopLeft, BottomLeft, Cuts[i]), High, Low), B = Shaded(Blend(TopRight, BottomRight, Cuts[i]), High, Low);
                UIVertex C = Shaded(Blend(TopRight, BottomRight, Cuts[i + 1]), High, Low), D = Shaded(Blend(TopLeft, BottomLeft, Cuts[i + 1]), High, Low);
                Sliced.Add(A); Sliced.Add(B); Sliced.Add(C);
                Sliced.Add(C); Sliced.Add(D); Sliced.Add(A);
            }
        }
        Mesh.Clear();
        Mesh.AddUIVertexTriangleStream(Sliced);
    }

    private UIVertex Shaded(UIVertex Vertex, float High, float Low)
    {
        Color Shade = Sample(Mathf.InverseLerp(High, Low, Vertex.position.y));
        Color Own = Vertex.color;
        Vertex.color = new Color(Own.r * Shade.r, Own.g * Shade.g, Own.b * Shade.b, Own.a * Shade.a);
        return Vertex;
    }

    private static UIVertex Blend(UIVertex From, UIVertex To, float T)
    {
        UIVertex Made = From;
        Made.position = Vector3.Lerp(From.position, To.position, T);
        Made.uv0 = Vector2.Lerp(From.uv0, To.uv0, T);
        Made.uv1 = Vector2.Lerp(From.uv1, To.uv1, T);
        Made.color = Color32.Lerp(From.color, To.color, T);
        return Made;
    }
}
