using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a note does when it reaches the judge line, from Flowaria's UiTweak
/// "Particle Effects": a flash of notes and a shockwave on every tap, sparks
/// thrown inwards or outwards by a flick, and on a hold a shockwave, a ripple
/// and sparks that stay on its head while it lasts and a last shockwave when
/// it ends. With the combo counter on, the combo reached is shown beside the
/// note, in one of eight places round the ring (the plugin's own "ComboText",
/// the note's direction rounded to 45 degrees).
///
/// There is no player in the editor, so every note is a perfect hit, at the
/// moment the chart says. The plugin's choice of effect per note type, its
/// prefabs and its parent (the Tuner, so the effects scale and turn with the
/// ring) are kept. What changed:
///
/// - A note fires when the playhead crosses it during playback, and only
///   then. The plugin fired every note it found behind the playhead, so a
///   jump forward set off every note in between at once, and so did dragging
///   the playhead while paused.
/// - Pausing freezes the effects on screen, and playing again carries on
///   from there; a jump clears them. The plugin let them run on, and a hold's
///   sparks went on for their whole length with the song stopped.
/// - A hold's sparks and ripple stay on until the hold ends or playback stops,
///   instead of being timed at the start, so a hold that is edited, deleted
///   or started halfway through behaves. Starting playback inside a hold
///   picks its sparks up for what is left of it.
/// - Effects run at the playback speed, so slowing the song slows them too.
/// - The combo number is the combo actually reached, hold ticks included;
///   the plugin counted the ticks but showed a number that lagged behind them.
/// </summary>
public class LimNoteEffects : MonoBehaviour
{
    public LimTunerManager Tuner;

    /// <summary>Where the note managers put a note at 100 per cent: the judge line.</summary>
    private const float JudgeRadius = 10f;
    /// <summary>
    /// How far the playhead may run ahead of what the frame's own length
    /// explains before it counts as a jump. The music's clock moves in steps
    /// of its audio buffer, a few hundredths of a second.
    /// </summary>
    private const float JumpAllowance = 0.1f;

    private class Effect
    {
        public GameObject Root;
        public ParticleSystem[] Particles;
        public Animator[] Animators;
        /// <summary>Seconds of playback left, for effects no particle system ends; below 0, the particles decide.</summary>
        public float Life = -1;
        /// <summary>A hold's head, followed while the hold lasts.</summary>
        public Transform Follow;
    }

    private class HoldEffect
    {
        public Lanotalium.Chart.LanotaHoldNote Note;
        public readonly List<Effect> Loops = new List<Effect>();
    }

    private class ComboText
    {
        public Animator Anim;
        public TextMesh Text;
        public float Angle;
        public int State;
    }

    private struct Hit
    {
        public float Time;
        public float Angle;
    }

    private readonly List<Effect> Effects = new List<Effect>();
    private readonly List<HoldEffect> Holds = new List<HoldEffect>();
    private readonly List<Hit> Hits = new List<Hit>();
    private readonly List<ComboText> ComboTexts = new List<ComboText>();

    private GameObject Harmony, ShockwaveClick, SparkleFlickIn, SparkleFlickOut, HoldStart, HoldMiddle, HoldEnd, SparkleHold, ComboCluster;
    private Font ComboFont;
    private bool PrefabsTried, PrefabsFound;

    private Transform TunerTransform;
    private GameObject Combo;
    private float LastTime;
    private bool WasPlaying, Frozen;
    private float AppliedSpeed = 1;

