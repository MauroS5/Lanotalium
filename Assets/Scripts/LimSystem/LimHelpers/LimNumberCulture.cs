using System.Globalization;
using System.Threading;
using UnityEngine;

/// <summary>
/// Fixes the editor on a point, not a comma, as the decimal separator.
///
/// Without this, every number the editor reads or writes follows the
/// separator Windows was set up with. On a Spanish system that means 1.5 is
/// read as 15, which is wrong in a text field and worse in a file: a chart
/// written with points would be read with every value a hundred times too
/// big. Charts themselves are JSON, which is always written with points, so
/// the two sides only agree once the program stops following the system.
///
/// Set before any scene loads, so it is in force for the very first value
/// read from disk.
/// </summary>
public static class LimNumberCulture
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // Only how numbers and dates are written; the interface language is
        // the language packages' business and is left alone.
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
    }
}
