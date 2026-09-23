using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Perfect Purified", Lanota's all-perfect finish, shown when playback
/// passes the end of the chart's last note: the moment the score reaches a
/// million, every note being a perfect hit in the editor.
///
/// Flowaria drew the pieces for UiTweak (the words, the pair of gears with
/// their orbit, a field of stars, a star particle, the "all combo" sound)
/// but left its animation empty, so the animation is made here: the stars
/// and the gears come in, the words land on them with a small bounce and a
/// burst of stars, and after three seconds it all fades. It runs on real
/// time, like the game's result screen, whatever the playback does, and is
/// taken off at once if the playhead is moved back before the end.
///
/// It lives on the tuner's FullScreenCanvas like the wave, so it shows in the
/// Tuner window and in full screen.
/// </summary>
public class LimPerfectPurified : MonoBehaviour
{
    public LimTunerManager Tuner;

    private const float JumpAllowance = 0.1f, Length = 4f, FadeFrom = 3.2f;

    private RectTransform Canvas, Root, Glow, Shape, Words;
    private CanvasGroup Fade;
    private Image GlowImage, ShapeImage, WordsImage;
    private ParticleSystem Burst;
    private AudioSource Sound;
    private Sprite GlowSprite, ShapeSprite, WordsSprite;
    private Material StarMaterial;
    private AudioClip Fanfare;
    private bool Tried, Found, BurstDone;
    private float Started = -1, LastTime;
    private bool WasPlaying;

    private void LateUpdate()
    {
        if (Tuner == null || !Tuner.isInitialized || Tuner.MediaPlayerManager == null) return;
        float Now = Tuner.ChartTime;
        bool Playing = Tuner.MediaPlayerManager.IsPlaying;
        float End = ChartEnd();
        if (Started >= 0)
        {
            // Moved back before the end: it has not happened yet.
            if (Now < End - JumpAllowance) Stop();
            else Animate(Time.unscaledTime - Started);
        }
        if (LimSystem.Preferences.PerfectPurified && Playing && WasPlaying && End > 0
            && LastTime < End && Now >= End && Now - LastTime <= Time.unscaledDeltaTime * 3 + JumpAllowance)
            Play();
        LastTime = Now;
        WasPlaying = Playing;
    }

    /// <summary>When the last note is done: the latest tap, or the latest end of a hold.</summary>
    private float ChartEnd()
    {
        float End = 0;
        foreach (Lanotalium.Chart.LanotaTapNote Note in Tuner.TapNoteManager.TapNote) End = Mathf.Max(End, Note.Time);
        foreach (Lanotalium.Chart.LanotaHoldNote Note in Tuner.HoldNoteManager.HoldNote) End = Mathf.Max(End, Note.Time + Note.Duration);
        return End;
    }

    private bool Load()
    {
        if (Tried) return Found;
        Tried = true;
        string B = LimUiTweakAssets.ScreenBundle;
        GlowSprite = LimUiTweakAssets.Load<Sprite>(B, "PPGlow");
        ShapeSprite = LimUiTweakAssets.Load<Sprite>(B, "PPShape");
        WordsSprite = LimUiTweakAssets.Load<Sprite>(B, "PPText");
        StarMaterial = LimUiTweakAssets.Load<Material>(B, "PPStar");
        Fanfare = LimUiTweakAssets.Load<AudioClip>(B, "allcombo");
        Canvas = Tuner.transform.Find("FullScreenCanvas") as RectTransform;
        Found = GlowSprite != null && ShapeSprite != null && WordsSprite != null && StarMaterial != null && Canvas != null;
        return Found;
    }

    private void Play()
    {
        if (!Load()) return;
        Stop();
        Build();
        Started = Time.unscaledTime;
        BurstDone = false;
        if (Fanfare != null)
        {
            if (Sound == null) Sound = gameObject.AddComponent<AudioSource>();
            Sound.playOnAwake = false;
            Sound.clip = Fanfare;
            Sound.volume = Tuner.MediaPlayerManager.MusicPlayer != null ? Tuner.MediaPlayerManager.MusicPlayer.volume : 1;
            Sound.Play();
        }
        Animate(0);
    }

    private void Stop()
    {
        Started = -1;
        if (Root != null) Destroy(Root.gameObject);
        Root = null;
        if (Sound != null) Sound.Stop();
    }