    private void LateUpdate()
    {
        if (Tuner == null || !Tuner.isInitialized || Tuner.MediaPlayerManager == null) return;
        bool Particles = LimSystem.Preferences.NoteEffects, Counter = LimSystem.Preferences.ComboCounter;
        if (!Particles) ClearEffects();
        if (!Counter) RemoveCombo();
        if (!Particles && !Counter)
        {
            WasPlaying = false;
            return;
        }
        if (!LoadPrefabs() || !FindTuner()) return;
        if (Counter) EnsureCombo();

        float Now = Tuner.ChartTime;
        bool Playing = Tuner.MediaPlayerManager.IsPlaying;
        float Speed = Mathf.Max(0.05f, Tuner.MediaPlayerManager.Pitch);

        if (!Playing)
        {
            if (WasPlaying) Freeze(true);
            // The playhead moved while paused: what is on screen belongs to
            // a moment that is no longer shown. Pausing itself may nudge the
            // clock a little (the precise mode resyncs it to the audio).
            float Moved = Mathf.Abs(Now - LastTime);
            if (WasPlaying ? Moved > JumpAllowance : Moved > 0.0001f) ClearAll();
            LastTime = Now;
            WasPlaying = false;
            FollowHolds();
            return;
        }

        bool Continuous = WasPlaying
            ? Now >= LastTime - 0.0001f && Now - LastTime <= Time.unscaledDeltaTime * Speed * 3 + JumpAllowance
            : Mathf.Abs(Now - LastTime) <= JumpAllowance;
        if (Speed != AppliedSpeed) SetSpeed(Speed);
        if (!Continuous) ClearAll();
        else
        {
            if (Frozen) Freeze(false);
            Cross(LastTime, Now, Particles, Counter);
        }
        // Playback starting, or landing, inside a hold.
        if ((!Continuous || !WasPlaying) && Particles) PickUpHolds(Now);
        EndHolds(Now);
        Age(Time.deltaTime * Speed);
        FollowHolds();
        LastTime = Now;
        WasPlaying = true;
    }

    private void OnDestroy()
    {
        ClearAll();
    }

    private bool LoadPrefabs()
    {
        if (PrefabsTried) return PrefabsFound;
        PrefabsTried = true;
        string B = LimUiTweakAssets.ParticleBundle;
        Harmony = LimUiTweakAssets.Load<GameObject>(B, "harmonyeffect");
        ShockwaveClick = LimUiTweakAssets.Load<GameObject>(B, "ShockwaveClick");
        SparkleFlickIn = LimUiTweakAssets.Load<GameObject>(B, "SparkleFlickIn");
        SparkleFlickOut = LimUiTweakAssets.Load<GameObject>(B, "SparkleFlickOut");
        HoldStart = LimUiTweakAssets.Load<GameObject>(B, "ShockwaveHoldStart");
        HoldMiddle = LimUiTweakAssets.Load<GameObject>(B, "ShockwaveHoldMiddle");
        HoldEnd = LimUiTweakAssets.Load<GameObject>(B, "ShockwaveHoldEnd");
        SparkleHold = LimUiTweakAssets.Load<GameObject>(B, "SparkleHold");
        ComboCluster = LimUiTweakAssets.Load<GameObject>(B, "ComboText");
        ComboFont = LimUiTweakAssets.Load<Font>(B, "Assets/UiTweak/ParticleTweak/Fonts/kawoszeh.ttf");
        PrefabsFound = Harmony != null && ShockwaveClick != null && SparkleFlickIn != null && SparkleFlickOut != null
            && HoldStart != null && HoldMiddle != null && HoldEnd != null && SparkleHold != null && ComboCluster != null;
        return PrefabsFound;
    }

    private bool FindTuner()
    {
        if (TunerTransform != null) return true;
        if (Tuner.CameraManager != null && Tuner.CameraManager.TunerGameObject != null) TunerTransform = Tuner.CameraManager.TunerGameObject.transform;
        else TunerTransform = Tuner.transform.Find("Tuner");
        return TunerTransform != null;
    }

