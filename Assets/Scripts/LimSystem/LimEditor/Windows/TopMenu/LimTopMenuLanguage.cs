using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The language, as a flag at the top right beside Plugin: the flag of the
/// language in use, and a click lists every language pack's flag and name to
/// switch to. Someone who opened the editor in a language they cannot read can
/// find it there without having to read the menus. Built at runtime next to
/// the scene's Plugin button, like the rest of the top menu's additions.
/// </summary>
public partial class LimTopMenuManager
{
    /// <summary>Which flag in Resources/Flags each language pack wears; a pack not listed shows its name instead.</summary>
    private static readonly Dictionary<string, string> LanguageFlagFiles = new Dictionary<string, string>
    {
        { "English", "En" },
        { "Español", "Es" },
    };
    private const float LanguageButtonWidth = 40f, LanguageRowHeight = 30f, LanguagePanelWidth = 150f;

    private Image LanguageFlag;
    private Text LanguageFallback;
    private GameObject LanguagePanel;

    private void SetUpLanguageMenu()
    {
        // The scene's Plugin group: a 100 x 30 cell anchored to the top right,
        // holding the Plugin button. Its PluginPanel field is not assigned in
        // the scene, so it is found by its name (its words are set later).
        RectTransform Plugin = transform.Find("Plugin") as RectTransform;
        if (Plugin == null) return;
        // The button's own words, for their face and size; the group's first
        // Text would be one inside the Plugin drop-down.
        Transform ButtonWords = Plugin.Find("Plugin/Text");
        Text Reference = ButtonWords != null ? ButtonWords.GetComponent<Text>() : null;

        GameObject Group = new GameObject("Language", typeof(RectTransform));
        Group.layer = Plugin.gameObject.layer;
        RectTransform Cell = Group.GetComponent<RectTransform>();
        Cell.SetParent(transform, false);
        Cell.anchorMin = Cell.anchorMax = new Vector2(1, 1);
        Cell.pivot = new Vector2(1, 1);
        Cell.sizeDelta = new Vector2(LanguageButtonWidth, Plugin.sizeDelta.y);
        // Just left of the Plugin cell, whose right edge is at its position.
        Cell.anchoredPosition = new Vector2(Plugin.anchoredPosition.x - Plugin.sizeDelta.x, Plugin.anchoredPosition.y);

        Image Face = Group.AddComponent<Image>();
        Face.color = new Color(0, 0, 0, 0.001f);
        Button Open = Group.AddComponent<Button>();
        Open.transition = Selectable.Transition.None;
        Open.targetGraphic = Face;
        Open.onClick.AddListener(() => LanguagePanel.SetActive(!LanguagePanel.activeSelf));

        LanguageFlag = NewFlagImage(Cell, "Flag", new Vector2(0.5f, 0.5f), Plugin.sizeDelta.y - 6);
        LanguageFallback = NewLanguageText(Cell, "Name", Reference, TextAnchor.MiddleCenter);
        LanguageFallback.rectTransform.anchorMin = Vector2.zero;
        LanguageFallback.rectTransform.anchorMax = Vector2.one;
        LanguageFallback.rectTransform.sizeDelta = Vector2.zero;
        LanguageFallback.resizeTextForBestFit = true;
        LanguageFallback.resizeTextMinSize = 8;
        LanguageFallback.resizeTextMaxSize = Reference != null ? Reference.fontSize : 18;

        LimMouseOverHint Hint = Group.AddComponent<LimMouseOverHint>();
        Hint.HintTextDictKey = "TopMenu_Language";
        if (Reference != null) Hint.Font = Reference.font;

        BuildLanguagePanel(Cell, Reference);
        Group.AddComponent<LimClosesOnOutsideClick>().Panel = LanguagePanel;
        ShowLanguageFlag();
    }

