using UnityEngine;

/// <summary>
/// The judge line's two ornaments from Flowaria's UiTweak "Tuner Ornaments":
/// a glow over the line that pulses with the beat, and a patterned ring that
/// turns slowly on it. Both hang from the JudgeLine sprite, as in the plugin,
/// so they turn and scale with it.
///
/// The plugin ran the glow at the current bpm but from whenever it happened
/// to be created, so the pulse had the right speed and a random phase. Here
/// its time is taken from the chart: each pulse starts on a beat, counted the
/// way the beatlines are, and it holds still while the song is paused.
///
/// They fade with the transparency motion, like the rest of the ring. The
/// animations write the sprites' own colour, so the fade goes through each
/// sprite's material instead.
/// </summary>
public class LimJudgeLineOrnaments : MonoBehaviour
{
    public LimTunerManager Tuner;

    private GameObject Glow, Ornament;
    private Animator GlowAnimator;
    private int GlowState;
    private Renderer[] Renderers;
    private float AppliedFade = -1;

    private void LateUpdate()
    {
        if (Tuner == null || Tuner.BpmManager == null || Tuner.BpmManager.JudgeLine == null) return;
        if (!LimSystem.Preferences.JudgeLineOrnaments)
        {
            Remove();
            return;
        }
        if (!Ensure()) return;
        GlowAnimator.Play(GlowState, 0, LimChartClock.BeatPhase(Tuner.BpmManager, Tuner.ChartTime));
        Fade();
    }

    private bool Ensure()
    {
        if (Glow != null && Ornament != null) return true;
        Remove();
        GameObject GlowPrefab = LimUiTweakAssets.Load<GameObject>(LimUiTweakAssets.ScreenBundle, "JudgeLineGlow");
        GameObject OrnamentPrefab = LimUiTweakAssets.Load<GameObject>(LimUiTweakAssets.ScreenBundle, "JudgeLineOrnament");
        if (GlowPrefab == null || OrnamentPrefab == null)
        {
            // Nothing to show; the switch stays on and simply does nothing.
            enabled = false;
            return false;
        }
        Transform Judge = Tuner.BpmManager.JudgeLine;
        Glow = Instantiate(GlowPrefab, Judge, false);
        Glow.name = "JudgeLineGlow";
        Ornament = Instantiate(OrnamentPrefab, Judge, false);
        Ornament.name = "JudgeLineOrnament";
        GlowAnimator = Glow.GetComponent<Animator>();
        GlowAnimator.Update(0);
        GlowState = GlowAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        // Its time is set every frame from the chart, not run by the clock.
        GlowAnimator.speed = 0;
        Renderer[] OfGlow = Glow.GetComponentsInChildren<Renderer>(true);
        Renderer[] OfOrnament = Ornament.GetComponentsInChildren<Renderer>(true);
        Renderers = new Renderer[OfGlow.Length + OfOrnament.Length];
        OfGlow.CopyTo(Renderers, 0);
        OfOrnament.CopyTo(Renderers, OfGlow.Length);
        AppliedFade = -1;
        return true;
    }

    private void Remove()
    {
        if (Glow != null) Destroy(Glow);
        if (Ornament != null) Destroy(Ornament);
        Glow = Ornament = null;
        GlowAnimator = null;
        Renderers = null;
    }

    private void Fade()
    {
        float Value = Tuner.CameraManager != null ? Mathf.Clamp01(Tuner.CameraManager.CurrentTransparency / LimCameraManager.OpaqueTransparency) : 1;
        if (Value == AppliedFade) return;
        AppliedFade = Value;
        foreach (Renderer Part in Renderers)
        {
            if (Part == null) continue;
            Material Own = Part.material;
            if (Own.HasProperty("_Color")) Own.color = new Color(1, 1, 1, Value);
        }
    }
}
