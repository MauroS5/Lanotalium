using UnityEngine;

/// <summary>
/// The left-right arrow shown when the pointer is over the end of a motion
/// bar on the timeline.
///
/// Drawn in code rather than shipped as an image: it is a shaft with an arrow
/// head at each end, in white with a dark outline so it reads on both the
/// light and the dark parts of the editor.
/// </summary>
public static class LimResizeCursor
{
    private const int Size = 32;
    private const int Half = Size / 2;

    private static Texture2D _Texture;

    public static Texture2D Texture
    {
        get
        {
            if (_Texture == null) _Texture = Build();
            return _Texture;
        }
    }

    /// <summary>The middle of the arrow, which is what points at the edge.</summary>
    public static Vector2 Hotspot { get { return new Vector2(Half, Half); } }

    private static Texture2D Build()
    {
        Texture2D Result = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Result.filterMode = FilterMode.Point;
        Result.hideFlags = HideFlags.HideAndDontSave;

        Color[] Pixels = new Color[Size * Size];
        for (int i = 0; i < Pixels.Length; ++i) Pixels[i] = Color.clear;

        // The shaft, then a head growing out of each end.
        for (int x = 6; x < Size - 6; ++x) Paint(Pixels, x, Half, 1);
        for (int i = 0; i < 6; ++i)
        {
            Paint(Pixels, 5 + i, Half, i);
            Paint(Pixels, Size - 6 - i, Half, i);
        }

        // Anything next to a white pixel and still empty becomes the outline.
        Color[] Outlined = (Color[])Pixels.Clone();
        for (int y = 0; y < Size; ++y)
            for (int x = 0; x < Size; ++x)
                if (Pixels[y * Size + x].a == 0 && HasWhiteNeighbour(Pixels, x, y))
                    Outlined[y * Size + x] = new Color(0, 0, 0, 0.85f);

        Result.SetPixels(Outlined);
        Result.Apply();
        return Result;
    }

    /// <summary>Paints a vertical run of pixels centred on Y.</summary>
    private static void Paint(Color[] Pixels, int X, int Y, int Reach)
    {
        for (int y = Y - Reach; y <= Y + Reach; ++y)
        {
            if (X < 0 || X >= Size || y < 0 || y >= Size) continue;
            Pixels[y * Size + X] = Color.white;
        }
    }

    private static bool HasWhiteNeighbour(Color[] Pixels, int X, int Y)
    {
        for (int y = Y - 1; y <= Y + 1; ++y)
        {
            for (int x = X - 1; x <= X + 1; ++x)
            {
                if (x < 0 || x >= Size || y < 0 || y >= Size) continue;
                if (Pixels[y * Size + x].a > 0) return true;
            }
        }
        return false;
    }
}