    private void BuildLanguagePanel(RectTransform Cell, Text Reference)
    {
        LimLanguageManager Languages = FindObjectOfType<LimLanguageManager>();
        List<string> Names = Languages != null && Languages.LanguagePackages != null ? new List<string>(Languages.LanguagePackages.Keys) : new List<string>();
        Names.Sort(System.StringComparer.OrdinalIgnoreCase);

        LanguagePanel = new GameObject("LanguagePanel", typeof(RectTransform));
        LanguagePanel.layer = Cell.gameObject.layer;
        RectTransform Panel = LanguagePanel.GetComponent<RectTransform>();
        Panel.SetParent(Cell, false);
        Panel.anchorMin = Panel.anchorMax = new Vector2(1, 0);
        Panel.pivot = new Vector2(1, 1);
        Panel.anchoredPosition = Vector2.zero;
        Panel.sizeDelta = new Vector2(LanguagePanelWidth, LanguageRowHeight * Names.Count);
        Image Back = LanguagePanel.AddComponent<Image>();
        // The Plugin drop-down's grey, through the theme like the other menus.
        LimThemeManager.Paint(Back, new Color(0.392f, 0.392f, 0.392f));

        for (int i = 0; i < Names.Count; ++i)
        {
            string Name = Names[i];
            GameObject Row = new GameObject(Name, typeof(RectTransform));
            Row.layer = Panel.gameObject.layer;
            RectTransform Rect = Row.GetComponent<RectTransform>();
            Rect.SetParent(Panel, false);
            Rect.anchorMin = new Vector2(0, 1);
            Rect.anchorMax = new Vector2(1, 1);
            Rect.pivot = new Vector2(0.5f, 1);
            Rect.sizeDelta = new Vector2(0, LanguageRowHeight);
            Rect.anchoredPosition = new Vector2(0, -i * LanguageRowHeight);
            Image Shade = Row.AddComponent<Image>();
            Shade.color = new Color(1, 1, 1, 0);
            Button Pick = Row.AddComponent<Button>();
            Pick.targetGraphic = Shade;
            ColorBlock Colours = Pick.colors;
            Colours.normalColor = new Color(1, 1, 1, 0);
            Colours.highlightedColor = new Color(1, 1, 1, 0.18f);
            Colours.pressedColor = new Color(1, 1, 1, 0.3f);
            Pick.colors = Colours;
            Pick.onClick.AddListener(() => ChooseLanguage(Name));

            Image Flag = NewFlagImage(Rect, "Flag", new Vector2(0, 0.5f), LanguageRowHeight - 6);
            Flag.rectTransform.anchoredPosition = new Vector2(6 + (LanguageRowHeight - 6) / 2, 0);
            Sprite Picture = FlagOf(Name);
            Flag.sprite = Picture;
            Flag.enabled = Picture != null;
            Text Words = NewLanguageText(Rect, "Name", Reference, TextAnchor.MiddleLeft);
            Words.text = Name;
            Words.rectTransform.anchorMin = Vector2.zero;
            Words.rectTransform.anchorMax = Vector2.one;
            Words.rectTransform.offsetMin = new Vector2(LanguageRowHeight + 8, 0);
            Words.rectTransform.offsetMax = new Vector2(-4, 0);
        }
        LanguagePanel.SetActive(false);
    }

    private Image NewFlagImage(RectTransform Parent, string Name, Vector2 Anchor, float Side)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        Holder.layer = Parent.gameObject.layer;
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = Rect.anchorMax = Anchor;
        Rect.pivot = new Vector2(0.5f, 0.5f);
        Rect.sizeDelta = new Vector2(Side, Side);
        Rect.anchoredPosition = Vector2.zero;
        Image Flag = Holder.AddComponent<Image>();
        Flag.preserveAspect = true;
        Flag.raycastTarget = false;
        return Flag;
    }

    private static Text NewLanguageText(RectTransform Parent, string Name, Text Reference, TextAnchor Alignment)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        Holder.layer = Parent.gameObject.layer;
        Holder.GetComponent<RectTransform>().SetParent(Parent, false);
        Text Words = Holder.AddComponent<Text>();
        Words.font = Reference != null ? Reference.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        Words.fontSize = Reference != null ? Reference.fontSize : 18;
        Words.color = Color.white;
        Words.alignment = Alignment;
        Words.horizontalOverflow = HorizontalWrapMode.Overflow;
        Words.verticalOverflow = VerticalWrapMode.Overflow;
        Words.raycastTarget = false;
        return Words;
    }

    private static readonly Dictionary<string, Sprite> FlagSprites = new Dictionary<string, Sprite>();

    private static Sprite FlagOf(string LanguageName)
    {
        string File;
        if (string.IsNullOrEmpty(LanguageName) || !LanguageFlagFiles.TryGetValue(LanguageName, out File)) return null;
        Sprite Made;
        if (FlagSprites.TryGetValue(File, out Made) && Made != null) return Made;
        Made = Resources.Load<Sprite>("Flags/" + File);
        if (Made == null)
        {
            Texture2D Picture = Resources.Load<Texture2D>("Flags/" + File);
            if (Picture != null) Made = Sprite.Create(Picture, new Rect(0, 0, Picture.width, Picture.height), new Vector2(0.5f, 0.5f));
        }
        FlagSprites[File] = Made;
        return Made;
    }

    /// <summary>The flag of the language in use; its name when there is no flag for it.</summary>
    private void ShowLanguageFlag()
    {
        if (LanguageFlag == null) return;
        string Current = LimSystem.Preferences.LanguageName;
        Sprite Picture = FlagOf(Current);
        LanguageFlag.sprite = Picture;
        LanguageFlag.enabled = Picture != null;
        LanguageFallback.text = Picture != null ? string.Empty : Current;
    }

    private void ChooseLanguage(string Name)
    {
        LanguagePanel.SetActive(false);
        LimLanguageManager Languages = FindObjectOfType<LimLanguageManager>();
        if (Languages == null) return;
        // As the Preferences window's drop-down does it.
        Languages.SetLanguage(Name);
        LimSystem.Preferences.LanguageName = Name;
        ShowLanguageFlag();
    }
}

/// <summary>Hides a drop-down when the pointer is pressed anywhere outside it and the control that opens it.</summary>
public class LimClosesOnOutsideClick : MonoBehaviour
{
    public GameObject Panel;

    private void Update()
    {
        if (Panel == null || !Panel.activeSelf) return;
        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) return;
        Canvas Owner = GetComponentInParent<Canvas>();
        Camera Eye = Owner != null && Owner.renderMode != RenderMode.ScreenSpaceOverlay ? Owner.worldCamera : null;
        if (RectTransformUtility.RectangleContainsScreenPoint(transform as RectTransform, Input.mousePosition, Eye)) return;
        if (RectTransformUtility.RectangleContainsScreenPoint(Panel.transform as RectTransform, Input.mousePosition, Eye)) return;
        Panel.SetActive(false);
    }
}
