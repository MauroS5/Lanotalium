using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The blue wave along the bottom of Lanota's play screen, which rises and
/// falls with the beat, and the glowing specks that drift up from the bottom
/// and fade out at its height. Both are drawn over the tuner, as in the game.
///
/// The wave is Flowaria's "ScreenSpectrum" from UiTweak, which the plugin
/// never got round to using: the top strip of playscene.png, tinted
/// (0.58, 0.67, 1) at 39 per cent, 1024 x 392 units on a 743 unit tall
/// screen with its foot 100 units below the bottom edge, and a one second
/// animation that sinks it 40.8 units by 0.73 s and brings it back up. Here
/// those figures are fractions of the screen's height, and the animation's
/// second is one beat of the chart, counted like the beatlines. The picture
/// is made at least 16:9 wide and centred, so a narrower window cuts its ends
/// off rather than squeezing it.
///
/// The specks are not in UiTweak; they are made here from its soft round
/// "Knob" particle, additive, to match the game.
///
/// Both live on the tuner's FullScreenCanvas, which the tuner camera draws in
/// full screen and in the Tuner window alike. The wave holds still while the
/// song is paused, since its time is the chart's; the specks keep drifting,
/// as the ring keeps turning.
/// </summary>
public class LimPlaySceneWave : MonoBehaviour
{
    public LimTunerManager Tuner;

    private static readonly Color WaveColor = new Color(0.581f, 0.667f, 1f, 0.392f);
    /// <summary>Flowaria's figures on a 743 tall screen: 392 tall, foot at -100, sinking 40.757 at 0.733 of the cycle.</summary>
    private const float WaveHeight = 392f / 743f, WaveFoot = -100f / 743f, WaveSink = 40.757f / 743f, SinkAt = 0.733f;
    private const float WideAspect = 16f / 9f;
    /// <summary>How high the specks climb before they are gone: about where the wave's crest is.</summary>
    private const float SpeckRise = 0.18f, SpeckLife = 5f;

    private RectTransform Canvas;
    private RectTransform Wave;
    private ParticleSystem Specks;
    private bool Tried, Found;
    private float BuiltHeight = -1, BuiltWidth = -1;

    private void LateUpdate()
    {
        if (Tuner == null) return;
        if (!LimSystem.Preferences.BackgroundWave)
        {
            Remove();
            return;
        }
        if (!Ensure()) return;
        Rect Area = Canvas.rect;
        if (Area.height <= 0) return;
        if (Area.height != BuiltHeight || Area.width != BuiltWidth) Fit(Area);
        float Phase = Tuner.isInitialized ? LimChartClock.BeatPhase(Tuner.BpmManager, Tuner.ChartTime) : 0;
        Wave.anchoredPosition = new Vector2(0, (WaveFoot - WaveSink * Sink(Phase)) * Area.height);
    }

    /// <summary>
    /// 0 up, 1 sunk: down over the first 0.733 of the beat and back up over
    /// the rest, eased at both ends as the animation's curve is.
    /// </summary>
    private static float Sink(float Phase)
    {
        if (Phase < SinkAt) return (1 - Mathf.Cos(Mathf.PI * Phase / SinkAt)) / 2;
        return (1 + Mathf.Cos(Mathf.PI * (Phase - SinkAt) / (1 - SinkAt))) / 2;
    }

