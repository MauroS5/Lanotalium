using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Keeps a spare copy of the chart while you work.
///
/// Every few minutes, and again on the way out, the chart is written to a new
/// timestamped file in an AutoSaves folder beside the project. Nothing is ever
/// overwritten, so a crash costs at most the last few minutes and the copies
/// stay there to be picked from. Only the newest handful are kept: a long
/// session would otherwise leave hundreds of files nobody will ever open.
///
/// The loop used to start only if a chart was already open, and the editor
/// starts with none: opening a project afterwards, which is how it is always
/// done, left autosave switched off for the rest of the session however the
/// preference was set. It now runs from the start and looks for a chart each
/// time round.
/// </summary>
public class LimAutosaver : MonoBehaviour
{
    /// <summary>How long between copies. Short enough to lose little, long enough not to litter.</summary>
    private const float AutosaveSeconds = 300;
    /// <summary>The folder they go in, beside the chart rather than among it.</summary>
    private const string AutosaveFolder = "AutoSaves";
    /// <summary>How many are kept. Two hours of work at the current interval.</summary>
    private const int AutosavesKept = 24;

    private Coroutine AutosaveCoroutineRef;

    public void Start()
    {
        AutosaveCoroutineRef = StartCoroutine(AutosaveCoroutine());
    }
    private void OnDestroy()
    {
        StopAutosave();
    }
    public static string CurrentTimeString()
    {
        return string.Format("{0}-{1}-{2} {3}-{4}-{5}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
    }
    IEnumerator AutosaveCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(AutosaveSeconds);
            yield return new WaitForEndOfFrame();
            Autosave();
        }
    }
    public void StopAutosave()
    {
        if (AutosaveCoroutineRef != null) StopCoroutine(AutosaveCoroutineRef);
    }

    /// <summary>
    /// Writes one copy, if there is a chart and the preference is on. Quiet
    /// about failures on purpose: a locked folder or a full disk should not
    /// throw an exception dialog over the editor every few minutes.
    /// </summary>
    public static void Autosave()
    {
        if (LimSystem.ChartContainer == null) return;
        if (!LimSystem.Preferences.Autosave) return;
        if (LimSystem.ChartContainer.ChartData == null) return;
        if (LimSystem.ChartContainer.ChartProperty == null) return;

        string Folder = LimSystem.ChartContainer.ChartProperty.ChartFolder;
        if (string.IsNullOrEmpty(Folder)) return;
        Folder = Folder + "/" + AutosaveFolder;
        try
        {
            // Made when it is needed: the folder cannot be prepared at
            // startup, when there is no chart and so no folder to put it in.
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + string.Format("/{0}.txt", CurrentTimeString()), LimSystem.ChartContainer.ChartData.ToString());
            ForgetOldestCopies(Folder);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Keeps the folder to the newest few copies. Sorted by the time the file
    /// was written rather than by its name: the name is a timestamp, but one
    /// whose parts are not padded, so as text it sorts wrongly.
    /// </summary>
    private static void ForgetOldestCopies(string Folder)
    {
        string[] Copies = Directory.GetFiles(Folder, "*.txt");
        if (Copies.Length <= AutosavesKept) return;
        Array.Sort(Copies, (string Left, string Right) =>
        {
            return File.GetLastWriteTimeUtc(Left).CompareTo(File.GetLastWriteTimeUtc(Right));
        });
        for (int i = 0; i < Copies.Length - AutosavesKept; ++i)
        {
            try { File.Delete(Copies[i]); }
            catch (Exception) { }
        }
    }

    private void OnApplicationQuit()
    {
        Autosave();
    }
}
