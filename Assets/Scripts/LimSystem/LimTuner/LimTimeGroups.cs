using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;

/// <summary>
/// The time groups of the chart that is open, and the editor's state about
/// them: which one new notes go into, which ones are hidden, and whether the
/// tuner shows the groups' effects.
///
/// A note in group 0, the base group, moves and looks exactly as every note
/// always has. A note in any other group moves by that group's own scroll
/// speed, which may be slower, stopped, or negative: the position of a note is
/// the distance its group has scrolled between now and the note's time, so a
/// negative speed simply runs that distance backwards and the note travels
/// back toward the core. On top of that a group can fade over time, fade by
/// where a note is on its path, turn round the core, and be tinted.
///
/// Static, because the tuner's note managers, the Creator, the clipboard and
/// the inspector all need it and none of them owns the others. Load resets
/// it for every chart that is opened.
/// </summary>
public static class LimTimeGroups
{
    public const int BaseGroup = 0;
    /// <summary>
    /// How faint a note is drawn at its most invisible while the effects are
    /// off, so a note the chart hides can still be seen and picked up.
    /// </summary>
    public const float GhostAlpha = 0.3f;
    /// <summary>Where on screen a note's path begins and ends, in the tuner's own percent.</summary>
    private const float PathStart = 20f, PathEnd = 100f;

    private static List<LanotaTimeGroup> _Groups = new List<LanotaTimeGroup>();
    private static readonly Dictionary<int, LanotaTimeGroup> _ById = new Dictionary<int, LanotaTimeGroup>();
    private static readonly HashSet<int> _Hidden = new HashSet<int>();
    private static int _HighestId;
    private static int _EvaluatedFrame = -1;

    /// <summary>The chart's groups, base group not included.</summary>
    public static List<LanotaTimeGroup> Groups { get { return _Groups; } }

    /// <summary>Where new notes go. Editor state, never saved.</summary>
    public static int ActiveGroup { get; private set; }

    /// <summary>
    /// Group effects shown, which is what the chart will look like. Off, every
    /// note moves by the chart's own speed and is not turned, so a note sits
    /// exactly where the pointer places or drags it (the pointer is always
    /// read against the chart's speed, since a stopped group has no position
    /// that tells one moment from another), and notes the chart hides are
    /// drawn faintly instead of not at all. Tints stay, so the groups can
    /// still be told apart.
    /// </summary>
    public static bool Preview = true;

    /// <summary>Raised whenever the groups or the editor's view of them change.</summary>
    public static event System.Action Changed;

    public static void Load(List<LanotaTimeGroup> Groups)
    {
        _Groups = Groups ?? new List<LanotaTimeGroup>();
        _Hidden.Clear();
        _Extras.Clear();
        ActiveGroup = BaseGroup;
        Preview = true;
        _HighestId = 0;
        _EvaluatedFrame = -1;
        Reindex();
        RaiseChanged();
    }

    /// <summary>Rebuilds the lookup after a group is added or removed.</summary>
    public static void Reindex()
    {
        _ById.Clear();
        foreach (LanotaTimeGroup Group in _Groups)
        {
            if (Group == null) continue;
            _ById[Group.Id] = Group;
            if (Group.Id > _HighestId) _HighestId = Group.Id;
        }
        if (ActiveGroup != BaseGroup && !_ById.ContainsKey(ActiveGroup)) ActiveGroup = BaseGroup;
        _EvaluatedFrame = -1;
    }

    public static LanotaTimeGroup Find(int Id)
    {
        LanotaTimeGroup Group;
        return _ById.TryGetValue(Id, out Group) ? Group : null;
    }

    public static bool Exists(int Id)
    {
        return Id == BaseGroup || _ById.ContainsKey(Id);
    }

    /// <summary>
    /// A number no group of this chart has had in this session, so undoing
    /// the removal of a group can never find its number taken by another.
    /// </summary>
    public static int NextId()
    {
        return _HighestId + 1;
    }

    public static void NoteIdUsed(int Id)
    {
        if (Id > _HighestId) _HighestId = Id;
    }

    public static void SetActive(int Id)
    {
        ActiveGroup = Exists(Id) ? Id : BaseGroup;
        RaiseChanged();
    }

    public static bool IsVisible(int Id)
    {
        return !_Hidden.Contains(Id);
    }

    public static void SetVisible(int Id, bool Visible)
    {
        if (Visible) _Hidden.Remove(Id);
        else _Hidden.Add(Id);
        RaiseChanged();
    }

    public static void SetPreview(bool On)
    {
        Preview = On;
        RaiseChanged();
    }

