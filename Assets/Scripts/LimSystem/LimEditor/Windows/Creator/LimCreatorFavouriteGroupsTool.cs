using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Favourite Groups tool and drops it into the Creator window
/// between Click To Create and Angleline.
///
/// Like the Grid tool, it is made from a copy of the Angleline tool, which
/// already carries the folding header and the scrolling body the tools here
/// share, and then emptied and filled with what this one needs: a heading
/// for each kind of pattern and a plus button that keeps the selection.
/// </summary>
public partial class LimCreatorManager
{
    private void CreateFavouriteGroupsTool()
    {
        if (AngleLineManager == null || AngleLineManager.ToolBase == null) return;
        RectTransform Source = AngleLineManager.ToolBase.ToolRect;
        if (Source == null || Source.parent == null) return;

        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = "FavouriteGroups";
        LimAngleLineManager Inherited = Clone.GetComponent<LimAngleLineManager>();
        if (Inherited != null) DestroyImmediate(Inherited);

        LimFavouriteGroupsManager Groups = Clone.AddComponent<LimFavouriteGroupsManager>();
        Groups.ToolBase = Clone.GetComponent<LimCreatorToolBase>();

        Transform Folder = Clone.transform.Find("Folder/Label");
        if (Folder != null) Groups.LabelText = Folder.GetComponent<Text>();

        RectTransform Content = Clone.transform.Find("View/Viewport/Content") as RectTransform;
        if (Content == null) { Destroy(Clone); return; }
        for (int i = Content.childCount - 1; i >= 0; --i) DestroyImmediate(Content.GetChild(i).gameObject);

        // The field and the checkbox of the angleline tool are kept as the
        // patterns to copy from, so the rows match the rest of the panel.
        Groups.ContentRect = Content;
        Groups.NameTemplate = AngleLineManager.AnglelineInputField;
        Groups.TickTemplate = AngleLineManager.EnableToggle;

        Font Face = Groups.NameTemplate != null && Groups.NameTemplate.textComponent != null
            ? Groups.NameTemplate.textComponent.font : null;
        Color Ink = new Color(0.85f, 0.85f, 0.85f);
        Groups.NotesHeader = LimUiBuilder.CreateLabel(Content, "NotesHeader", Face, 16, Ink, TextAnchor.MiddleLeft);
        Groups.MotionsHeader = LimUiBuilder.CreateLabel(Content, "MotionsHeader", Face, 16, Ink, TextAnchor.MiddleLeft);

        CreateHeaderButton(Clone, LimIcons.Favourite, "+", "Favourites_Add", 1, Groups.AddSelection);
        CreateHeaderButton(Clone, LimIcons.Export, "E", "Favourites_Export", 2, Groups.Export);
        CreateHeaderButton(Clone, LimIcons.Import, "I", "Favourites_Import", 3, Groups.Import);

        Groups.Setup(OperationManager);
        FavouriteGroupsManager = Groups;
        if (LimLanguageManager.TextDict != null) Groups.SetTexts();
        Groups.Rebuild();
    }

    /// <summary>
    /// The buttons on the tool's own header bar, counting in from the right,
    /// so they stay put while the list below them grows.
    /// </summary>
    private void CreateHeaderButton(GameObject Tool, string Icon, string Fallback, string HintKey, int Slot,
                                    UnityEngine.Events.UnityAction OnClick)
    {
        RectTransform Header = Tool.transform.Find("Folder") as RectTransform;
        if (Header == null) return;

        GameObject Holder = new GameObject("Header" + HintKey, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Header, false);
        Rect.anchorMin = new Vector2(1, 0.5f);
        Rect.anchorMax = new Vector2(1, 0.5f);
        Rect.pivot = new Vector2(1, 0.5f);
        Rect.anchoredPosition = new Vector2(-5 - (Slot - 1) * 28, 0);
        Rect.sizeDelta = new Vector2(26, 26);

        Image Face = Holder.GetComponent<Image>();
        Image Reference = AngleLineManager.EnableToggle != null ? AngleLineManager.EnableToggle.targetGraphic as Image : null;
        if (Reference != null)
        {
            Face.sprite = Reference.sprite;
            Face.type = Reference.type;
            Face.color = Reference.color;
        }
        Button Btn = Holder.GetComponent<Button>();
        Btn.targetGraphic = Face;
        Btn.onClick.AddListener(OnClick);

        Font Face2 = AngleLineManager.AnglelineInputField != null && AngleLineManager.AnglelineInputField.textComponent != null
            ? AngleLineManager.AnglelineInputField.textComponent.font : null;
        if (!LimIcons.TryPlace(Rect, Icon, new Color(0.15f, 0.15f, 0.15f), 4f))
        {
            Text Caption = LimUiBuilder.CreateLabel(Rect, "Text", Face2, 16, new Color(0.15f, 0.15f, 0.15f), TextAnchor.MiddleCenter);
            Caption.rectTransform.anchorMin = Vector2.zero;
            Caption.rectTransform.anchorMax = Vector2.one;
            Caption.rectTransform.offsetMin = Vector2.zero;
            Caption.rectTransform.offsetMax = Vector2.zero;
            Caption.text = Fallback;
        }

        LimMouseOverHint Hint = Holder.AddComponent<LimMouseOverHint>();
        Hint.HintTextDictKey = HintKey;
        Hint.Font = Face2;
    }
}
