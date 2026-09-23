using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Grid tool and drops it into the Creator window between
/// Angleline and Copier.
///
/// It is made from a copy of the Angleline tool, which already has the
/// folding header and the scrolling body every tool here shares; the copy
/// then loses the angleline behaviour and its contents, and is given two
/// fields and an opacity slider instead. Nothing of this is in the scene
/// file, so the scene keeps the three tools it always had.
/// </summary>
public partial class LimCreatorManager
{
    private const float GridRowHeight = 30f;
    private const float GridRowStep = 35f;
    private const float GridContentHeight = 75f;

    private void CreateGridTool()
    {
        if (AngleLineManager == null || AngleLineManager.ToolBase == null) return;
        RectTransform Source = AngleLineManager.ToolBase.ToolRect;
        if (Source == null || Source.parent == null) return;

        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = "Grid";
        // The copy came with the angleline tool's own behaviour attached.
        LimAngleLineManager Inherited = Clone.GetComponent<LimAngleLineManager>();
        if (Inherited != null) DestroyImmediate(Inherited);

        LimGridManager Grid = Clone.AddComponent<LimGridManager>();
        Grid.ToolBase = Clone.GetComponent<LimCreatorToolBase>();
        Grid.InvalidColor = AngleLineManager.InvalidColor;
        Grid.ValidColor = AngleLineManager.ValidColor;

        Transform Folder = Clone.transform.Find("Folder/Label");
        if (Folder != null) Grid.LabelText = Folder.GetComponent<Text>();

        RectTransform Content = Clone.transform.Find("View/Viewport/Content") as RectTransform;
        if (Content == null) { Destroy(Clone); return; }
        // Whatever the angleline tool keeps in there is not wanted here.
        for (int i = Content.childCount - 1; i >= 0; --i) DestroyImmediate(Content.GetChild(i).gameObject);

        BuildGridContent(Grid, Content);

        Grid.ToolBase.Height = 30 + GridContentHeight;
        Grid.Setup(TunerManager, ClickToCreateManager != null ? ClickToCreateManager.TunerCamera : null);
        GridManager = Grid;
        if (LimLanguageManager.TextDict != null) Grid.SetTexts();
    }

    /// <summary>
    /// A tick and a field for each direction, V. on the left and H. on the
    /// right, and the opacity slider on the row below. The ticks are copies
    /// of the angleline tool's own checkbox, so they look and behave like the
    /// one next to it.
    /// </summary>
    private void BuildGridContent(LimGridManager Grid, RectTransform Content)
    {
        InputField Template = AngleLineManager.AnglelineInputField;
        Font Face = Template != null && Template.textComponent != null ? Template.textComponent.font : null;
        Color Ink = new Color(0.85f, 0.85f, 0.85f);

        Grid.VerticalToggle = CloneToggle(Content, "VerticalToggle", new Vector2(0, 1), 4, -5, Grid);
        Grid.VerticalLabel = LimUiBuilder.CreateLabel(Content, "VerticalLabel", Face, 16, Ink, TextAnchor.MiddleLeft);
        Place(Grid.VerticalLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(38, -5), new Vector2(22, GridRowHeight));

        Grid.HorizontalToggle = CloneToggle(Content, "HorizontalToggle", new Vector2(0.5f, 1), 4, -5, Grid);
        Grid.HorizontalLabel = LimUiBuilder.CreateLabel(Content, "HorizontalLabel", Face, 16, Ink, TextAnchor.MiddleLeft);
        Place(Grid.HorizontalLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(38, -5), new Vector2(22, GridRowHeight));

        Grid.VerticalField = CloneField(Template, Content, "VerticalField", new Vector2(0, 1), new Vector2(0.5f, 1), 62, -5);
        Grid.HorizontalField = CloneField(Template, Content, "HorizontalField", new Vector2(0.5f, 1), new Vector2(1, 1), 62, -5);
        if (Grid.VerticalField != null)
        {
            Grid.VerticalFieldImage = Grid.VerticalField.GetComponent<Image>();
            Grid.VerticalField.onValueChanged.AddListener((string Value) => { Grid.OnVerticalFieldChange(); });
        }
        if (Grid.HorizontalField != null)
        {
            Grid.HorizontalFieldImage = Grid.HorizontalField.GetComponent<Image>();
            Grid.HorizontalField.onValueChanged.AddListener((string Value) => { Grid.OnHorizontalFieldChange(); });
        }

        Grid.OpacityLabel = LimUiBuilder.CreateLabel(Content, "OpacityLabel", Face, 14, Ink, TextAnchor.MiddleLeft);
        Place(Grid.OpacityLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -40), new Vector2(90, GridRowHeight));

