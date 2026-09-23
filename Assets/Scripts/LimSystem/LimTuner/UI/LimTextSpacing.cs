using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Letter spacing, which uGUI's Text lacks: every character after the
/// first is moved along by a share of the font size, towards the side the
/// text is not aligned to, so a right-aligned score keeps its right edge.
/// The game's score is set wider than this face sets it at the same height.
/// Must come before any effect that cuts characters into more pieces
/// (<see cref="LimTextGradient"/>), as it counts six vertices a character.
/// </summary>
public class LimTextSpacing : BaseMeshEffect
{
    /// <summary>The extra room between two characters, as a share of the font size.</summary>
    public float Share = 0.1f;

    private readonly List<UIVertex> Vertices = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper Mesh)
    {
        if (!IsActive()) return;
        Text Label = graphic as Text;
        if (Label == null) return;
        float Space = Label.fontSize * Share;
        Vertices.Clear();
        Mesh.GetUIVertexStream(Vertices);
        int Characters = Vertices.Count / 6;
        if (Characters < 2) return;
        float Anchor = Label.alignment == TextAnchor.UpperRight || Label.alignment == TextAnchor.MiddleRight || Label.alignment == TextAnchor.LowerRight ? 1
            : Label.alignment == TextAnchor.UpperCenter || Label.alignment == TextAnchor.MiddleCenter || Label.alignment == TextAnchor.LowerCenter ? 0.5f : 0;
        for (int i = 0; i < Vertices.Count; ++i)
        {
            UIVertex Moved = Vertices[i];
            Moved.position.x += (i / 6 - Anchor * (Characters - 1)) * Space;
            Vertices[i] = Moved;
        }
        Mesh.Clear();
        Mesh.AddUIVertexTriangleStream(Vertices);
    }
}
