using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The moving arrow on flick notes, as Lanota draws it: a fixed square window
/// that starts at the note's middle and reaches towards the core, through
/// which the chevron keeps sliding the way the finger has to go; whatever
/// leaves by one edge comes back in by the other, so the arrows seem to pour
/// through without end. Flick In slides towards the core, Flick Out away from
/// it. The pictures and the speed (1.5 x 1.3 of the window a second) are
/// Flowaria's, from AUTO_FlickArrow.
///
/// That plugin had it right but for one setting: its sprites had a tight
/// mesh, cut to the chevron's outline, so the sliding picture was only seen
/// inside a chevron-shaped hole and came out in pieces. Here the sprite is
/// made again with a full rectangle, over a texture that repeats, on the
/// UI/Default shader, which applies a texture offset (Sprites/Default does
/// not). The plugin also drew every arrow at the same size; here the window
/// is sized from the note, 0.42 of its width as in the game, so it grows with
/// Size 1 to 3.
///
/// The editor's prefabs stay; the arrow is hung on each flick note the frame
/// after it exists, so a note rebuilt by an edit gets it back. Four shared
/// materials carry four starting points of the slide, so neighbouring notes
/// are not in step and no note needs a material of its own. A time group's
/// fade reaches it through its sprite colour, like the note's other sprites.
/// </summary>
public class LimFlickArrows : MonoBehaviour
{
    public LimTunerManager Tuner;

    private const string ArrowName = "FlickArrow";
    private const float Speed = 1.5f * 1.3f;
    private const int Variants = 4;
    /// <summary>The window's side against the note's width, and a size 0 flick's width if a note has no picture to measure.</summary>
    private const float WindowShare = 0.42f, ReferenceWidth = 2.55f;

    private Sprite InArrow, OutArrow;
    private Material[] InMaterials, OutMaterials;
    private bool Loaded, Missing;
    private readonly HashSet<int> Dressed = new HashSet<int>();
    private readonly List<GameObject> Arrows = new List<GameObject>();

    private void LateUpdate()
    {
        if (Tuner == null || !Tuner.isInitialized || Tuner.TapNoteManager == null || Tuner.TapNoteManager.TapNote == null) return;
        if (!LimSystem.Preferences.FlickArrows)
        {
            if (Dressed.Count != 0) Undress();
            return;
        }
        if (!Load()) return;
        Slide(InMaterials, -1);
        Slide(OutMaterials, 1);
        foreach (Lanotalium.Chart.LanotaTapNote Note in Tuner.TapNoteManager.TapNote)
        {
            if ((Note.Type != 2 && Note.Type != 3) || Note.TapNoteGameObject == null) continue;
            if (Dressed.Contains(Note.TapNoteGameObject.GetInstanceID())) continue;
            Dress(Note);
        }
    }

    private bool Load()
    {
        if (Loaded) return true;
        if (Missing) return false;
        Sprite In = Resources.Load<Sprite>("UiTweak/flick_in_arrow");
        Sprite Out = Resources.Load<Sprite>("UiTweak/flick_out_arrow");
        Shader Scrolling = Shader.Find("UI/Default");
        if (In == null || Out == null || Scrolling == null)
        {
            Missing = true;
            Debug.LogWarning("UiTweak: the flick arrows could not be loaded");
            return false;
        }
        InArrow = FullWindow(In);
        OutArrow = FullWindow(Out);
        InMaterials = MakeMaterials(Scrolling, "FlickInArrow");
        OutMaterials = MakeMaterials(Scrolling, "FlickOutArrow");
        Loaded = true;
        return true;
    }

    /// <summary>
    /// The same picture as a full rectangle, its foot at the note's middle,
    /// one unit square, over a texture that wraps round.
    /// </summary>
    private static Sprite FullWindow(Sprite Source)
    {
        Texture2D Picture = Source.texture;
        Picture.wrapMode = TextureWrapMode.Repeat;
        return Sprite.Create(Picture, new Rect(0, 0, Picture.width, Picture.height), new Vector2(0.5f, 0), Picture.width, 0, SpriteMeshType.FullRect);
    }

    private static Material[] MakeMaterials(Shader Scrolling, string Name)
    {
        Material[] Made = new Material[Variants];
        for (int i = 0; i < Variants; ++i) Made[i] = new Material(Scrolling) { name = Name + i };
        return Made;
    }

    private static void Slide(Material[] Materials, float Direction)
    {
        float Offset = Time.time * Speed * Direction;
        for (int i = 0; i < Materials.Length; ++i) Materials[i].SetTextureOffset("_MainTex", new Vector2(0, Offset + (float)i / Materials.Length));
    }

    private void Dress(Lanotalium.Chart.LanotaTapNote Note)
    {
        GameObject Root = Note.TapNoteGameObject;
        bool In = Note.Type == 2;

        // The note's own picture says how wide it is.
        float Width = ReferenceWidth;
        Transform Body = Root.transform.Find("Note");
        SpriteRenderer BodySprite = Body != null ? Body.GetComponent<SpriteRenderer>() : null;
        if (BodySprite != null && BodySprite.sprite != null) Width = BodySprite.sprite.bounds.size.x * Body.localScale.x;
        float Side = Width * WindowShare;

        GameObject Made = new GameObject(ArrowName);
        Made.layer = Root.layer;
        Made.transform.SetParent(Root.transform, false);
        // The root's +y points at the core: the window reaches inwards from the note's middle.
        Made.transform.localScale = new Vector3(Side, Side, 1);
        SpriteRenderer Look = Made.AddComponent<SpriteRenderer>();
        Look.sprite = In ? InArrow : OutArrow;
        Look.sharedMaterial = (In ? InMaterials : OutMaterials)[Random.Range(0, Variants)];
        Look.sortingLayerName = "Note";
        // Over the note, and over a highlighted note's glow.
        Look.sortingOrder = Note.Combination ? 2 : 1;

        Arrows.Add(Made);
        Dressed.Add(Root.GetInstanceID());
        LimTimeGroups.ForgetExtras(Root);
    }

    private void Undress()
    {
        foreach (GameObject Arrow in Arrows)
        {
            if (Arrow == null) continue;
            GameObject Root = Arrow.transform.parent != null ? Arrow.transform.parent.gameObject : null;
            DestroyImmediate(Arrow);
            LimTimeGroups.ForgetExtras(Root);
        }
        Arrows.Clear();
        Dressed.Clear();
    }
}