    // ------------------------------------------------------------ movement

    /// <summary>
    /// Whether a note is moved by a scroll speed other than the chart's. Only
    /// then does it skip the chart's own culling, which works out what is on
    /// screen from the chart's speed and would hide it at the wrong moments.
    /// </summary>
    public static bool UsesOwnScroll(int Group)
    {
        return Group != BaseGroup && Preview && _ById.ContainsKey(Group);
    }

    /// <summary>The scroll speed list a note of this group moves by.</summary>
    public static List<LanotaScroll> ScrollFor(int Group, LimScrollManager ScrollManager)
    {
        // Chart speed switched off in the editor means every speed, a
        // group's included; ScrollManager.Scroll already answers that.
        if (!UsesOwnScroll(Group) || ScrollManager.DisableChartSpeed) return ScrollManager.Scroll;
        LanotaTimeGroup Found = Find(Group);
        if (Found.Scroll == null || Found.Scroll.Count == 0) return ScrollManager.Scroll;
        return Found.Scroll;
    }

    /// <summary>
    /// How far along its path a note is, 100 being on the judge line, worked
    /// out the way the tuner has always done it but from any scroll list: 100
    /// less the distance the list scrolls between now and the note's time.
    /// </summary>
    public static float MovePercent(List<LanotaScroll> Scroll, float ChartTime, float JudgeTime, float PlaySpeed)
    {
        int StartScroll = 0, EndScroll = 0;
        float Percent = 100;
        int Count = Scroll.Count;
        for (int i = 0; i < Count - 1; ++i)
        {
            if (ChartTime >= Scroll[i].Time && ChartTime < Scroll[i + 1].Time) StartScroll = i;
            if (JudgeTime >= Scroll[i].Time && JudgeTime < Scroll[i + 1].Time) EndScroll = i;
        }
        if (Count != 0)
        {
            if (ChartTime >= Scroll[Count - 1].Time) StartScroll = Count - 1;
            if (JudgeTime >= Scroll[Count - 1].Time) EndScroll = Count - 1;
        }
        for (int i = StartScroll; i <= EndScroll; ++i)
        {
            if (StartScroll == EndScroll) Percent -= (JudgeTime - ChartTime) * Scroll[i].Speed * 10 * PlaySpeed;
            else if (i == StartScroll) Percent -= (Scroll[i + 1].Time - ChartTime) * Scroll[i].Speed * 10 * PlaySpeed;
            else if (i != EndScroll) Percent -= (Scroll[i + 1].Time - Scroll[i].Time) * Scroll[i].Speed * 10 * PlaySpeed;
            else Percent -= (JudgeTime - Scroll[i].Time) * Scroll[i].Speed * 10 * PlaySpeed;
        }
        if (Percent < 0) Percent = 0;
        if (Percent > 100) Percent = 100;
        return Percent;
    }

    /// <summary>The speed a list is scrolling at, at a moment of the chart.</summary>
    public static float SpeedAt(List<LanotaScroll> Scroll, float Time)
    {
        if (Scroll == null || Scroll.Count == 0) return 1;
        float Speed = Scroll[0].Speed;
        for (int i = 0; i < Scroll.Count; ++i)
        {
            if (Scroll[i].Time > Time) break;
            Speed = Scroll[i].Speed;
        }
        return Speed;
    }

    // ------------------------------------------------------------- effects

    /// <summary>
    /// Works out every group's opacity, turn and tint for this moment, once a
    /// frame however many notes ask. The easing is the camera's own table, so
    /// an ease number means the same here as on a motion.
    /// </summary>
    public static void EvaluateFrame(float ChartTime, LimCameraManager Camera)
    {
        if (Time.frameCount == _EvaluatedFrame) return;
        _EvaluatedFrame = Time.frameCount;
        foreach (LanotaTimeGroup Group in _Groups)
        {
            if (Group == null) continue;
            Group.EvalAlpha = Mathf.Clamp01(WalkDestination(Group.Opacity, ChartTime, 100f, Camera) / 100f);
            Group.EvalRotation = WalkAdditive(Group.Rotation, ChartTime, Camera);
            Color Tint;
            Group.EvalHasTint = TryParseTint(Group.Color, out Tint);
            Group.EvalTint = Group.EvalHasTint ? Tint : Color.white;
        }
    }

