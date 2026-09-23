using UnityEngine;

/// <summary>
/// Flowaria's sharper rail bodies from UiTweak's Rails folder: the pressed
/// body brighter and four times as fine across, and the unpressed one blue
/// with the dark edges the game draws, where the editor's is a flat blue.
///
/// The hold note manager draws every rail with two shared materials,
/// HoldTouch while the rail is being held and HoldUntouch otherwise; turning
/// this on hands it two others with the new pictures, and turning it off
/// gives it its own back. A time group's faded copy of a rail material is
/// made from whichever material it finds, so it follows too.
///
/// The pictures are turned a quarter round from Flowaria's: they ran along
/// the texture's height, for a shader ("Affine UV fix") that divided by a
/// second set of UVs a LineRenderer never provides. A LineRenderer runs its
/// texture along the width, as the editor's own rail picture does, so they
/// were rotated to match and are drawn with the editor's own pressed-rail
/// shader.
/// </summary>
public class LimHdRails : MonoBehaviour
{
    public LimTunerManager Tuner;

    private Material OwnTouch, OwnUntouch, HdTouch, HdUntouch;
    private bool Applied, Missing;

    private void LateUpdate()
    {
        if (Tuner == null || Tuner.HoldNoteManager == null) return;
        LimHoldNoteManager Holds = Tuner.HoldNoteManager;
        bool Wanted = LimSystem.Preferences.HdRails && Make(Holds);
        if (Wanted == Applied) return;
        if (Wanted)
        {
            OwnTouch = Holds.HoldTouch;
            OwnUntouch = Holds.HoldUntouch;
            Holds.HoldTouch = HdTouch;
            Holds.HoldUntouch = HdUntouch;
        }
        else
        {
            Holds.HoldTouch = OwnTouch;
            Holds.HoldUntouch = OwnUntouch;
        }
        Applied = Wanted;
        Repaint(Holds, Wanted ? OwnTouch : HdTouch, Wanted ? OwnUntouch : HdUntouch);
    }

    private bool Make(LimHoldNoteManager Holds)
    {
        if (HdTouch != null && HdUntouch != null) return true;
        if (Missing) return false;
        Texture2D Pressed = Resources.Load<Texture2D>("UiTweak/rail_pressed");
        Texture2D Unpressed = Resources.Load<Texture2D>("UiTweak/rail_unpressed");
        Material Pattern = Holds.HoldTouch;
        if (Pressed == null || Unpressed == null || Pattern == null)
        {
            Missing = true;
            Debug.LogWarning("UiTweak: the HD rail pictures could not be loaded");
            return false;
        }
        HdTouch = new Material(Pattern) { name = "HoldTouchHD", mainTexture = Pressed, color = Color.white };
        HdUntouch = new Material(Pattern) { name = "HoldUntouchHD", mainTexture = Unpressed, color = Color.white };
        return true;
    }

    /// <summary>Rails already drawn keep the old material until they change state; they are given the new one now.</summary>
    private static void Repaint(LimHoldNoteManager Holds, Material OldTouch, Material OldUntouch)
    {
        if (Holds.HoldNote == null) return;
        foreach (Lanotalium.Chart.LanotaHoldNote Note in Holds.HoldNote)
        {
            if (Note.LineRenderer == null) continue;
            Material Current = Note.LineRenderer.sharedMaterial;
            if (Current == OldTouch) Note.LineRenderer.sharedMaterial = Holds.HoldTouch;
            else if (Current == OldUntouch) Note.LineRenderer.sharedMaterial = Holds.HoldUntouch;
        }
    }
}
