using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// The pictures, particles, animations and fonts of Flowaria's UiTweak, a
/// community plugin for Lanotalium 2.5 (hit effects, combo counter, judge line
/// ornaments, tuner skins, a Lanota style header). Its code is rewritten
/// here as part of the editor; its art is used as Flowaria shipped it: three
/// asset bundles built with Unity 2018.3.0, in StreamingAssets/UiTweak.
///
/// The bundles could not have been turned back into project assets: one of
/// them carries a compiled shader, which has no source to import. They hold
/// no scripts, only built-in components, their layers are the tuner's (8 to
/// 13) and their sorting layer ids are this project's own, so everything in
/// them renders here as it did under the plugin.
///
/// **They cannot be open at the same time**: Unity refuses the second one as
/// "another AssetBundle with the same files is already loaded", because they
/// were built separately and share internal names. The plugin never noticed,
/// since it closed each bundle as soon as it had read it. So does this: a
/// bundle is opened, everything in it is read, and it is closed again at
/// once, keeping what was read. Those objects stay alive across scene loads
/// because they are referenced from here; should one ever be gone, the bundle
/// is simply read again.
/// </summary>
public static class LimUiTweakAssets
{
    public const string ParticleBundle = "uitweak.particle";
    public const string ScreenBundle = "uitweak.screen";
    public const string HeaderBundle = "uitweak.header";

    private struct Entry
    {
        public string Path;
        public Object Asset;
    }

    private static readonly Dictionary<string, List<Entry>> Contents = new Dictionary<string, List<Entry>>();
    private static readonly HashSet<string> Missing = new HashSet<string>();

    /// <summary>
    /// An asset of one of the bundles, by its name or by its path in
    /// Flowaria's project, or null when the bundle or the asset is not there;
    /// a missing bundle is reported once, and the feature asking stays off.
    /// </summary>
    public static T Load<T>(string Bundle, string Name) where T : Object
    {
        List<Entry> Entries = Read(Bundle, false);
        if (Entries == null) return null;
        T Found = Find<T>(Entries, Name);
        if (Found == null && Entries.Exists(E => E.Asset == null && !ReferenceEquals(E.Asset, null)))
        {
            // Something that was read has been destroyed since: read it all again.
            Entries = Read(Bundle, true);
            if (Entries != null) Found = Find<T>(Entries, Name);
        }
        if (Found == null) Debug.LogWarning("UiTweak: " + Bundle + " has no " + typeof(T).Name + " called " + Name);
        return Found;
    }

    private static T Find<T>(List<Entry> Entries, string Name) where T : Object
    {
        foreach (Entry E in Entries)
        {
            T Asset = E.Asset as T;
            if (Asset == null) continue;
            if (string.Equals(E.Path, Name, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(Asset.name, Name, System.StringComparison.OrdinalIgnoreCase)) return Asset;
        }
        return null;
    }

    private static List<Entry> Read(string Name, bool Again)
    {
        List<Entry> Entries;
        if (!Again && Contents.TryGetValue(Name, out Entries)) return Entries;
        if (Missing.Contains(Name)) return null;
        string Path = System.IO.Path.Combine(System.IO.Path.Combine(Application.streamingAssetsPath, "UiTweak"), Name);
        AssetBundle Bundle = File.Exists(Path) ? AssetBundle.LoadFromFile(Path) : null;
        if (Bundle == null)
        {
            Missing.Add(Name);
            Debug.LogWarning("UiTweak: could not open " + Path);
            return null;
        }
        Entries = new List<Entry>();
        try
        {
            foreach (string AssetPath in Bundle.GetAllAssetNames())
            {
                foreach (Object Asset in Bundle.LoadAssetWithSubAssets(AssetPath)) Entries.Add(new Entry { Path = AssetPath, Asset = Asset });
            }
        }
        finally
        {
            // False: what was read stays.
            Bundle.Unload(false);
        }
        Contents[Name] = Entries;
        return Entries;
    }
}
