using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The UiTweak menu, right after Analyzer: every switch for what came from
/// Flowaria's plugins, each entry ticked while it is on, and the tuner skins
/// in a list opening beside it. Flowaria gave permission for their use.
///
/// Built at runtime from a copy of the Analyzer's button and drop-down (the
/// scene's Chart Convert ones), so it looks like the rest of the menu bar.
/// The drop-down closes itself when the pointer leaves it, as the others do;
/// clicking an entry keeps it open so several can be switched in a row. The
/// skin list is a child of the drop-down, so moving onto it does not count
/// as leaving, and it is filled from disk each time it opens.
/// </summary>
public partial class LimTopMenuManager
{
    private const float MenuRowHeight = 30, MenuPanelWidth = 300, CheckWidth = 22;

    private class MenuEntry
    {
        public Text Label, Check;
        public string TextKey;
        public Func<bool> IsOn;
    }

    private GameObject UiTweakPanel, SkinPanel, MenuRowTemplate;
    private Text UiTweakTabText, SkinEntryLabel, CreditLabel;
    private readonly List<MenuEntry> UiTweakEntries = new List<MenuEntry>();
    private readonly List<MenuEntry> SkinEntries = new List<MenuEntry>();
    private readonly List<GameObject> SkinRows = new List<GameObject>();
    private LimTunerWindowManager SkinWindow;

