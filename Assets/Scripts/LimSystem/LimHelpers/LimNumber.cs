using System.Globalization;

/// <summary>
/// Reads numbers typed into the editor's fields.
///
/// Values are written with a point, and a point is what a field expects, but
/// a comma is accepted too and means the same thing: charting with the
/// numeric keypad of a Spanish keyboard should not depend on which key that
/// pad happens to produce. Thousands separators are not allowed, so nothing
/// is silently read as a different number.
/// </summary>
public static class LimNumber
{
    public static bool TryParseFloat(string Text, out float Value)
    {
        Value = 0;
        if (string.IsNullOrEmpty(Text)) return false;
        string Normalized = Text.Trim().Replace(',', '.');
        return float.TryParse(Normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out Value);
    }
}
