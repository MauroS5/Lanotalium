using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Highlighted (Combination) notes with their glow drawn close, as the game
/// draws them: a bright rim hugging the note rather than a wide halo.
///
/// A highlighted note's glow is its "Light" sprite, behind the note. The
/// editor's prefabs draw it at 1.55 x 1.4 against a note at 1.2, which leaves
/// a broad band of light above and below; Flowaria's highlighted flicks had
/// it at 1.28 x 1.18 against 1. This draws every highlighted tap and hold
/// head's glow at 94 per cent of its width and 80 per cent of its height,
/// which brings the halo in to about that. Each glow's own scale is kept, so
/// turning it off puts every one back; a note rebuilt by an edit is caught
/// the frame after.
/// </summary>
public class LimCompactHighlight : MonoBehaviour
{
    public LimTunerManager Tuner;

    private static readonly Vector3 Squeeze = new Vector3(0.94f, 0.8f, 1);
    private readonly Dictionary<Transform, Vector3> Squeezed = new Dictionary<Transform, Vector3>();
    private readonly List<Transform> Gone = new List<Transform>();

    private void LateUpdate()
    {
        if (Tuner == null || !Tuner.isInitialized || Tuner.TapNoteManager == null || Tuner.HoldNoteManager == null) return;
        if (!LimSystem.Preferences.CompactHighlight)
        {
            if (Squeezed.Count != 0) Release();
            return;
        }
        foreach (Lanotalium.Chart.LanotaTapNote Note in Tuner.TapNoteManager.TapNote)
            if (Note.Combination && Note.TapNoteGameObject != null) Hold(Note.TapNoteGameObject.transform);
        foreach (Lanotalium.Chart.LanotaHoldNote Note in Tuner.HoldNoteManager.HoldNote)
            if (Note.Combination && Note.HoldNoteGameObject != null) Hold(Note.HoldNoteGameObject.transform);
        Forget();
    }

    private void Hold(Transform Note)
    {
        Transform Light = Note.Find("Light");
        if (Light == null || Squeezed.ContainsKey(Light)) return;
        Vector3 Own = Light.localScale;
        Squeezed[Light] = Own;
        Light.localScale = Vector3.Scale(Own, Squeeze);
    }

    private void Release()
    {
        foreach (KeyValuePair<Transform, Vector3> Entry in Squeezed) if (Entry.Key != null) Entry.Key.localScale = Entry.Value;
        Squeezed.Clear();
    }

    /// <summary>Glows whose note was rebuilt or deleted.</summary>
    private void Forget()
    {
        Gone.Clear();
        foreach (Transform Light in Squeezed.Keys) if (Light == null) Gone.Add(Light);
        foreach (Transform Light in Gone) Squeezed.Remove(Light);
    }
}