        Image Skin = Template != null ? Template.GetComponent<Image>() : null;
        Grid.OpacitySlider = LimUiBuilder.CreateSlider(Content, "Opacity", Skin, 0, 1, 1);
        RectTransform SliderRect = Grid.OpacitySlider.GetComponent<RectTransform>();
        SliderRect.anchorMin = new Vector2(0, 1);
        SliderRect.anchorMax = new Vector2(1, 1);
        SliderRect.pivot = new Vector2(0.5f, 1);
        SliderRect.offsetMin = new Vector2(98, -60);
        SliderRect.offsetMax = new Vector2(-5, -45);
        Grid.OpacitySlider.onValueChanged.AddListener((float Value) => { Grid.OnOpacityChange(); });
    }

    /// <summary>
    /// A copy of the angleline tool's checkbox, switched on and wired to the
    /// grid instead of to the tool it came from.
    /// </summary>
    private Toggle CloneToggle(RectTransform Parent, string Name, Vector2 Anchor, float X, float Y, LimGridManager Grid)
    {
        if (AngleLineManager.EnableToggle == null) return null;
        GameObject Clone = Instantiate(AngleLineManager.EnableToggle.gameObject, Parent);
        LimThemeManager.Adopt(AngleLineManager.EnableToggle.gameObject, Clone);
        Clone.name = Name;
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Place(Rect, Anchor, Anchor, new Vector2(0, 1), new Vector2(X, Y), new Vector2(GridRowHeight, GridRowHeight));

        Toggle Tick = Clone.GetComponent<Toggle>();
        if (Tick != null)
        {
            // A fresh event: the copy arrived still switching the angleline
            // tool on and off.
            Tick.onValueChanged = new Toggle.ToggleEvent();
            Tick.isOn = true;
            Tick.onValueChanged.AddListener((bool On) => { Grid.OnToggleChange(); });
        }
        StripHints(Clone);
        return Tick;
    }

    /// <summary>
    /// Takes off the hover notes a copy inherited: they would explain the
    /// angleline tool while sitting in this one.
    /// </summary>
    private static void StripHints(GameObject Clone)
    {
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
    }

    /// <summary>
    /// A copy of a field that is already in the panel, stretched across half
    /// the width. The copy arrives still wired to whatever the original
    /// reported to, so its event is replaced rather than added to.
    /// </summary>
    private InputField CloneField(InputField Template, RectTransform Parent, string Name,
                                  Vector2 AnchorMin, Vector2 AnchorMax, float Left, float Top)
    {
        if (Template == null) return null;
        GameObject Clone = Instantiate(Template.gameObject, Parent);
        LimThemeManager.Adopt(Template.gameObject, Clone);
        Clone.name = Name;
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchorMin = AnchorMin;
        Rect.anchorMax = AnchorMax;
        Rect.pivot = new Vector2(0.5f, 1);
        Rect.offsetMin = new Vector2(Left, Top - GridRowHeight);
        Rect.offsetMax = new Vector2(-5, Top);

        StripHints(Clone);
        InputField Field = Clone.GetComponent<InputField>();
        if (Field != null)
        {
            Field.onValueChanged = new InputField.OnChangeEvent();
            Field.onEndEdit = new InputField.SubmitEvent();
            Field.text = string.Empty;
        }
        return Field;
    }

    private static void Place(RectTransform Rect, Vector2 AnchorMin, Vector2 AnchorMax, Vector2 Pivot,
                              Vector2 Position, Vector2 Size)
    {
        Rect.anchorMin = AnchorMin;
        Rect.anchorMax = AnchorMax;
        Rect.pivot = Pivot;
        Rect.anchoredPosition = Position;
        Rect.sizeDelta = Size;
    }
}