    private void Build()
    {
        GameObject Holder = new GameObject("PerfectPurified", typeof(RectTransform));
        Holder.layer = Canvas.gameObject.layer;
        Root = Holder.GetComponent<RectTransform>();
        Root.SetParent(Canvas, false);
        Root.anchorMin = Vector2.zero;
        Root.anchorMax = Vector2.one;
        Root.sizeDelta = Vector2.zero;
        Fade = Holder.AddComponent<CanvasGroup>();
        Fade.blocksRaycasts = false;
        Fade.interactable = false;
        float Height = Canvas.rect.height;
        Glow = Piece("Stars", GlowSprite, Height * 0.62f, out GlowImage);
        Shape = Piece("Gears", ShapeSprite, Height * 0.4f, out ShapeImage);
        Words = Piece("Words", WordsSprite, Height * 0.36f, out WordsImage);

        GameObject Stars = new GameObject("Burst", typeof(RectTransform));
        Stars.layer = Holder.layer;
        Stars.transform.SetParent(Root, false);
        Burst = Stars.AddComponent<ParticleSystem>();
        Burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule Main = Burst.main;
        Main.loop = false;
        Main.playOnAwake = false;
        Main.duration = 1;
        Main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        Main.startSpeed = new ParticleSystem.MinMaxCurve(Height * 0.25f, Height * 0.7f);
        Main.startSize = new ParticleSystem.MinMaxCurve(Height * 0.03f, Height * 0.08f);
        Main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        Main.simulationSpace = ParticleSystemSimulationSpace.Local;
        Main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        Main.useUnscaledTime = true;
        ParticleSystem.EmissionModule Emission = Burst.emission;
        Emission.rateOverTime = 0;
        Emission.SetBursts(new[] { new ParticleSystem.Burst(0, 36) });
        ParticleSystem.ShapeModule Spread = Burst.shape;
        Spread.shapeType = ParticleSystemShapeType.Circle;
        Spread.radius = Height * 0.05f;
        ParticleSystem.LimitVelocityOverLifetimeModule Slow = Burst.limitVelocityOverLifetime;
        Slow.enabled = true;
        Slow.limit = Height * 0.05f;
        Slow.dampen = 0.15f;
        ParticleSystem.ColorOverLifetimeModule Dim = Burst.colorOverLifetime;
        Dim.enabled = true;
        Gradient Alpha = new Gradient();
        Alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.5f), new GradientAlphaKey(0, 1) });
        Dim.color = Alpha;
        ParticleSystemRenderer Draw = Stars.GetComponent<ParticleSystemRenderer>();
        Draw.sharedMaterial = StarMaterial;
        Draw.sortingLayerName = "EditorUI";
        Draw.sortingOrder = 2;
    }

    private RectTransform Piece(string Name, Sprite Picture, float Height, out Image Look)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        Holder.layer = Root.gameObject.layer;
        RectTransform Place = Holder.GetComponent<RectTransform>();
        Place.SetParent(Root, false);
        Place.anchorMin = Place.anchorMax = new Vector2(0.5f, 0.5f);
        Place.sizeDelta = new Vector2(Height * Picture.rect.width / Picture.rect.height, Height);
        Look = Holder.AddComponent<Image>();
        Look.sprite = Picture;
        Look.raycastTarget = false;
        return Place;
    }

    /// <summary>Where everything is at a time after the start, in seconds.</summary>
    private void Animate(float T)
    {
        if (Root == null) return;
        if (T >= Length)
        {
            Stop();
            return;
        }
        float GlowIn = Ease(T / 0.5f);
        GlowImage.color = new Color(1, 1, 1, 0.9f * GlowIn);
        Glow.localScale = Vector3.one * (0.8f + 0.3f * GlowIn + 0.05f * T);

        float ShapeIn = Ease(T / 0.35f);
        ShapeImage.color = new Color(1, 1, 1, ShapeIn);
        Shape.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, ShapeIn);

        float WordsIn = Mathf.Clamp01((T - 0.15f) / 0.35f);
        WordsImage.color = new Color(1, 1, 1, Ease(WordsIn));
        Words.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Bounce(WordsIn));

        if (!BurstDone && T >= 0.4f)
        {
            BurstDone = true;
            Burst.Play(true);
        }
        Fade.alpha = T < FadeFrom ? 1 : 1 - (T - FadeFrom) / (Length - FadeFrom);
    }

    private static float Ease(float X)
    {
        X = Mathf.Clamp01(X);
        return 1 - (1 - X) * (1 - X) * (1 - X);
    }

    /// <summary>0 to 1, overshooting a little before settling: the words' landing.</summary>
    private static float Bounce(float X)
    {
        X = Mathf.Clamp01(X);
        const float Over = 1.7f;
        float Y = X - 1;
        return 1 + Y * Y * ((Over + 1) * Y + Over);
    }
}