    /// <summary>Each key takes the value from wherever it was to its own, the way the transparency motion does.</summary>
    public static float WalkDestination(List<LanotaGroupKey> Keys, float Time, float Start, LimCameraManager Camera)
    {
        float Value = Start;
        if (Keys == null) return Value;
        for (int i = 0; i < Keys.Count; ++i)
        {
            LanotaGroupKey Key = Keys[i];
            if (Time < Key.Time) break;
            float Reached = (i + 1 < Keys.Count && Keys[i + 1].Time <= Time) ? Keys[i + 1].Time : Time;
            float Percent = Key.Duration > 0 ? (Reached - Key.Time) / Key.Duration : 1f;
            Value += (Key.Value - Value) * Ease(Camera, Percent, Key.Ease);
        }
        return Value;
    }

    /// <summary>Each key adds its value, the way the rotation motion does.</summary>
    public static float WalkAdditive(List<LanotaGroupKey> Keys, float Time, LimCameraManager Camera)
    {
        float Value = 0;
        if (Keys == null) return Value;
        for (int i = 0; i < Keys.Count; ++i)
        {
            LanotaGroupKey Key = Keys[i];
            if (Time < Key.Time) break;
            float Reached = (i + 1 < Keys.Count && Keys[i + 1].Time <= Time) ? Keys[i + 1].Time : Time;
            float Percent = Key.Duration > 0 ? (Reached - Key.Time) / Key.Duration : 1f;
            Value += Key.Value * Ease(Camera, Percent, Key.Ease);
        }
        return Value;
    }

    private static float Ease(LimCameraManager Camera, float Percent, int Mode)
    {
        if (Camera == null) return Mathf.Clamp01(Percent);
        return Camera.CalculateEasedCurve(Percent, Mode);
    }

    /// <summary>Whether a note is one this file paints or turns at all. Base notes never are.</summary>
    public static bool HasEffects(int Group)
    {
        return Group != BaseGroup && _ById.ContainsKey(Group);
    }

    /// <summary>
    /// Degrees a note of this group is turned round the core right now. Only
    /// with the effects shown: while they are off, notes sit where the
    /// pointer puts them.
    /// </summary>
    public static float NoteRotation(int Group)
    {
        if (!UsesOwnScroll(Group)) return 0;
        return Find(Group).EvalRotation;
    }

    /// <summary>
    /// How opaque a note is drawn: the group's opacity now, times its fade by
    /// where the note is on its path. With the effects off, a hidden note is
    /// drawn faintly instead of not at all, and a selected note is always at
    /// least faintly there, or it could not be seen to be selected.
    /// </summary>
    public static float NoteAlpha(int Group, float Percent, bool Selected)
    {
        LanotaTimeGroup Found = Find(Group);
        if (Found == null) return 1;
        float Alpha = Found.EvalAlpha * DistanceAlpha(Found, Percent);
        if (!Preview) Alpha = Mathf.Lerp(GhostAlpha, 1f, Alpha);
        if (Selected) Alpha = Mathf.Max(Alpha, GhostAlpha);
        return Mathf.Clamp01(Alpha);
    }

    /// <summary>
    /// The fade by position, from the tuner's percent (20 at the core, 100 on
    /// the line) turned into per cent of the path the settings are given in.
    /// </summary>
    public static float DistanceAlpha(LanotaTimeGroup Group, float Percent)
    {
        float Along = Mathf.Clamp((Percent - PathStart) / (PathEnd - PathStart) * 100f, 0, 100);
        float Alpha = 1;
        if (Group.FadeIn > 0) Alpha *= Mathf.Clamp01(Along / Group.FadeIn);
        if (Group.FadeOut < 100) Alpha *= Mathf.Clamp01((100f - Along) / (100f - Group.FadeOut));
        return Alpha;
    }

    /// <summary>
    /// A note's colour with its group's tint and opacity on it. The tint is
    /// left off a selected note, which has to keep reading as selected, and
    /// off whichever of the note and its highlight the group leaves plain.
    /// </summary>
    public static Color Shade(int Group, Color Base, float Percent, bool Selected, bool Highlight = false)
    {
        LanotaTimeGroup Found = Find(Group);
        if (Found == null) return Base;
        Color Result = Base;
        if (Tinted(Found, Selected, Highlight))
        {
            Result.r *= Found.EvalTint.r;
            Result.g *= Found.EvalTint.g;
            Result.b *= Found.EvalTint.b;
        }
        Result.a = Base.a * NoteAlpha(Group, Percent, Selected);
        return Result;
    }

