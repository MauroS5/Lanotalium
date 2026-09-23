using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Analyzer menu, in the place Chart Convert had.
///
/// Chart Convert's BMS and Arcaea importers had gone stale, so its button and
/// drop-down are taken over at runtime rather than removed from the scene:
/// the menu button still opens the same panel, and its two entries now start
/// Automatic and Manual. The converting code itself (LimChartConverting,
/// Schwarzer.Chart) is left in place, unreachable, because the scene still
/// holds a LimChartConverting component and deleting its script would leave
/// a missing-script warning behind.
/// </summary>
public partial class LimTopMenuManager
{
    private LimAnalyzer Analyzer;

    private void SetUpAnalyzer()
    {
        if (Analyzer != null) return;
        Analyzer = gameObject.AddComponent<LimAnalyzer>();
        Rewire(ChartConvertBmsText, Analyzer.RunAutomatic);
        Rewire(ChartConvertArcaeaText, Analyzer.OpenManual);
    }

    private void Rewire(Text Caption, UnityEngine.Events.UnityAction Action)
    {
        if (Caption == null) return;
        // Walked by hand: the drop-down is hidden when this runs, and
        // GetComponentInParent finds nothing on an inactive object, which
        // once left the old importer wired to both entries.
        Button Entry = null;
        for (Transform Step = Caption.transform; Step != null && Entry == null; Step = Step.parent)
            Entry = Step.GetComponent<Button>();
        if (Entry == null)
        {
            Debug.LogError("Analyzer: no button found for " + Caption.name);
            return;
        }
        // A fresh event, so the scene's call to the old importer goes too.
        Entry.onClick = new Button.ButtonClickedEvent();
        Entry.onClick.AddListener(() =>
        {
            ChartConvertPanel.SetActive(false);
            Action();
        });
    }

    private void SetAnalyzerTexts()
    {
        ChartConvertText.text = LimLanguageManager.TextDict["TopMenu_Analyzer"];
        ChartConvertBmsText.text = LimLanguageManager.TextDict["TopMenu_Analyzer_Automatic"];
        ChartConvertArcaeaText.text = LimLanguageManager.TextDict["TopMenu_Analyzer_Manual"];
        if (Analyzer != null) Analyzer.SetManualTexts();
    }
}
