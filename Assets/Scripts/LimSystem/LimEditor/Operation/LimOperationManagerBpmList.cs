using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Writing the bpm list as a whole, for the Analyzer: one undo step puts
/// back every entry it replaced, however many it wrote.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>
    /// Replaces the chart's bpm list with Entries, which must start with the
    /// base entry at -3 that every chart carries.
    /// </summary>
    public void ReplaceBpmList(List<Lanotalium.Chart.LanotaChangeBpm> Entries, bool SaveOperation = true)
    {
        List<Lanotalium.Chart.LanotaChangeBpm> Before = CopyBpmList(TunerManager.BpmManager.Bpm);
        List<Lanotalium.Chart.LanotaChangeBpm> After = CopyBpmList(Entries);
        WriteBpmList(CopyBpmList(After));
        if (!SaveOperation) return;
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() => { WriteBpmList(CopyBpmList(After)); });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() => { WriteBpmList(CopyBpmList(Before)); });
        AddToOperationSaver(OpSave);
    }

    /// <summary>
    /// The chart's list with one tempo starting at Time: an entry already
    /// there is replaced, and when it becomes the first entry the base entry
    /// takes the same tempo, so the lines before it do not run at another.
    /// </summary>
    public List<Lanotalium.Chart.LanotaChangeBpm> BpmListWith(float Time, float Bpm)
    {
        List<Lanotalium.Chart.LanotaChangeBpm> Entries = CopyBpmList(TunerManager.BpmManager.Bpm);
        for (int i = Entries.Count - 1; i >= 1; --i)
            if (Mathf.Abs(Entries[i].Time - Time) < 0.001f) Entries.RemoveAt(i);
        Entries.Add(NewBpmEntry(Time, Bpm));
        Entries.Sort((A, B) => A.Time.CompareTo(B.Time));
        if (Entries.Count > 1 && Entries[1].Time == Time) Entries[0].Bpm = Bpm;
        return Entries;
    }

    /// <summary>A fresh list: the base entry and one entry per start time.</summary>
    public List<Lanotalium.Chart.LanotaChangeBpm> BpmListOf(IList<KeyValuePair<float, float>> Starts)
    {
        Lanotalium.Chart.LanotaChangeBpm Base = TunerManager.BpmManager.Bpm.Count > 0
            ? CopyBpm(TunerManager.BpmManager.Bpm[0])
            : new Lanotalium.Chart.LanotaChangeBpm { Type = 6, Time = -3 };
        Base.Bpm = Starts.Count > 0 ? Starts[0].Value : Base.Bpm;
        List<Lanotalium.Chart.LanotaChangeBpm> Entries = new List<Lanotalium.Chart.LanotaChangeBpm> { Base };
        foreach (KeyValuePair<float, float> Start in Starts) Entries.Add(NewBpmEntry(Start.Key, Start.Value));
        return Entries;
    }

    private static Lanotalium.Chart.LanotaChangeBpm NewBpmEntry(float Time, float Bpm)
    {
        return new Lanotalium.Chart.LanotaChangeBpm { Type = 0, Time = Time, Bpm = Bpm };
    }

    private void WriteBpmList(List<Lanotalium.Chart.LanotaChangeBpm> Entries)
    {
        // The list object is the chart's own and other managers hold it, so
        // it is refilled rather than replaced.
        List<Lanotalium.Chart.LanotaChangeBpm> Live = TunerManager.BpmManager.Bpm;
        foreach (Lanotalium.Chart.LanotaChangeBpm Old in Live)
            if (Old.ListGameObject != null) Destroy(Old.ListGameObject);
        Live.Clear();
        Live.AddRange(Entries);
        TunerManager.BpmManager.SortBpmList();
        InspectorManager.ComponentBpm.InstantiateBpmList();
        InspectorManager.ComponentBpm.ReCalculateBeatlineTimes();
    }

    private static List<Lanotalium.Chart.LanotaChangeBpm> CopyBpmList(List<Lanotalium.Chart.LanotaChangeBpm> Entries)
    {
        List<Lanotalium.Chart.LanotaChangeBpm> Copy = new List<Lanotalium.Chart.LanotaChangeBpm>();
        foreach (Lanotalium.Chart.LanotaChangeBpm Entry in Entries) Copy.Add(CopyBpm(Entry));
        return Copy;
    }

    private static Lanotalium.Chart.LanotaChangeBpm CopyBpm(Lanotalium.Chart.LanotaChangeBpm Entry)
    {
        return new Lanotalium.Chart.LanotaChangeBpm
        {
            Type = Entry.Type,
            Time = Entry.Time,
            Duration = Entry.Duration,
            Degree = Entry.Degree,
            Size = Entry.Size,
            Critical = Entry.Critical,
            Combination = Entry.Combination,
            Bpm = Entry.Bpm
        };
    }
}
