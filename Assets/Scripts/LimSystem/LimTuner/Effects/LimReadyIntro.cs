using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lanota's "Ready" before a song: a dark band opening across the middle of
/// the screen and the word blinking over a thin gold line, for four seconds,
/// before the music starts.
///
/// The game shows it before the audio begins; a song file has no room for
/// that unless silence is added to it. So nothing is added: when playback is
/// started from the very beginning, the song is held there while "Ready"
/// plays, and started when it ends. Pressing play again during it skips
/// it; moving the playhead, or pausing, calls it off. The first playback,
/// which the editor starts by itself when a project opens, is left alone.
///
/// The animation is Flowaria's, from UiTweak's ScreenTweak, which had it but
/// no prefab for it: "ReadyScale" opens the band (the playscene sheet's
/// second strip) from nothing to its full height over the first second, and
/// "ReadyAlpha" brings the word and the line in by 0.5 s, blinks them to 0.6
/// and back every half second, and takes them out at 4 s. The band closes as
/// the word goes. Sizes are fractions of the screen's height, from Flowaria's
/// figures on a 743 unit tall screen.
/// </summary>
public class LimReadyIntro : MonoBehaviour
{
    public LimTunerManager Tuner;

    private const float Length = 4f, AtStart = 0.05f;
    private static readonly float[] BlinkTimes = { 0, 0.5f, 1, 1.5f, 2, 2.5f, 3, 3.5f, 4 };
    private static readonly float[] BlinkValues = { 0, 1, 0.6f, 1, 0.6f, 1, 0.6f, 1, 0 };
    /// <summary>The band: 130 x 1.59 units of 743; the word and the line under it.</summary>
    private const float BandHeight = 206.7f / 743f, WordHeight = 0.1f, LineWidth = 0.62f, LineHeight = 0.008f, LineBelow = 0.075f;

    private LimOnPlayHook Hook;
    private RectTransform Canvas, Root, Band;
    private Image BandImage, Word, Line;
    private Sprite BandSprite, WordSprite, LineSprite;
    private bool Tried, Found, Starting;
    private float Started = -1;

    private void LateUpdate()
    {
        if (Tuner == null || Tuner.MediaPlayerManager == null) return;
        if (Hook == null || !Hook.Current) Hook = new LimOnPlayHook(OnPlay);
        if (Started < 0) return;
        LimMediaPlayerManager Player = Tuner.MediaPlayerManager;
        // Called off by anything that moves the song from its start.
        if (Player.IsPlaying || Mathf.Abs(Player.Time) > AtStart || !LimSystem.Preferences.ReadyIntro)
        {
            Stop();
            return;
        }
        float T = Time.unscaledTime - Started;
        if (T >= Length)
        {
            Stop();
            Starting = true;
            Player.IsPlaying = true;
            Starting = false;
            return;
        }
        Animate(T);
    }

    /// <summary>
    /// Playback has just been started. From the very beginning, with the
    /// switch on, it is stopped again at once, before a frame of audio is
    /// out, and "Ready" takes the four seconds instead.
    /// </summary>
    private void OnPlay(float At)
    {
        if (Starting || !LimSystem.Preferences.ReadyIntro || Tuner == null || !Tuner.isInitialized) return;
        if (Started >= 0)
        {
            // Play pressed again during it: straight to the song.
            Stop();
            return;
        }
        if (Mathf.Abs(At) > AtStart || !Load()) return;
        Starting = true;
        Tuner.MediaPlayerManager.IsPlaying = false;
        Tuner.MediaPlayerManager.Time = 0;
        Starting = false;
        Build();
        Started = Time.unscaledTime;
        Animate(0);
    }