    private void SetUpUiTweakMenu()
    {
        if (UiTweakPanel != null || ChartConvertText == null) return;
        Transform TabButton = ChartConvertText.transform.parent;
        Transform Group = TabButton != null ? TabButton.parent : null;
        if (Group == null) return;
        GameObject Copy = Instantiate(Group.gameObject, Group.parent);
        LimThemeManager.Adopt(Group.gameObject, Copy);
        Copy.name = "UiTweak";
        foreach (LimMouseOverHint Hint in Copy.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
        RectTransform GroupRect = Group as RectTransform, CopyRect = Copy.GetComponent<RectTransform>();
        RectTransform ButtonRect = TabButton as RectTransform;
        CopyRect.anchoredPosition = GroupRect.anchoredPosition + new Vector2(ButtonRect.sizeDelta.x, 0);

        Transform Tab = Copy.transform.Find(TabButton.name);
        Button TabClick = Tab.GetComponent<Button>();
        UiTweakTabText = Tab.GetComponentInChildren<Text>(true);
        UiTweakPanel = Copy.transform.Find(ChartConvertPanel.name).gameObject;
        UiTweakPanel.name = "UiTweakPanel";
        TabClick.onClick = new Button.ButtonClickedEvent();
        TabClick.onClick.AddListener(() =>
        {
            bool Open = !UiTweakPanel.activeSelf;
            UiTweakPanel.SetActive(Open);
            if (SkinPanel != null) SkinPanel.SetActive(false);
            if (Open) RefreshUiTweakMenu();
        });

        // One of the copied entries stays as the pattern for every row.
        MenuRowTemplate = UiTweakPanel.transform.GetChild(0).gameObject;
        for (int i = UiTweakPanel.transform.childCount - 1; i >= 1; --i) DestroyImmediate(UiTweakPanel.transform.GetChild(i).gameObject);
        MenuRowTemplate.SetActive(false);

        // The skin list: a copy of the empty drop-down, hung off its side.
        SkinPanel = Instantiate(UiTweakPanel, UiTweakPanel.transform);
        SkinPanel.name = "SkinPanel";
        DestroyImmediate(SkinPanel.transform.GetChild(0).gameObject);
        RectTransform SkinRect = SkinPanel.GetComponent<RectTransform>();
        SkinRect.anchorMin = SkinRect.anchorMax = new Vector2(0, 1);
        SkinRect.pivot = new Vector2(0, 1);
        SkinPanel.SetActive(false);

        AddSwitch("UiTweak_NoteEffects", () => LimSystem.Preferences.NoteEffects, () => LimSystem.Preferences.NoteEffects = !LimSystem.Preferences.NoteEffects);
        AddSwitch("UiTweak_ComboCounter", () => LimSystem.Preferences.ComboCounter, () => LimSystem.Preferences.ComboCounter = !LimSystem.Preferences.ComboCounter);
        AddSwitch("UiTweak_FlickArrows", () => LimSystem.Preferences.FlickArrows, () => LimSystem.Preferences.FlickArrows = !LimSystem.Preferences.FlickArrows);
        AddSwitch("UiTweak_JudgeOrnaments", () => LimSystem.Preferences.JudgeLineOrnaments, () => LimSystem.Preferences.JudgeLineOrnaments = !LimSystem.Preferences.JudgeLineOrnaments);
        AddSwitch("UiTweak_Wave", () => LimSystem.Preferences.BackgroundWave, () => LimSystem.Preferences.BackgroundWave = !LimSystem.Preferences.BackgroundWave);
        AddSwitch("UiTweak_LanotaHeader", () => LimSystem.Preferences.LanotaHeader, () => LimSystem.Preferences.LanotaHeader = !LimSystem.Preferences.LanotaHeader);
        AddSwitch("UiTweak_ShowScore", () => LimSystem.Preferences.ShowScore, () => LimSystem.Preferences.ShowScore = !LimSystem.Preferences.ShowScore);
        AddSwitch("UiTweak_ProgressBar", () => LimSystem.Preferences.ProgressBar, () => LimSystem.Preferences.ProgressBar = !LimSystem.Preferences.ProgressBar);
        AddAction("UiTweak_CustomDifficulty", () =>
        {
            UiTweakPanel.SetActive(false);
            LimLanotaHeader Header = FindObjectOfType<LimLanotaHeader>();
            if (Header != null) Header.OpenCustomDifficulty();
        });
        AddSwitch("UiTweak_PerfectPurified", () => LimSystem.Preferences.PerfectPurified, () => LimSystem.Preferences.PerfectPurified = !LimSystem.Preferences.PerfectPurified);
        AddSwitch("UiTweak_ReadyIntro", () => LimSystem.Preferences.ReadyIntro, () => LimSystem.Preferences.ReadyIntro = !LimSystem.Preferences.ReadyIntro);
        AddSwitch("UiTweak_HdRails", () => LimSystem.Preferences.HdRails, () => LimSystem.Preferences.HdRails = !LimSystem.Preferences.HdRails);
        AddSwitch("UiTweak_HdCore", () => LimSystem.Preferences.HdCore, () => LimSystem.Preferences.HdCore = !LimSystem.Preferences.HdCore);
        AddSwitch("UiTweak_CompactHighlight", () => LimSystem.Preferences.CompactHighlight, () => LimSystem.Preferences.CompactHighlight = !LimSystem.Preferences.CompactHighlight);
        AddSkinEntry();
        AddCredit();
        SizePanel(UiTweakPanel, UiTweakEntries.Count + 2);
        if (LimLanguageManager.TextDict != null) SetUiTweakTexts();
    }

    private GameObject NewRow(GameObject Panel, string Name, int Index, out Text Label, out Text Check, out Button Click)
    {
        GameObject Row = Instantiate(MenuRowTemplate, Panel.transform);
        LimThemeManager.Adopt(MenuRowTemplate, Row);
        Row.name = Name;
        Row.SetActive(true);
        RectTransform Rect = Row.GetComponent<RectTransform>();
        Rect.anchoredPosition = new Vector2(Rect.anchoredPosition.x, -MenuRowHeight * Index);
        Label = Row.GetComponentInChildren<Text>(true);
        GameObject Mark = Instantiate(Label.gameObject, Row.transform);
        Mark.name = "Check";
        Check = Mark.GetComponent<Text>();
        RectTransform MarkRect = Check.rectTransform;
        MarkRect.anchorMin = new Vector2(0, 0);
        MarkRect.anchorMax = new Vector2(0, 1);
        MarkRect.pivot = new Vector2(0, 0.5f);
        MarkRect.anchoredPosition = new Vector2(3, 0);
        MarkRect.sizeDelta = new Vector2(CheckWidth, 0);
        Check.alignment = TextAnchor.MiddleCenter;
        Check.text = string.Empty;
        Click = Row.GetComponent<Button>();
        Click.onClick = new Button.ButtonClickedEvent();
        return Row;
    }

    private void AddSwitch(string TextKey, Func<bool> IsOn, Action Flip)
    {
        Text Label, Check;
        Button Click;
        GameObject Row = NewRow(UiTweakPanel, TextKey, UiTweakEntries.Count, out Label, out Check, out Click);
        Click.onClick.AddListener(() =>
        {
            Flip();
            RefreshUiTweakMenu();
        });
        AddMenuHint(Row, TextKey);
        UiTweakEntries.Add(new MenuEntry { Label = Label, Check = Check, TextKey = TextKey, IsOn = IsOn });
    }

    /// <summary>An entry that does something once rather than switching; it has no tick, only its words.</summary>
    private void AddAction(string TextKey, Action Act)
    {
        Text Label, Check;
        Button Click;
        GameObject Row = NewRow(UiTweakPanel, TextKey, UiTweakEntries.Count, out Label, out Check, out Click);
        Click.onClick.AddListener(() => Act());
        AddMenuHint(Row, TextKey);
        UiTweakEntries.Add(new MenuEntry { Label = Label, Check = Check, TextKey = TextKey, IsOn = () => false });
    }

    private void AddSkinEntry()
    {
        Text Label, Arrow;
        Button Click;
        int Index = UiTweakEntries.Count;
        GameObject Row = NewRow(UiTweakPanel, "TunerSkin", Index, out Label, out Arrow, out Click);
        // The arrow saying a list opens goes on the right.
        RectTransform ArrowRect = Arrow.rectTransform;
        ArrowRect.anchorMin = new Vector2(1, 0);
        ArrowRect.anchorMax = new Vector2(1, 1);
        ArrowRect.pivot = new Vector2(1, 0.5f);
        ArrowRect.anchoredPosition = new Vector2(-6, 0);
        Arrow.text = "▶";
        SkinEntryLabel = Label;
        Click.onClick.AddListener(() =>
        {
            bool Open = !SkinPanel.activeSelf;
            if (Open) FillSkinPanel(Index);
            SkinPanel.SetActive(Open);
        });
        AddMenuHint(Row, "UiTweak_TunerSkin");
    }

    /// <summary>Flowaria's name at the foot of the menu, which does nothing when clicked.</summary>
    private void AddCredit()
    {
        Text Label, Check;
        Button Click;
        GameObject Row = NewRow(UiTweakPanel, "Credit", UiTweakEntries.Count + 1, out Label, out Check, out Click);
        Click.interactable = false;
        Label.fontStyle = FontStyle.Italic;
        CreditLabel = Label;
    }

    private void SizePanel(GameObject Panel, int Rows)
    {
        RectTransform Rect = Panel.GetComponent<RectTransform>();
        Rect.sizeDelta = new Vector2(MenuPanelWidth, MenuRowHeight * Rows + 1);
    }

    private void AddMenuHint(GameObject Target, string TextKey)
    {
        LimMouseOverHint Hint = Target.AddComponent<LimMouseOverHint>();
        Hint.HintTextDictKey = TextKey;
        Hint.Font = FileText != null ? FileText.font : null;
    }

    /// <summary>Ritmo, Física, then every folder of StreamingAssets/TunerSkin, the one in use ticked.</summary>
    private void FillSkinPanel(int BesideRow)
    {
        foreach (GameObject Row in SkinRows) if (Row != null) Destroy(Row);
        SkinRows.Clear();
        SkinEntries.Clear();
        List<string> Names = LimTunerSkins.Available();
        AddSkinRow(LimLanguageManager.TextDict["UiTweak_TunerSkin_Ritmo"], () => IsBuiltIn(Lanotalium.Editor.TunerSkin.Ritmo), () => UseBuiltIn(Lanotalium.Editor.TunerSkin.Ritmo));
        AddSkinRow(LimLanguageManager.TextDict["UiTweak_TunerSkin_Fisica"], () => IsBuiltIn(Lanotalium.Editor.TunerSkin.Fisica), () => UseBuiltIn(Lanotalium.Editor.TunerSkin.Fisica));
        foreach (string Name in Names)
        {
            string Chosen = Name;
            AddSkinRow(Chosen, () => LimSystem.Preferences.CustomTunerSkin == Chosen, () =>
            {
                LimSystem.Preferences.CustomTunerSkin = Chosen;
                LimTunerSkins.Refresh(false);
            });
        }
        SizePanel(SkinPanel, SkinEntries.Count);
        RectTransform Rect = SkinPanel.GetComponent<RectTransform>();
        Rect.anchoredPosition = new Vector2(MenuPanelWidth, -MenuRowHeight * BesideRow);
        RefreshSkinChecks();
    }

    private void AddSkinRow(string Name, Func<bool> IsOn, Action Use)
    {
        Text Label, Check;
        Button Click;
        GameObject Row = NewRow(SkinPanel, Name, SkinEntries.Count, out Label, out Check, out Click);
        Label.text = Name;
        Click.onClick.AddListener(() =>
        {
            Use();
            RefreshSkinChecks();
        });
        SkinRows.Add(Row);
        SkinEntries.Add(new MenuEntry { Label = Label, Check = Check, IsOn = IsOn });
    }

    private static bool IsBuiltIn(Lanotalium.Editor.TunerSkin Skin)
    {
        return string.IsNullOrEmpty(LimSystem.Preferences.CustomTunerSkin) && LimSystem.Preferences.TunerSkin == Skin;
    }

    /// <summary>The Tuner window's own two skins, chosen the way its Skin panel chooses them.</summary>
    private void UseBuiltIn(Lanotalium.Editor.TunerSkin Skin)
    {
        if (SkinWindow == null) SkinWindow = FindObjectOfType<LimTunerWindowManager>();
        if (SkinWindow != null)
        {
            if (Skin == Lanotalium.Editor.TunerSkin.Ritmo) SkinWindow.UseRitmoSkin();
            else SkinWindow.UseFisicaSkin();
            return;
        }
        LimSystem.Preferences.TunerSkin = Skin;
        LimSystem.Preferences.CustomTunerSkin = string.Empty;
    }

    private void RefreshUiTweakMenu()
    {
        foreach (MenuEntry Entry in UiTweakEntries) Entry.Check.text = Entry.IsOn() ? "✓" : string.Empty;
    }

    private void RefreshSkinChecks()
    {
        foreach (MenuEntry Entry in SkinEntries) Entry.Check.text = Entry.IsOn() ? "✓" : string.Empty;
    }

    private void SetUiTweakTexts()
    {
        if (UiTweakTabText == null) return;
        UiTweakTabText.text = LimLanguageManager.TextDict["TopMenu_UiTweak"];
        foreach (MenuEntry Entry in UiTweakEntries) Entry.Label.text = LimLanguageManager.TextDict[Entry.TextKey];
        if (SkinEntryLabel != null) SkinEntryLabel.text = LimLanguageManager.TextDict["UiTweak_TunerSkin"];
        if (CreditLabel != null) CreditLabel.text = LimLanguageManager.TextDict["UiTweak_Credit"];
        RefreshUiTweakMenu();
    }
}