    /// <summary>
    /// Every note the playhead passed since the last frame, in (From, To]:
    /// a note exactly where playback started from has already been seen.
    /// </summary>
    private void Cross(float From, float To, bool Particles, bool Counter)
    {
        Hits.Clear();
        foreach (Lanotalium.Chart.LanotaTapNote Note in Tuner.TapNoteManager.TapNote)
        {
            if (Note.Time <= From || Note.Time > To) continue;
            Vector3 Position;
            Quaternion Rotation;
            PlaceOnJudgeLine(Note.Degree + LimTimeGroups.NoteRotation(Note.Group), out Position, out Rotation);
            if (Particles) EmitTap(Note, Position, Rotation);
            Hits.Add(new Hit { Time = Note.Time, Angle = AngleOnRing(Position) });
        }
        foreach (Lanotalium.Chart.LanotaHoldNote Note in Tuner.HoldNoteManager.HoldNote)
        {
            if (Note.Time > From && Note.Time <= To)
            {
                Transform Head = HeadOf(Note);
                if (Particles) StartHold(Note, Head, true);
                Hits.Add(new Hit { Time = Note.Time, Angle = AngleOnRing(Head.position) });
            }
            float End = Note.Time + Note.Duration;
            if (End > From && End <= To && Particles) FinishHold(Note, true);
        }
        if (Counter && Hits.Count != 0) ShowCombo(To);
    }

    /// <summary>The same placement the tap note manager gives a note at the judge line.</summary>
    private void PlaceOnJudgeLine(float Degree, out Vector3 Position, out Quaternion Rotation)
    {
        float Rotated = Degree + Tuner.CameraManager.CurrentRotation;
        Position = new Vector3(-JudgeRadius * Mathf.Sin(Rotated * Mathf.Deg2Rad), 0, -JudgeRadius * Mathf.Cos(Rotated * Mathf.Deg2Rad));
        Rotation = Quaternion.Euler(90, Rotated, 0);
    }

    private void EmitTap(Lanotalium.Chart.LanotaTapNote Note, Vector3 Position, Quaternion Rotation)
    {
        Effect Flash = Spawn(Harmony, Position, Rotation, null);
        Flash.Life = LongestClip(Flash.Animators);
        // A catch is only brushed, not struck: no shockwave.
        if (Note.Type != 4) Spawn(ShockwaveClick, Position, Rotation, null);
        if (Note.Type == 2) Spawn(SparkleFlickIn, Position, Rotation, null);
        else if (Note.Type == 3) Spawn(SparkleFlickOut, Position, Rotation, null);
    }

    /// <summary>
    /// The head of a hold, which the hold note manager keeps on the judge
    /// line and turns with the rail for as long as the hold lasts.
    /// </summary>
    private Transform HeadOf(Lanotalium.Chart.LanotaHoldNote Note)
    {
        return Note.HoldNoteGameObject != null ? Note.HoldNoteGameObject.transform : TunerTransform;
    }

    private void StartHold(Lanotalium.Chart.LanotaHoldNote Note, Transform Head, bool WithShockwave)
    {
        if (FindHold(Note) != null) return;
        if (WithShockwave) Spawn(HoldStart, Head.position, Head.rotation, Head);
        HoldEffect Hold = new HoldEffect { Note = Note };
        Effect Ripple = Spawn(HoldMiddle, Head.position, Head.rotation, Head);
        Effect Sparks = Spawn(SparkleHold, Head.position, Head.rotation, Head);
        // The ripple is a one-second system in the prefab, which the plugin
        // stretched to the hold's length; here it simply runs until stopped.
        foreach (ParticleSystem System in Ripple.Particles)
        {
            ParticleSystem.MainModule Main = System.main;
            Main.loop = true;
        }
        Hold.Loops.Add(Ripple);
        Hold.Loops.Add(Sparks);
        Holds.Add(Hold);
    }

    private void FinishHold(Lanotalium.Chart.LanotaHoldNote Note, bool WithShockwave)
    {
        HoldEffect Hold = FindHold(Note);
        if (Hold != null) StopHold(Hold);
        if (WithShockwave)
        {
            Transform Head = HeadOf(Note);
            Spawn(HoldEnd, Head.position, Head.rotation, null);
        }
    }

    private HoldEffect FindHold(Lanotalium.Chart.LanotaHoldNote Note)
    {
        foreach (HoldEffect Hold in Holds) if (Hold.Note == Note) return Hold;
        return null;
    }