    /// <summary>RRGGBB, with or without a leading #; empty or unreadable is no tint.</summary>
    public static bool TryParseTint(string Hex, out Color Tint)
    {
        Tint = Color.white;
        if (string.IsNullOrEmpty(Hex)) return false;
        string Clean = Hex.Trim().TrimStart('#');
        if (Clean.Length != 6) return false;
        int Value;
        if (!int.TryParse(Clean, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out Value)) return false;
        Tint = new Color(((Value >> 16) & 255) / 255f, ((Value >> 8) & 255) / 255f, (Value & 255) / 255f, 1f);
        return true;
    }

    /// <summary>
    /// A note's other sprites: the Light a highlighted (Combination) note
    /// wears behind itself. Found once per note object and kept, so the fade
    /// can reach them too; a note of a group has to vanish whole, or the glow
    /// left behind gives away where it is.
    /// </summary>
    private static readonly Dictionary<GameObject, SpriteRenderer[]> _Extras = new Dictionary<GameObject, SpriteRenderer[]>();

    /// <summary>
    /// Gives every other sprite of the note the group's tint and the same
    /// opacity as the note. Which sprite a prefab lists first is not the same
    /// for every note (a highlighted rail lists its Light before itself), so
    /// all of them are treated alike rather than trusting the order.
    /// </summary>
    public static void FadeExtras(GameObject Note, SpriteRenderer Main, int Group, bool Selected, float Alpha)
    {
        SpriteRenderer[] Extras = ExtrasOf(Note, Main);
        if (Extras == null) return;
        LanotaTimeGroup Found = Find(Group);
        foreach (SpriteRenderer Sprite in Extras)
        {
            if (Sprite == null) continue;
            Color Wanted = Found != null && Tinted(Found, Selected, IsHighlight(Sprite)) ? Found.EvalTint : Color.white;
            Wanted.a = Alpha;
            if (Sprite.color != Wanted) Sprite.color = Wanted;
        }
    }

    private static void FadeExtras(GameObject Note, SpriteRenderer Main, Color Wanted)
    {
        SpriteRenderer[] Extras = ExtrasOf(Note, Main);
        if (Extras == null) return;
        foreach (SpriteRenderer Sprite in Extras)
        {
            if (Sprite != null && Sprite.color != Wanted) Sprite.color = Wanted;
        }
    }

    private static SpriteRenderer[] ExtrasOf(GameObject Note, SpriteRenderer Main)
    {
        if (Note == null) return null;
        SpriteRenderer[] Extras;
        if (!_Extras.TryGetValue(Note, out Extras))
        {
            List<SpriteRenderer> Found = new List<SpriteRenderer>();
            foreach (SpriteRenderer Sprite in Note.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (Sprite != Main) Found.Add(Sprite);
            }
            Extras = Found.ToArray();
            _Extras[Note] = Extras;
        }
        return Extras;
    }

    private static bool Tinted(LanotaTimeGroup Group, bool Selected, bool Highlight)
    {
        return Group.EvalHasTint && !Selected && (Highlight ? Group.ColorHighlight : Group.ColorNotes);
    }

    private static readonly Dictionary<SpriteRenderer, bool> _Highlights = new Dictionary<SpriteRenderer, bool>();

    /// <summary>
    /// Whether a sprite is the glow of a highlighted note, the prefabs'
    /// "Light". Asked by sprite rather than by place: a highlighted rail's
    /// first sprite, which the managers take for the note, is its Light.
    /// Kept, since reading a name makes a new string every time.
    /// </summary>
    public static bool IsHighlight(SpriteRenderer Sprite)
    {
        if (Sprite == null) return false;
        bool Known;
        if (!_Highlights.TryGetValue(Sprite, out Known))
        {
            Known = Sprite.gameObject.name == "Light";
            if (_Highlights.Count > 20000) _Highlights.Clear();
            _Highlights[Sprite] = Known;
        }
        return Known;
    }

    /// <summary>
    /// A note that left its group, or whose group went: its other sprites go
    /// back to fully shown, which is what every prefab ships them at.
    /// </summary>
    /// <summary>
    /// For code that adds or removes sprites on a note after it was built
    /// (the flick arrows): the list of its other sprites is read again.
    /// </summary>
    public static void ForgetExtras(GameObject Note)
    {
        if (Note != null) _Extras.Remove(Note);
    }

    public static void RestoreExtras(GameObject Note, SpriteRenderer Main)
    {
        if (Note == null || !_Extras.ContainsKey(Note)) return;
        FadeExtras(Note, Main, Color.white);
        _Extras.Remove(Note);
    }

    public static void RaiseChanged()
    {
        _EvaluatedFrame = -1;
        if (Changed != null) Changed();
    }
}
