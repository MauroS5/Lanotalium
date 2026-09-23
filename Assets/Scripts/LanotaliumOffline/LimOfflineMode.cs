using UnityEngine;

/// <summary>
/// Master switch for every outbound network feature of Lanotalium.
///
/// While Enabled is true the editor never contacts a remote server:
/// update check, ChartZone browsing and downloads, cloud sync, chart
/// submission, Layesta login/upload and the ffmpeg downloader all turn
/// into no-ops.
///
/// Local file access is deliberately untouched. LimProjectManager loads
/// audio and background images through WWW / UnityWebRequest using
/// "file:///" URLs; blocking those would stop projects from loading.
/// Only http:// and https:// calls are disabled.
///
/// Set Enabled to false to restore the original online behaviour.
/// </summary>
public static class LimOfflineMode
{
    public static readonly bool Enabled = true;

    /// <summary>
    /// Records a blocked remote call in the Unity console so it is
    /// obvious why an online button did nothing.
    /// </summary>
    public static void LogBlocked(string Feature)
    {
        Debug.Log("[LimOfflineMode] Blocked remote call: " + Feature);
    }
}