    /// <summary>
    /// The sparks stop coming and the ones in the air are left to fade; the
    /// prefabs' own stop action removes them once the last one is gone.
    /// </summary>
    private void StopHold(HoldEffect Hold)
    {
        foreach (Effect Loop in Hold.Loops)
        {
            if (Loop.Root == null) continue;
            foreach (ParticleSystem System in Loop.Particles) if (System != null) System.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Loop.Follow = null;
        }
        Holds.Remove(Hold);
    }

    /// <summary>
    /// Holds whose end the playhead is past, or before whose start it went
    /// back, or that are no longer in the chart, lose their sparks.
    /// </summary>
    private void EndHolds(float Now)
    {
        for (int i = Holds.Count - 1; i >= 0; --i)
        {
            Lanotalium.Chart.LanotaHoldNote Note = Holds[i].Note;
            if (Now < Note.Time || Now > Note.Time + Note.Duration || !Tuner.HoldNoteManager.HoldNote.Contains(Note)) StopHold(Holds[i]);
        }
    }

    /// <summary>After a jump, the holds the playhead landed inside get their sparks for what is left of them.</summary>
    private void PickUpHolds(float Now)
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in Tuner.HoldNoteManager.HoldNote)
        {
            if (Note.Time < Now && Now < Note.Time + Note.Duration) StartHold(Note, HeadOf(Note), false);
        }
    }

    private Effect Spawn(GameObject Prefab, Vector3 Position, Quaternion Rotation, Transform Follow)
    {
        GameObject Root = Instantiate(Prefab, Position, Rotation, TunerTransform);
        Effect Made = new Effect
        {
            Root = Root,
            Particles = Root.GetComponentsInChildren<ParticleSystem>(true),
            Animators = Root.GetComponentsInChildren<Animator>(true),
            Follow = Follow
        };
        SetSpeed(Made, AppliedSpeed);
        Effects.Add(Made);
        return Made;
    }

    private static float LongestClip(Animator[] Animators)
    {
        float Longest = 0;
        foreach (Animator Anim in Animators)
        {
            if (Anim == null || Anim.runtimeAnimatorController == null) continue;
            foreach (AnimationClip Clip in Anim.runtimeAnimatorController.animationClips) Longest = Mathf.Max(Longest, Clip.length);
        }
        return Longest > 0 ? Longest : 1;
    }

    /// <summary>
    /// Counts down the effects that end by time, at the playback speed, and
    /// forgets the ones their particles have already removed.
    /// </summary>
    private void Age(float Seconds)
    {
        for (int i = Effects.Count - 1; i >= 0; --i)
        {
            Effect E = Effects[i];
            if (E.Root == null)
            {
                Effects.RemoveAt(i);
                continue;
            }
            if (E.Life < 0) continue;
            E.Life -= Seconds;
            if (E.Life <= 0)
            {
                Destroy(E.Root);
                Effects.RemoveAt(i);
            }
        }
    }

    private void FollowHolds()
    {
        foreach (Effect E in Effects)
        {
            if (E.Root == null || E.Follow == null) continue;
            E.Root.transform.SetPositionAndRotation(E.Follow.position, E.Follow.rotation);
        }
    }

    /// <summary>
    /// Stops everything where it is, particles and animations alike, or lets
    /// it go on. Done through their speeds rather than Pause, which a system
    /// that was already told to stop emitting would take back on resuming.
    /// </summary>
    private void Freeze(bool Stop)
    {
        Frozen = Stop;
        float Speed = Stop ? 0 : AppliedSpeed;
        foreach (Effect E in Effects) SetSpeed(E, Speed);
        foreach (ComboText Text in ComboTexts) if (Text.Anim != null) Text.Anim.speed = Speed;
    }

    private void SetSpeed(float Speed)
    {
        AppliedSpeed = Speed;
        if (Frozen) return;
        foreach (Effect E in Effects) SetSpeed(E, Speed);
        foreach (ComboText Text in ComboTexts) if (Text.Anim != null) Text.Anim.speed = Speed;
    }

    private static void SetSpeed(Effect E, float Speed)
    {
        if (E.Root == null) return;
        foreach (ParticleSystem System in E.Particles)
        {
            if (System == null) continue;
            ParticleSystem.MainModule Main = System.main;
            Main.simulationSpeed = Speed;
        }
        foreach (Animator Anim in E.Animators) if (Anim != null) Anim.speed = Speed;
    }

    private void ClearEffects()
    {
        foreach (Effect E in Effects) if (E.Root != null) Destroy(E.Root);
        Effects.Clear();
        Holds.Clear();
        Frozen = false;
    }

    private void ClearAll()
    {
        ClearEffects();
        foreach (ComboText Text in ComboTexts) HideCombo(Text);
    }

    private void EnsureCombo()
    {
        if (Combo != null) return;
        ComboTexts.Clear();
        Combo = Instantiate(ComboCluster, TunerTransform, false);
        Combo.name = "ComboText";
        foreach (Transform Place in Combo.transform)
        {
            Transform Label = Place.Find("Text");
            ComboText Text = new ComboText
            {
                Anim = Place.GetComponent<Animator>(),
                Text = Label != null ? Label.GetComponent<TextMesh>() : null,
                Angle = Place.localEulerAngles.y
            };
            if (Text.Anim == null || Text.Text == null) continue;
            // The plugin set the font by hand; kept in case the prefab lost it.
            if (Text.Text.font == null && ComboFont != null)
            {
                Text.Text.font = ComboFont;
                Text.Text.GetComponent<MeshRenderer>().sharedMaterial = ComboFont.material;
            }
            Text.Anim.Update(0);
            Text.State = Text.Anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
            HideCombo(Text);
            ComboTexts.Add(Text);
        }
        Freeze(Frozen);
    }

    private void RemoveCombo()
    {
        if (Combo != null) Destroy(Combo);
        Combo = null;
        ComboTexts.Clear();
    }

    /// <summary>
    /// A new copy would play its animation straight away and flash the word
    /// in all eight places, so it is sent to the animation's last frame, which
    /// shows nothing.
    /// </summary>
    private static void HideCombo(ComboText Text)
    {
        if (Text.Anim == null) return;
        Text.Anim.Play(Text.State, 0, 1f);
        Text.Anim.Update(0);
    }

    /// <summary>
    /// Several notes in one frame are numbered in time order up to the combo
    /// reached, each at its own place round the ring.
    /// </summary>
    private void ShowCombo(float Now)
    {
        if (ComboTexts.Count == 0) return;
        Hits.Sort((Hit A, Hit B) => A.Time.CompareTo(B.Time));
        int Total = LimChartClock.Combo(Tuner, Now);
        for (int i = 0; i < Hits.Count; ++i)
        {
            int Value = Mathf.Max(1, Total - (Hits.Count - 1 - i));
            ComboText Text = Nearest(Hits[i].Angle);
            Text.Text.text = Value.ToString();
            Text.Anim.speed = Frozen ? 0 : AppliedSpeed;
            Text.Anim.Play(Text.State, 0, 0f);
        }
    }

    private ComboText Nearest(float Angle)
    {
        ComboText Best = ComboTexts[0];
        float BestGap = float.MaxValue;
        foreach (ComboText Text in ComboTexts)
        {
            float Gap = Mathf.Abs(Mathf.DeltaAngle(Angle, Text.Angle));
            if (Gap < BestGap)
            {
                BestGap = Gap;
                Best = Text;
            }
        }
        return Best;
    }

    /// <summary>
    /// A point's direction seen from the middle of the ring, in the Tuner's
    /// own turn, which is how the eight places of the counter are laid out:
    /// the one turned by A degrees sits towards (-sin A, -cos A).
    /// </summary>
    private float AngleOnRing(Vector3 World)
    {
        Vector3 Local = TunerTransform.InverseTransformPoint(World);
        return Mathf.Atan2(-Local.x, -Local.z) * Mathf.Rad2Deg;
    }
}
