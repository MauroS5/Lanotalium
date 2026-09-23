using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The two controls added to the left of Create Catch Rail's Quantity box:
/// Reverse, and Spins.
///
/// **Reverse** decides which way round the ring the catch notes travel. A
/// catch rail used to be laid out by plain subtraction of the two degrees,
/// so a run from 350 to 10 was read as a journey of -340 and went the whole
/// way round the back rather than the twenty degrees across zero. It now
/// takes the short way by default, which is what is nearly always wanted,
/// and the long way only when this is switched on. It is a coloured button
/// like Enable and the Attach To pair rather than a tick, to match the row
/// it sits in, and it starts off.
///
/// **Spins** adds whole turns on top of that. Three spins between a note at
/// degree 0 and one at degree 5 lays the catch notes out over 1085 degrees,
/// so they wind three times round the core before arriving, evenly spaced
/// and all at the same distance since only the degree is being spread.
///
/// Both are read by CreateCatchRail in LimCreatorManager.
/// </summary>
public partial class LimCreatorManager
{
    /// <summary>Beside the Quantity box, which is 70 wide and flush right.</summary>
    private const float CatchRailFieldWidth = 70f;
    private const float CatchRailGap = 5f;
    private const float CatchRailButtonWidth = 72f;
    /// <summary>
    /// How much shorter than the row the button is drawn, so it sits inside
    /// the band like the two boxes beside it instead of filling the row edge
    /// to edge and reading as taller than the bar it is on.
    /// </summary>
    private const float CatchRailButtonInset = 8f;
    /// <summary>
    /// The row's own label is 250 wide against a Creator of 420, which left
    /// no room for two more controls: Quantity takes the last 70, Spins the
    /// 70 before it, and Reverse would have been laid straight over the
    /// label. The words fit comfortably in 140, so the label gives the space
    /// back rather than the controls being squeezed into nothing.
    /// </summary>
    private const float CatchRailLabelWidth = 140f;

    private Button ReverseButton;
    private Image ReverseImage;
    private Text ReverseText, SpinsHintText;
    private InputField SpinsInputField;

    /// <summary>Off by default: going the long way round is the rare case.</summary>
    private bool CatchRailReverse;

    private void CreateCatchRailOptions()
    {
        if (CreateCatchRailQuantityInputField == null) return;
        RectTransform Quantity = CreateCatchRailQuantityInputField.GetComponent<RectTransform>();
        RectTransform Row = Quantity != null ? Quantity.parent as RectTransform : null;
        if (Row == null) return;

        if (CreateCatchRailText != null)
        {
            RectTransform Label = CreateCatchRailText.rectTransform;
            Label.sizeDelta = new Vector2(CatchRailLabelWidth, Label.sizeDelta.y);
        }
        BuildSpinsField(Quantity, Row);
        BuildReverseButton(Quantity, Row);
        if (LimLanguageManager.TextDict != null) SetCatchRailOptionTexts();
    }

    /// <summary>
    /// A copy of the Quantity box, so the two read as a pair, moved one box
    /// and a gap to the left of it.
    /// </summary>
    private void BuildSpinsField(RectTransform Quantity, RectTransform Row)
    {
        GameObject Clone = Instantiate(Quantity.gameObject, Row);
        LimThemeManager.Adopt(Quantity.gameObject, Clone);
        Clone.name = "Spins";
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchorMin = Quantity.anchorMin;
        Rect.anchorMax = Quantity.anchorMax;
        Rect.pivot = Quantity.pivot;
        Rect.sizeDelta = Quantity.sizeDelta;
        Rect.anchoredPosition = Quantity.anchoredPosition + new Vector2(-(CatchRailFieldWidth + CatchRailGap), 0);
        // The copy came reporting what is typed into it back to Quantity.
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
        foreach (UnityEngine.EventSystems.EventTrigger Inherited in Clone.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)) DestroyImmediate(Inherited);

