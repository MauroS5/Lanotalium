using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;

/// <summary>
/// Everything that changes the time groups: making and removing them,
/// renaming them, putting notes into them, and the scroll speeds each one
/// moves by.
///
/// A scroll speed entry may belong to the chart's own list or to any group's,
/// and the operations that edit one find out which rather than assuming the
/// chart's, so the one row the inspector has always used for a scroll speed
/// serves every list.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>The list a scroll speed entry belongs to.</summary>
    public List<LanotaScroll> ScrollListOf(LanotaScroll Data)
    {
        foreach (LanotaTimeGroup Group in LimTimeGroups.Groups)
        {
            if (Group.Scroll != null && Group.Scroll.Contains(Data)) return Group.Scroll;
        }
        return TunerManager.ScrollManager.Scroll;
    }

    private List<LanotaTimeGroup> ChartTimeGroups
    {
        get { return TunerManager.ChartContainer.ChartData.LanotaTimeGroups; }
    }

    /// <summary>
    /// A new group, moving at speed 1 like the chart does by default, made
    /// the active one so the notes placed next go straight into it.
    /// </summary>
    public LanotaTimeGroup CreateTimeGroup()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return null;
        LanotaTimeGroup Group = new LanotaTimeGroup { Id = LimTimeGroups.NextId() };
        Group.Name = LimLanguageManager.TextDict["TimeGroups_NewName"] + " " + Group.Id;
        Group.Scroll.Add(new LanotaScroll { Speed = 1, Time = -10 });
        AddTimeGroupRaw(Group);
        LimTimeGroups.SetActive(Group.Id);

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() => { AddTimeGroupRaw(Group); });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() => { RemoveTimeGroupRaw(Group); });
        AddToOperationSaver(OpSave);
        return Group;
    }

    /// <summary>
    /// Removes a group and everything that belongs to it: its notes, rails
    /// included, go with it, as do its speeds, keys, fade and colour, which
    /// live on the group itself. They are let go of first, so nothing is left
    /// selected that no longer exists. One undo puts the group and every one
    /// of its notes back, the same objects, still in the group.
    /// </summary>
    public void DeleteTimeGroup(int Id)
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;
        LanotaTimeGroup Group = LimTimeGroups.Find(Id);
        if (Group == null) return;
        List<LanotaTapNote> Taps = new List<LanotaTapNote>();
        List<LanotaHoldNote> Holds = new List<LanotaHoldNote>();
        foreach (LanotaTapNote Tap in TunerManager.TapNoteManager.TapNote) if (Tap.Group == Id) Taps.Add(Tap);
        foreach (LanotaHoldNote Hold in TunerManager.HoldNoteManager.HoldNote) if (Hold.Group == Id) Holds.Add(Hold);

        System.Action Remove = () =>
        {
            SelectNothing();
            foreach (LanotaTapNote Tap in Taps) DeleteTapNote(Tap, false);
            foreach (LanotaHoldNote Hold in Holds) DeleteHoldNote(Hold, false);
            RemoveTimeGroupRaw(Group);
            if (InspectorManager != null) InspectorManager.OnSelectChange();
        };
        System.Action Restore = () =>
        {
            AddTimeGroupRaw(Group);
            foreach (LanotaTapNote Tap in Taps) { Tap.Group = Id; AddTapNote(Tap, false, false, false); }
            foreach (LanotaHoldNote Hold in Holds) { Hold.Group = Id; AddHoldNote(Hold, false, false, false); }
            LimTimeGroups.RaiseChanged();
        };
        Remove();
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() => { Remove(); });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() => { Restore(); });
        AddToOperationSaver(OpSave);
    }

    public void RenameTimeGroup(int Id, string Name)
    {
        LanotaTimeGroup Group = LimTimeGroups.Find(Id);
        if (Group == null) return;
        Group.Name = Name ?? string.Empty;
    }

    /// <summary>
    /// Puts every selected note into a group, rails included. One undo for
    /// the lot, which gives each note back the group it came from.
    /// </summary>
    public int MoveSelectedNotesToGroup(int Id)
    {
        if (!LimTimeGroups.Exists(Id)) return 0;
        Dictionary<LanotaTapNote, int> TapWas = new Dictionary<LanotaTapNote, int>();
        Dictionary<LanotaHoldNote, int> HoldWas = new Dictionary<LanotaHoldNote, int>();
        foreach (LanotaTapNote Tap in SelectedTapNote) if (Tap.Group != Id) TapWas[Tap] = Tap.Group;
        foreach (LanotaHoldNote Hold in SelectedHoldNote) if (Hold.Group != Id) HoldWas[Hold] = Hold.Group;
        int Moved = TapWas.Count + HoldWas.Count;
        if (Moved == 0) return 0;

        System.Action Apply = () =>
        {
            foreach (LanotaTapNote Tap in TapWas.Keys) Tap.Group = Id;
            foreach (LanotaHoldNote Hold in HoldWas.Keys) Hold.Group = Id;
            LimTimeGroups.RaiseChanged();
        };
        System.Action Undo = () =>
        {
            foreach (KeyValuePair<LanotaTapNote, int> Pair in TapWas) Pair.Key.Group = Pair.Value;
            foreach (KeyValuePair<LanotaHoldNote, int> Pair in HoldWas) Pair.Key.Group = Pair.Value;
            LimTimeGroups.RaiseChanged();
        };
        Apply();
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() => { Apply(); });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() => { Undo(); });
        AddToOperationSaver(OpSave);
        return Moved;
    }

    /// <summary>
    /// A scroll speed for a group, at a moment, at speed 1: the same thing
    /// Create Scroll Speed makes for the chart's own list.
    /// </summary>
    public bool AddGroupScrollSpeed(int Id, float Time)
    {
        LanotaTimeGroup Group = LimTimeGroups.Find(Id);
        if (Group == null) return false;
        if (Time < 0) Time = 0;
        if (!LimSystem.Preferences.Unsafe)
        {
            foreach (LanotaScroll Existing in Group.Scroll) if (Existing.Time == Time) return false;
        }
        Group.Scroll.Add(new LanotaScroll { Speed = 1, Time = Time });
        Group.Scroll.Sort((LanotaScroll A, LanotaScroll B) => { return A.Time.CompareTo(B.Time); });
        LimTimeGroups.RaiseChanged();
        return true;
    }

    /// <summary>
    /// A key for a group's opacity or rotation at the playhead. An opacity
    /// key starts at the opacity the group already has there and a rotation
    /// key at no turn at all, so adding one changes nothing until it is edited.
    /// </summary>
    public LanotaGroupKey AddGroupKey(int Id, bool Opacity)
    {
        LanotaTimeGroup Group = LimTimeGroups.Find(Id);
        if (Group == null) return null;
        List<LanotaGroupKey> Keys = Opacity ? Group.Opacity : Group.Rotation;
        float Time = Mathf.Max(0, TunerManager.ChartTime);
        foreach (LanotaGroupKey Existing in Keys) if (Mathf.Abs(Existing.Time - Time) < 0.0001f) return null;
        LanotaGroupKey Key = new LanotaGroupKey { Time = Time, Duration = 1, Ease = 0 };
        Key.Value = Opacity ? Mathf.Round(LimTimeGroups.WalkDestination(Group.Opacity, Time, 100f, TunerManager.CameraManager)) : 0;
        Keys.Add(Key);
        SortGroupKeys(Group);
        LimTimeGroups.RaiseChanged();
        return Key;
    }

    public void DeleteGroupKey(int Id, LanotaGroupKey Key)
    {
        LanotaTimeGroup Group = LimTimeGroups.Find(Id);
        if (Group == null || Key == null) return;
        if (Key.ListGameObject != null) Destroy(Key.ListGameObject);
        Group.Opacity.Remove(Key);
        Group.Rotation.Remove(Key);
        LimTimeGroups.RaiseChanged();
    }

    /// <summary>
    /// Kept in time order after any edit, which the walk that reads them
    /// relies on. The rows on screen keep their order until the panel is next
    /// rebuilt, so a field being typed in is never pulled out from under the
    /// cursor.
    /// </summary>
    public void SortGroupKeys(LanotaTimeGroup Group)
    {
        System.Comparison<LanotaGroupKey> ByTime = (LanotaGroupKey A, LanotaGroupKey B) => { return A.Time.CompareTo(B.Time); };
        Group.Opacity.Sort(ByTime);
        Group.Rotation.Sort(ByTime);
    }

    public void SetGroupFade(int Id, float FadeIn, float FadeOut)
    {
        LanotaTimeGroup Group = LimTimeGroups.Find(Id);
        if (Group == null) return;
        Group.FadeIn = Mathf.Clamp(FadeIn, 0, 100);
        Group.FadeOut = Mathf.Clamp(FadeOut, 0, 100);
    }

    /// <summary>RRGGBB; empty takes the tint off.</summary>
    public bool SetGroupColor(int Id, string Hex)
    {
        LanotaTimeGroup Group = LimTimeGroups.Find(Id);
        if (Group == null) return false;
        string Clean = (Hex ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();
        Color Tint;
        if (Clean.Length != 0 && !LimTimeGroups.TryParseTint(Clean, out Tint)) return false;
        Group.Color = Clean;
        return true;
    }

    /// <summary>Whether a group's colour reaches its notes, and the glow of the highlighted ones.</summary>
    public void SetGroupColorParts(int Id, bool Notes, bool Highlight)
    {
        LanotaTimeGroup Group = LimTimeGroups.Find(Id);
        if (Group == null) return;
        Group.ColorNotes = Notes;
        Group.ColorHighlight = Highlight;
    }

    private void AddTimeGroupRaw(LanotaTimeGroup Group)
    {
        if (!ChartTimeGroups.Contains(Group)) ChartTimeGroups.Add(Group);
        LimTimeGroups.NoteIdUsed(Group.Id);
        LimTimeGroups.Reindex();
        LimTimeGroups.RaiseChanged();
    }

    private void RemoveTimeGroupRaw(LanotaTimeGroup Group)
    {
        foreach (LanotaScroll Speed in Group.Scroll)
        {
            if (Speed.ListGameObject != null) Destroy(Speed.ListGameObject);
        }
        ChartTimeGroups.Remove(Group);
        LimTimeGroups.Reindex();
        LimTimeGroups.RaiseChanged();
    }
}