    private bool Ensure()
    {
        if (Wave != null && Specks != null) return true;
        if (Tried && !Found) return false;
        Tried = true;
        Transform Holder = Tuner.transform.Find("FullScreenCanvas");
        Canvas = Holder as RectTransform;
        Sprite Picture = LimUiTweakAssets.Load<Sprite>(LimUiTweakAssets.ScreenBundle, "playscene_0");
        Material Knob = LimUiTweakAssets.Load<Material>(LimUiTweakAssets.ParticleBundle, "Knob");
        Found = Canvas != null && Picture != null && Knob != null;
        if (!Found) return false;

        GameObject WaveObject = new GameObject("LanotaWave", typeof(RectTransform));
        WaveObject.layer = Canvas.gameObject.layer;
        Wave = WaveObject.GetComponent<RectTransform>();
        Wave.SetParent(Canvas, false);
        // Behind the header, which shares this canvas in full screen.
        Wave.SetAsFirstSibling();
        Wave.anchorMin = Wave.anchorMax = new Vector2(0.5f, 0);
        Wave.pivot = new Vector2(0.5f, 0);
        Image Look = WaveObject.AddComponent<Image>();
        Look.sprite = Picture;
        Look.color = WaveColor;
        Look.raycastTarget = false;

        GameObject SpeckObject = new GameObject("LanotaWaveSpecks", typeof(RectTransform));
        SpeckObject.layer = Canvas.gameObject.layer;
        RectTransform Place = SpeckObject.GetComponent<RectTransform>();
        Place.SetParent(Canvas, false);
        Place.anchorMin = Place.anchorMax = new Vector2(0.5f, 0);
        Place.anchoredPosition = Vector2.zero;
        Specks = SpeckObject.AddComponent<ParticleSystem>();
        Specks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer Draw = SpeckObject.GetComponent<ParticleSystemRenderer>();
        Draw.sharedMaterial = Knob;
        Draw.sortingLayerName = "EditorUI";
        Draw.sortingOrder = 1;
        BuiltHeight = BuiltWidth = -1;
        return true;
    }

    /// <summary>Sizes the wave and the specks to the canvas; again whenever the window changes.</summary>
    private void Fit(Rect Area)
    {
        BuiltHeight = Area.height;
        BuiltWidth = Area.width;
        float Height = Area.height;
        float Width = Mathf.Max(Area.width, Height * WideAspect);
        Wave.sizeDelta = new Vector2(Width, Height * WaveHeight);

        Specks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule Main = Specks.main;
        Main.loop = true;
        Main.playOnAwake = true;
        Main.duration = 5;
        Main.startLifetime = new ParticleSystem.MinMaxCurve(SpeckLife * 0.7f, SpeckLife);
        Main.startSpeed = 0;
        Main.startSize = new ParticleSystem.MinMaxCurve(Height * 0.006f, Height * 0.014f);
        Main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.88f, 1f, 0.55f), new Color(0.9f, 0.97f, 1f, 0.85f));
        Main.maxParticles = 40;
        Main.simulationSpace = ParticleSystemSimulationSpace.Local;
        Main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        Main.prewarm = true;

        ParticleSystem.EmissionModule Emission = Specks.emission;
        Emission.rateOverTime = 2.2f * Area.width / Height;

        ParticleSystem.ShapeModule SpeckShape = Specks.shape;
        SpeckShape.enabled = true;
        SpeckShape.shapeType = ParticleSystemShapeType.Box;
        SpeckShape.scale = new Vector3(Area.width, 1, 1);
        SpeckShape.position = new Vector3(0, -Height * 0.02f, 0);

        ParticleSystem.VelocityOverLifetimeModule Rise = Specks.velocityOverLifetime;
        Rise.enabled = true;
        Rise.space = ParticleSystemSimulationSpace.Local;
        float Climb = Height * SpeckRise / SpeckLife;
        Rise.x = new ParticleSystem.MinMaxCurve(-Climb * 0.1f, Climb * 0.1f);
        Rise.y = new ParticleSystem.MinMaxCurve(Climb * 0.7f, Climb * 1.2f);
        Rise.z = new ParticleSystem.MinMaxCurve(0, 0);

        ParticleSystem.ColorOverLifetimeModule Fade = Specks.colorOverLifetime;
        Fade.enabled = true;
        Gradient Alpha = new Gradient();
        Alpha.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0, 1) });
        Fade.color = Alpha;
        Specks.Play(true);
    }

    private void Remove()
    {
        if (Wave != null) Destroy(Wave.gameObject);
        if (Specks != null) Destroy(Specks.gameObject);
        Wave = null;
        Specks = null;
    }
}