        SpinsInputField = Clone.GetComponent<InputField>();
        if (SpinsInputField != null)
        {
            SpinsInputField.onValueChanged = new InputField.OnChangeEvent();
            SpinsInputField.onEndEdit = new InputField.SubmitEvent();
            SpinsInputField.text = string.Empty;
            SpinsHintText = SpinsInputField.placeholder as Text;
        }
        // What spins do is not obvious from the word, so the box explains
        // itself when the pointer rests on it.
        LimMouseOverHint Explains = Clone.AddComponent<LimMouseOverHint>();
        Explains.HintTextDictKey = "Creator_CatchRail_Spins";
        if (SpinsInputField != null && SpinsInputField.textComponent != null) Explains.Font = SpinsInputField.textComponent.font;
    }

    /// <summary>
    /// Built rather than copied: nothing on this row is a switch, and the
    /// ones that are live in other tools, where copying one would have
    /// dragged that tool's wiring along with it.
    /// </summary>
    private void BuildReverseButton(RectTransform Quantity, RectTransform Row)
    {
        GameObject Made = new GameObject("Reverse", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform Rect = Made.GetComponent<RectTransform>();
        Rect.SetParent(Row, false);
        Rect.anchorMin = Quantity.anchorMin;
        Rect.anchorMax = Quantity.anchorMax;
        Rect.pivot = Quantity.pivot;
        Rect.sizeDelta = new Vector2(CatchRailButtonWidth, Quantity.sizeDelta.y - CatchRailButtonInset);
        Rect.anchoredPosition = Quantity.anchoredPosition + new Vector2(-(CatchRailFieldWidth * 2 + CatchRailGap * 2), 0);
        // Above everything else on the row, so the row's own button cannot
        // take the click on its way past.
        Rect.SetAsLastSibling();

        ReverseImage = Made.GetComponent<Image>();
        ReverseImage.raycastTarget = true;
        // The box beside it lends its sprite, so the switch is drawn the way
        // the rest of the row is rather than as a bare rectangle.
        Image Reference = CreateCatchRailQuantityInputField.GetComponent<Image>();
        if (Reference != null)
        {
            ReverseImage.sprite = Reference.sprite;
            ReverseImage.type = Reference.type;
        }

        ReverseButton = Made.GetComponent<Button>();
        // A Button made at runtime comes with ColorTint and no target, so
        // whether Unity repaints this graphic depends on a field nothing has
        // set. Pinned both ways round: the target is named, and the tinting
        // is switched off, so the only thing that ever colours the switch is
        // the switch saying whether it is on.
        ReverseButton.targetGraphic = ReverseImage;
        ReverseButton.transition = Selectable.Transition.None;
        ReverseButton.onClick.AddListener(OnReverseClick);

        GameObject Label = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform LabelRect = Label.GetComponent<RectTransform>();
        LabelRect.SetParent(Rect, false);
        LabelRect.anchorMin = Vector2.zero;
        LabelRect.anchorMax = Vector2.one;
        LabelRect.offsetMin = Vector2.zero;
        LabelRect.offsetMax = Vector2.zero;
        ReverseText = Label.GetComponent<Text>();
        ReverseText.alignment = TextAnchor.MiddleCenter;
        ReverseText.raycastTarget = false;
        if (CreateCatchRailText != null)
        {
            ReverseText.font = CreateCatchRailText.font;
            ReverseText.fontSize = CreateCatchRailText.fontSize;
            ReverseText.color = CreateCatchRailText.color;
        }
        RefreshReverseLook();
    }

    public void SetCatchRailOptionTexts()
    {
        if (ReverseText != null) ReverseText.text = LimLanguageManager.TextDict["Window_Creator_CreateCatchRail_Reverse"];
        if (SpinsHintText != null) SpinsHintText.text = LimLanguageManager.TextDict["Window_Creator_CreateCatchRail_Spins"];
    }

    public void OnReverseClick()
    {
        CatchRailReverse = !CatchRailReverse;
        RefreshReverseLook();
    }

    private void RefreshReverseLook()
    {
        if (ReverseImage == null) return;
        // The same two colours Enable and the Attach To pair use, so on and
        // off read the same everywhere in the Creator.
        Color On = ClickToCreateManager != null ? ClickToCreateManager.PressedColor : new Color(0.62f, 0.62f, 0.62f);
        Color Off = ClickToCreateManager != null ? ClickToCreateManager.UnPressedColor : Color.white;
        ReverseImage.color = CatchRailReverse ? On : Off;
    }

    /// <summary>
    /// How far round the ring the catch notes have to travel, in the chart's
    /// own degrees. The short way unless Reverse is on, plus whole turns for
    /// the spins asked for, which are added in whichever direction the run is
    /// already going so that they lengthen it rather than unwind it.
    /// </summary>
    private float CatchRailSweep(float FromDegree, float ToDegree)
    {
        float Sweep = Mathf.DeltaAngle(FromDegree, ToDegree);
        if (CatchRailReverse) Sweep += Sweep > 0 ? -360f : 360f;

        int Spins = CatchRailSpins();
        if (Spins <= 0) return Sweep;
        float Direction = Sweep > 0.0001f ? 1f : (Sweep < -0.0001f ? -1f : (CatchRailReverse ? -1f : 1f));
        return Sweep + 360f * Spins * Direction;
    }

    /// <summary>Blank means none, which is how the box starts.</summary>
    private int CatchRailSpins()
    {
        if (SpinsInputField == null) return 0;
        if (string.IsNullOrEmpty(SpinsInputField.text)) return 0;
        int Spins;
        if (!int.TryParse(SpinsInputField.text, out Spins)) return 0;
        return Spins < 0 ? 0 : Spins;
    }
}