    private bool Load()
    {
        if (Tried) return Found;
        Tried = true;
        string B = LimUiTweakAssets.ScreenBundle;
        BandSprite = LimUiTweakAssets.Load<Sprite>(B, "playscene_1");
        WordSprite = LimUiTweakAssets.Load<Sprite>(B, "Ready");
        LineSprite = LimUiTweakAssets.Load<Sprite>(B, "ReadySelector");
        Canvas = Tuner.transform.Find("FullScreenCanvas") as RectTransform;
        Found = BandSprite != null && WordSprite != null && LineSprite != null && Canvas != null;
        return Found;
    }

    private void Build()
    {
        GameObject Holder = new GameObject("Ready", typeof(RectTransform));
        Holder.layer = Canvas.gameObject.layer;
        Root = Holder.GetComponent<RectTransform>();
        Root.SetParent(Canvas, false);
        Root.anchorMin = Vector2.zero;
        Root.anchorMax = Vector2.one;
        Root.sizeDelta = Vector2.zero;
        Rect Area = Canvas.rect;
        float Height = Area.height;
        Band = Piece("Band", BandSprite, new Vector2(Mathf.Max(Area.width, Height * 16f / 9f), Height * BandHeight), 0, out BandImage);
        BandImage.type = Image.Type.Simple;
        float WordWidth = Height * WordHeight * WordSprite.rect.width / WordSprite.rect.height;
        Piece("Word", WordSprite, new Vector2(WordWidth, Height * WordHeight), 0, out Word);
        Piece("Line", LineSprite, new Vector2(Height * LineWidth, Height * LineHeight), -Height * LineBelow, out Line);
    }

    private RectTransform Piece(string Name, Sprite Picture, Vector2 Size, float Y, out Image Look)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        Holder.layer = Root.gameObject.layer;
        RectTransform Place = Holder.GetComponent<RectTransform>();
        Place.SetParent(Root, false);
        Place.anchorMin = Place.anchorMax = new Vector2(0.5f, 0.5f);
        Place.sizeDelta = Size;
        Place.anchoredPosition = new Vector2(0, Y);
        Look = Holder.AddComponent<Image>();
        Look.sprite = Picture;
        Look.raycastTarget = false;
        return Place;
    }

    private void Animate(float T)
    {
        if (Root == null) return;
        float Opening = Mathf.Clamp01(T);
        float Closing = Mathf.Clamp01((Length - T) / 0.5f);
        Band.localScale = new Vector3(1, Opening * Closing, 1);
        float Blink = Sample(T);
        Word.color = new Color(1, 1, 1, Blink);
        Line.color = new Color(1, 1, 1, Blink);
    }

    private static float Sample(float T)
    {
        for (int i = 1; i < BlinkTimes.Length; ++i)
        {
            if (T <= BlinkTimes[i])
                return Mathf.Lerp(BlinkValues[i - 1], BlinkValues[i], (T - BlinkTimes[i - 1]) / (BlinkTimes[i] - BlinkTimes[i - 1]));
        }
        return 0;
    }

    private void Stop()
    {
        Started = -1;
        if (Root != null) Destroy(Root.gameObject);
        Root = null;
    }

    private void OnDestroy()
    {
        if (Hook != null) Hook.Release();
    }
}

/// <summary>
/// A listener on the media player's OnPlay event, which is a static that the
/// player makes anew in its Start: a hook knows whether it is still on the
/// event in use, so its owner can hang a new one when the player has
/// replaced it (another project opened, the tuner scene loaded again).
/// </summary>
public class LimOnPlayHook
{
    private readonly Lanotalium.MediaPlayer.OnPlayEvent Event;
    private readonly UnityEngine.Events.UnityAction<float> Action;

    public LimOnPlayHook(UnityEngine.Events.UnityAction<float> Listener)
    {
        Event = LimMediaPlayerManager.OnPlay;
        Action = Listener;
        if (Event != null) Event.AddListener(Action);
    }

    public bool Current
    {
        get { return Event != null && Event == LimMediaPlayerManager.OnPlay; }
    }

    public void Release()
    {
        if (Event != null) Event.RemoveListener(Action);
    }
}
