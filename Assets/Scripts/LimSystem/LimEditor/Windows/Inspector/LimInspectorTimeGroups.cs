using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Time Groups tab, beside BPM, Scroll Speed and Default.
///
/// Opening it closes those three, and opening any of them closes it, so the
/// panel is never buried under another list. Everything about the groups is
/// in the one panel, top to bottom:
///   - a toolbar: add a group, move the selected notes into the active one,
///     and the switch that shows or hides the groups' effects;
///   - a row per group, the base group first: its name, how many notes it
///     holds, Edit to make it the active group (where new notes go and whose
///     settings are shown below), Visible, and delete, which asks twice;
///   - the active group's scroll speeds, in the very rows the Scroll Speed
///     tab uses;
///   - for any group but the base: its opacity over time, its turn round the
///     core, its fade by distance, and its colour.
///
/// Built at runtime from copies of what the inspector already has, like the
/// rest of this branch: the tab is a copy of the Default tab, the panel a copy
/// of the Scroll Speed component, and every row a copy of a scroll speed row,
/// so nothing here looks bolted on and the scene is never edited.
/// </summary>
public partial class LimInspectorManager
{
    private const float TimeGroupsRowHeight = 30f;
    private const float TimeGroupsTabX = 390f;
    private const float TimeGroupsTabMaxWidth = 110f;
    private const float TimeGroupsTabMinWidth = 60f;
    private const float TimeGroupsCloseWidth = 30f;
    private const float TimeGroupsGap = 5f;
    /// <summary>A second click on delete within this many seconds removes the group.</summary>
    private const float TimeGroupsDeleteConfirmSeconds = 3f;
    private static readonly Color TimeGroupsInk = new Color(0.15f, 0.15f, 0.15f);
    private static readonly Color TimeGroupsDeleteArmed = new Color(0.95f, 0.45f, 0.45f);
    /// <summary>The swatches offered for a group's colour; the first takes the tint off.</summary>
    private static readonly string[] TimeGroupsSwatches = { "", "FF8080", "FFB070", "FFF080", "90F090", "80E0FF", "8090FF", "D090FF" };

    private RectTransform TimeGroupsTab;
    private Image TimeGroupsTabImg;
    private Text TimeGroupsTabText;
    private RectTransform TimeGroupsComponentRect, TimeGroupsViewRect, TimeGroupsContent, TimeGroupsPairs, TimeGroupsSubLabels;
    private Text TimeGroupsLabel;
    private Font TimeGroupsFont;
    private readonly List<GameObject> TimeGroupsRows = new List<GameObject>();
    private readonly Dictionary<int, Text> TimeGroupsCountLabels = new Dictionary<int, Text>();
    private readonly Dictionary<int, int> TimeGroupsCounts = new Dictionary<int, int>();
    private bool TimeGroupsBuilt, TimeGroupsFolded;
    private float TimeGroupsUnfoldHeight;
    private float TimeGroupsLastWidth = -1f;
    private int TimeGroupsDeleteArmedId = -1;
    private float TimeGroupsDeleteArmedAt;
    /// <summary>The chart the panel was last filled from.</summary>
    private Lanotalium.ChartContainer TimeGroupsChart;
    private Color TimeGroupsValid = Color.white, TimeGroupsInvalid = new Color(1f, 0.6f, 0.6f);

    private bool IsTimeGroupsOpen
    {
        get { return TimeGroupsComponentRect != null && TimeGroupsComponentRect.gameObject.activeSelf; }
    }

    private void Start()
    {
        BuildTimeGroupsUi();
        LimTimeGroups.Changed += OnTimeGroupsChanged;
    }
    private void OnDestroy()
    {
        LimTimeGroups.Changed -= OnTimeGroupsChanged;
    }

    private void BuildTimeGroupsUi()
    {
        if (TimeGroupsBuilt) return;
        if (DefaultSwitcherImg == null || ComponentScrollSpeed == null) return;
        TimeGroupsBuilt = true;

        // The tab: a copy of Default, to its right.
        RectTransform DefaultTab = DefaultSwitcherImg.rectTransform;
        GameObject Tab = Instantiate(DefaultTab.gameObject, DefaultTab.parent);
        LimThemeManager.Adopt(DefaultTab.gameObject, Tab);
        Tab.name = "TimeGroupsSwitcher";
        TimeGroupsTab = Tab.GetComponent<RectTransform>();
        TimeGroupsTab.anchoredPosition = new Vector2(TimeGroupsTabX, DefaultTab.anchoredPosition.y);
        TimeGroupsTabImg = Tab.GetComponent<Image>();
        // Through the theme, like every colour this panel sets: a colour set
        // directly is the default theme's whatever theme is on.
        LimThemeManager.Paint(TimeGroupsTabImg, UnpressedColor);
        Button TabButton = Tab.GetComponent<Button>();
        if (TabButton != null)
        {
            TabButton.onClick = new Button.ButtonClickedEvent();
            TabButton.onClick.AddListener(SwitchTimeGroups);
        }
        TimeGroupsTabText = Tab.GetComponentInChildren<Text>(true);
        if (TimeGroupsTabText != null)
        {
            // The space left between Default and the close button is narrow,
            // so the words shrink rather than spill out of it.
            TimeGroupsTabText.resizeTextForBestFit = true;
            TimeGroupsTabText.resizeTextMinSize = 8;
            TimeGroupsTabText.resizeTextMaxSize = TimeGroupsTabText.fontSize > 0 ? TimeGroupsTabText.fontSize : 14;
        }
        StripInherited(Tab);

        // The panel: a copy of the Scroll Speed component.
        GameObject Panel = Instantiate(ComponentScrollSpeed.gameObject, ComponentScrollSpeed.transform.parent);
        LimThemeManager.Adopt(ComponentScrollSpeed.gameObject, Panel);
        Panel.name = "ComponentTimeGroups";
        // Its script would keep answering for the chart's own list.
        ComponentScrollSpeedManager Inherited = Panel.GetComponent<ComponentScrollSpeedManager>();
        if (Inherited != null) DestroyImmediate(Inherited);
        StripInherited(Panel);
        TimeGroupsComponentRect = Panel.GetComponent<RectTransform>();

        Transform Folder = Panel.transform.Find("Folder");
        if (Folder != null)
        {
            Button FoldButton = Folder.GetComponent<Button>();
            if (FoldButton != null)
            {
                FoldButton.onClick = new Button.ButtonClickedEvent();
                FoldButton.onClick.AddListener(FoldTimeGroups);
            }
            TimeGroupsLabel = Folder.GetComponentInChildren<Text>(true);
            if (TimeGroupsLabel != null) TimeGroupsFont = TimeGroupsLabel.font;
        }
        Transform View = Panel.transform.Find("Component");
        TimeGroupsViewRect = View as RectTransform;
        Transform Content = View != null ? View.Find("Viewport/Content") : null;
        TimeGroupsContent = Content as RectTransform;
        if (Content != null)
        {
            Transform ChartSpeed = Content.Find("ChartSpeed");
            if (ChartSpeed != null) DestroyImmediate(ChartSpeed.gameObject);
            TimeGroupsSubLabels = Content.Find("SubLabels") as RectTransform;
            TimeGroupsPairs = Content.Find("Pairs") as RectTransform;
            if (TimeGroupsPairs != null)
            {
                // Rows the copy carried over belong to the chart's own list.
                for (int i = TimeGroupsPairs.childCount - 1; i >= 0; --i) DestroyImmediate(TimeGroupsPairs.GetChild(i).gameObject);
            }
        }
        // The colours a scroll speed row marks a good and a bad number with.
        GameObject Sample = Instantiate(ComponentScrollSpeed.TimeValuePrefab);
        TimeValuePairManager SamplePair = Sample.GetComponent<TimeValuePairManager>();
        if (SamplePair != null)
        {
            TimeGroupsValid = SamplePair.ValidColor;
            TimeGroupsInvalid = SamplePair.InvalidColor;
        }
        DestroyImmediate(Sample);

        Panel.SetActive(false);
        if (LimLanguageManager.TextDict != null) SetTimeGroupsTexts();
    }

    /// <summary>Copies arrive still answering to the control they were copied from.</summary>
    private static void StripInherited(GameObject Clone)
    {
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
        foreach (UnityEngine.EventSystems.EventTrigger Trigger in Clone.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)) DestroyImmediate(Trigger);
    }

    public void SetTimeGroupsTexts()
    {
        if (TimeGroupsTabText != null) TimeGroupsTabText.text = LimLanguageManager.TextDict["Window_Inspector_Switcher_TimeGroups"];
        if (TimeGroupsLabel != null) TimeGroupsLabel.text = LimLanguageManager.TextDict["TimeGroups_Label"];
        if (IsTimeGroupsOpen) RebuildTimeGroups();
    }

    /// <summary>Called every frame from Update.</summary>
    private void UpdateTimeGroupsUi()
    {
        if (TimeGroupsTab == null) return;
        // As wide as the room between Default and the close button allows.
        RectTransform Window = TimeGroupsTab.parent as RectTransform;
        if (Window != null)
        {
            float Room = Window.rect.width - TimeGroupsCloseWidth - TimeGroupsGap - TimeGroupsTabX;
            float Width = Mathf.Clamp(Room, TimeGroupsTabMinWidth, TimeGroupsTabMaxWidth);
            if (TimeGroupsTab.sizeDelta.x != Width) TimeGroupsTab.sizeDelta = new Vector2(Width, TimeGroupsTab.sizeDelta.y);
        }
        if (TimeGroupsDeleteArmedId != -1 && Time.unscaledTime - TimeGroupsDeleteArmedAt > TimeGroupsDeleteConfirmSeconds)
        {
            TimeGroupsDeleteArmedId = -1;
            if (IsTimeGroupsOpen) RebuildTimeGroups();
        }
        if (!IsTimeGroupsOpen || TunerManager == null || !TunerManager.isInitialized) return;
        // A chart opened while the panel is showing: the groups are new ones.
        if (TimeGroupsChart != LimSystem.ChartContainer) { RebuildTimeGroups(); return; }
        if (TimeGroupsViewRect != null && TimeGroupsViewRect.rect.width != TimeGroupsLastWidth) { RebuildTimeGroups(); return; }
        RefreshTimeGroupCounts();
    }

    /// <summary>
    /// How many notes each group holds, kept current while the panel is open,
    /// so moving notes into a group shows at once that they went.
    /// </summary>
    private void RefreshTimeGroupCounts()
    {
        if (TimeGroupsCountLabels.Count == 0) return;
        TimeGroupsCounts.Clear();
        foreach (LanotaTapNote Tap in TunerManager.TapNoteManager.TapNote)
        {
            int Seen; TimeGroupsCounts.TryGetValue(Tap.Group, out Seen); TimeGroupsCounts[Tap.Group] = Seen + 1;
        }
        foreach (LanotaHoldNote Hold in TunerManager.HoldNoteManager.HoldNote)
        {
            int Seen; TimeGroupsCounts.TryGetValue(Hold.Group, out Seen); TimeGroupsCounts[Hold.Group] = Seen + 1;
        }
        foreach (KeyValuePair<int, Text> Pair in TimeGroupsCountLabels)
        {
            if (Pair.Value == null) continue;
            int Count; TimeGroupsCounts.TryGetValue(Pair.Key, out Count);
            string Shown = Count.ToString();
            if (Pair.Value.text != Shown) Pair.Value.text = Shown;
        }
    }

    public void SwitchTimeGroups()
    {
        if (LimSystem.ChartContainer == null) return;
        BuildTimeGroupsUi();
        if (TimeGroupsComponentRect == null) return;
        if (IsTimeGroupsOpen)
        {
            CloseTimeGroups();
            ArrangeComponentsUi();
            return;
        }
        // Opening it puts away the three lists beside it.
        if (ComponentBpm.ComponentBpmView.activeInHierarchy) SwitchBpmList();
        if (ComponentScrollSpeed.gameObject.activeInHierarchy) SwitchScrollSpeedList();
        if (ComponentDefault.gameObject.activeInHierarchy) SwitchDefault();
        LimThemeManager.Paint(TimeGroupsTabImg, PressedColor);
        TimeGroupsComponentRect.gameObject.SetActive(true);
        TimeGroupsFolded = false;
        RebuildTimeGroups();
    }

    /// <summary>Put away whenever BPM, Scroll Speed or Default is opened.</summary>
    private void CloseTimeGroups()
    {
        if (!IsTimeGroupsOpen) return;
        TimeGroupsComponentRect.gameObject.SetActive(false);
        if (TimeGroupsTabImg != null) LimThemeManager.Paint(TimeGroupsTabImg, UnpressedColor);
        ClearTimeGroupsRows();
    }

    private void FoldTimeGroups()
    {
        if (TimeGroupsViewRect == null) return;
        TimeGroupsFolded = !TimeGroupsFolded;
        TimeGroupsViewRect.sizeDelta = new Vector2(0, TimeGroupsFolded ? 0 : TimeGroupsUnfoldHeight);
        TimeGroupsComponentRect.sizeDelta = new Vector2(0, TimeGroupsViewRect.sizeDelta.y - TimeGroupsViewRect.anchoredPosition.y);
        ArrangeComponentsUi();
    }

    private void OnTimeGroupsChanged()
    {
        if (IsTimeGroupsOpen) RebuildTimeGroups();
    }

    /// <summary>Where the panel goes in the stack of components.</summary>
    private float ArrangeTimeGroupsUi(float Height)
    {
        if (!IsTimeGroupsOpen) return Height;
        TimeGroupsComponentRect.anchoredPosition = new Vector2(0, Height);
        return Height - TimeGroupsComponentRect.sizeDelta.y;
    }

    // ------------------------------------------------------------ building

    private void ClearTimeGroupsRows()
    {
        foreach (GameObject Row in TimeGroupsRows) if (Row != null) Destroy(Row);
        TimeGroupsRows.Clear();
        TimeGroupsCountLabels.Clear();
    }

    private void RebuildTimeGroups()
    {
        if (TimeGroupsContent == null || TunerManager == null || !TunerManager.isInitialized) return;
        ClearTimeGroupsRows();
        TimeGroupsChart = LimSystem.ChartContainer;
        TimeGroupsLastWidth = TimeGroupsViewRect.rect.width;
        // Shrinks with a narrow window but never grows past the 500 it was
        // drawn for: a window stretched past the screen's edge made every
        // button three times wider, off to the right out of sight.
        float Ratio = Mathf.Clamp(TimeGroupsLastWidth / 500f, 0.2f, 1f);
        float Y = 0;

        // Toolbar.
        GameObject Toolbar = NewTimeGroupsRow("Toolbar", Y);
        AddTimeGroupsButton(Toolbar.transform, "TimeGroups_Add", 5, 150, Ratio, false, () => { OperationManager.CreateTimeGroup(); });
        Button Move = AddTimeGroupsButton(Toolbar.transform, "TimeGroups_MoveSelected", 160, 185, Ratio, false, MoveSelectionToActiveGroup);
        AddTimeGroupsHint(Move, "TimeGroups_Hint_Move");
        Button Effects = AddTimeGroupsButton(Toolbar.transform, LimTimeGroups.Preview ? "TimeGroups_Effects_On" : "TimeGroups_Effects_Off", 350, 145, Ratio, LimTimeGroups.Preview,
            () => { LimTimeGroups.SetPreview(!LimTimeGroups.Preview); });
        AddTimeGroupsHint(Effects, "TimeGroups_Hint_Effects");
        Y -= TimeGroupsRowHeight;

        // One row for the base group, then one per group.
        AddTimeGroupRow(LimTimeGroups.BaseGroup, LimLanguageManager.TextDict["TimeGroups_Base"], Y, Ratio);
        Y -= TimeGroupsRowHeight;
        foreach (LanotaTimeGroup Group in LimTimeGroups.Groups)
        {
            AddTimeGroupRow(Group.Id, Group.Name, Y, Ratio);
            Y -= TimeGroupsRowHeight;
        }
        RefreshTimeGroupCounts();

        // The active group's scroll speeds.
        LanotaTimeGroup Active = LimTimeGroups.Find(LimTimeGroups.ActiveGroup);
        string ActiveName = Active != null ? Active.Name : LimLanguageManager.TextDict["TimeGroups_Base"];
        Y -= TimeGroupsGap;
        GameObject SpeedHeader = NewTimeGroupsRow("SpeedHeader", Y);
        AddTimeGroupsText(SpeedHeader.transform, LimLanguageManager.TextDict["TimeGroups_ScrollSpeedOf"] + " " + ActiveName, 5, 340, Ratio);
        AddTimeGroupsButton(SpeedHeader.transform, "TimeGroups_AddSpeed", 350, 145, Ratio, false, AddSpeedToActiveGroup);
        Y -= TimeGroupsRowHeight;
        if (TimeGroupsSubLabels != null)
        {
            TimeGroupsSubLabels.anchoredPosition = new Vector2(TimeGroupsSubLabels.anchoredPosition.x, Y);
            Y -= TimeGroupsRowHeight;
        }
        if (TimeGroupsPairs != null)
        {
            TimeGroupsPairs.anchoredPosition = new Vector2(TimeGroupsPairs.anchoredPosition.x, Y);
            float PairY = 0;
            foreach (LanotaScroll Speed in ActiveGroupScroll())
            {
                if (Speed.ListGameObject != null) Destroy(Speed.ListGameObject);
                Speed.ListGameObject = Instantiate(ComponentScrollSpeed.TimeValuePrefab, TimeGroupsPairs);
                Speed.ListGameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, PairY);
                TimeValuePairManager Pair = Speed.ListGameObject.GetComponent<TimeValuePairManager>();
                Pair.OperationManager = OperationManager;
                Pair.Initialize(Speed);
                Speed.InstanceId = Speed.ListGameObject.GetInstanceID();
                TimeGroupsRows.Add(Speed.ListGameObject);
                PairY -= TimeGroupsRowHeight;
            }
            Y += PairY;
        }

        if (Active == null)
        {
            Y -= TimeGroupsGap;
            GameObject Note = NewTimeGroupsRow("BaseNote", Y);
            AddTimeGroupsText(Note.transform, LimLanguageManager.TextDict["TimeGroups_BaseHasNoEffects"], 5, 490, Ratio);
            Y -= TimeGroupsRowHeight;
        }
        else
        {
            Y = BuildKeySection(Active, true, Y, Ratio);
            Y = BuildKeySection(Active, false, Y, Ratio);
            Y = BuildFadeSection(Active, Y, Ratio);
            Y = BuildColorSection(Active, Y, Ratio);
        }

        TimeGroupsUnfoldHeight = -Y + TimeGroupsGap;
        if (!TimeGroupsFolded) TimeGroupsViewRect.sizeDelta = new Vector2(0, TimeGroupsUnfoldHeight);
        TimeGroupsComponentRect.sizeDelta = new Vector2(0, TimeGroupsViewRect.sizeDelta.y - TimeGroupsViewRect.anchoredPosition.y);
        ArrangeComponentsUi();
    }

    private List<LanotaScroll> ActiveGroupScroll()
    {
        LanotaTimeGroup Active = LimTimeGroups.Find(LimTimeGroups.ActiveGroup);
        if (Active != null) return Active.Scroll;
        return TunerManager.ScrollManager.Scroll;
    }

    private void AddTimeGroupRow(int Id, string Name, float Y, float Ratio)
    {
        bool IsBase = Id == LimTimeGroups.BaseGroup;
        bool IsActive = LimTimeGroups.ActiveGroup == Id;
        bool Visible = LimTimeGroups.IsVisible(Id);

        RowParts Parts = NewFieldRow("TimeGroup" + Id, Y, 1);
        InputField NameField = Parts.Fields[0];
        NameField.contentType = InputField.ContentType.Standard;
        NameField.text = Name;
        NameField.interactable = !IsBase;
        Place(NameField.GetComponent<RectTransform>(), 5, 150, Ratio);
        int Which = Id;
        NameField.onEndEdit.AddListener((string Typed) => { OperationManager.RenameTimeGroup(Which, Typed); });

        Text Count = AddTimeGroupsText(Parts.Row.transform, "0", 155, 40, Ratio);
        Count.alignment = TextAnchor.MiddleCenter;
        TimeGroupsCountLabels[Id] = Count;

        Button Edit = AddTimeGroupsButton(Parts.Row.transform, IsActive ? "TimeGroups_Active" : "TimeGroups_Select", 200, 95, Ratio, IsActive,
            () => { LimTimeGroups.SetActive(Which); });
        AddTimeGroupsHint(Edit, "TimeGroups_Hint_Edit");
        AddTimeGroupsButton(Parts.Row.transform, Visible ? "TimeGroups_Visible" : "TimeGroups_Hidden", 300, 95, Ratio, Visible,
            () => { LimTimeGroups.SetVisible(Which, !LimTimeGroups.IsVisible(Which)); });

        Button Delete = Parts.Delete;
        Place(Delete.GetComponent<RectTransform>(), 400, 95, Ratio);
        Delete.interactable = !IsBase;
        if (!IsBase)
        {
            if (TimeGroupsDeleteArmedId == Id)
            {
                Image Face = Delete.GetComponent<Image>();
                if (Face != null) LimThemeManager.Paint(Face, TimeGroupsDeleteArmed);
            }
            Delete.onClick.AddListener(() => { OnDeleteTimeGroupClick(Which); });
        }
    }

    /// <summary>
    /// Opacity or rotation: a heading with its add button, the column names,
    /// then a row per key with Timing, Duration, the value and the Ease.
    /// </summary>
    private float BuildKeySection(LanotaTimeGroup Group, bool Opacity, float Y, float Ratio)
    {
        Y -= TimeGroupsGap;
        GameObject Header = NewTimeGroupsRow(Opacity ? "OpacityHeader" : "RotationHeader", Y);
        AddTimeGroupsText(Header.transform, LimLanguageManager.TextDict[Opacity ? "TimeGroups_Opacity" : "TimeGroups_Rotation"], 5, 340, Ratio);
        int Id = Group.Id;
        Button Add = AddTimeGroupsButton(Header.transform, Opacity ? "TimeGroups_AddOpacity" : "TimeGroups_AddRotation", 350, 145, Ratio, false, () =>
        {
            if (OperationManager.AddGroupKey(Id, Opacity) == null) LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["TimeGroups_Msg_KeyExists"]);
        });
        AddTimeGroupsHint(Add, Opacity ? "TimeGroups_Hint_Opacity" : "TimeGroups_Hint_Rotation");
        Y -= TimeGroupsRowHeight;

        List<LanotaGroupKey> Keys = Opacity ? Group.Opacity : Group.Rotation;
        if (Keys.Count != 0)
        {
            GameObject Columns = NewTimeGroupsRow("Columns", Y);
            AddTimeGroupsText(Columns.transform, LimLanguageManager.TextDict["TimeGroups_Col_Timing"], 10, 115, Ratio);
            AddTimeGroupsText(Columns.transform, LimLanguageManager.TextDict["TimeGroups_Col_Duration"], 135, 90, Ratio);
            AddTimeGroupsText(Columns.transform, LimLanguageManager.TextDict[Opacity ? "TimeGroups_Col_Opacity" : "TimeGroups_Col_Degrees"], 235, 90, Ratio);
            AddTimeGroupsText(Columns.transform, LimLanguageManager.TextDict["TimeGroups_Col_Ease"], 335, 60, Ratio);
            Y -= TimeGroupsRowHeight;
        }
        foreach (LanotaGroupKey Key in Keys)
        {
            BuildKeyRow(Group, Key, Opacity, Y, Ratio);
            Y -= TimeGroupsRowHeight;
        }
        return Y;
    }

    private void BuildKeyRow(LanotaTimeGroup Group, LanotaGroupKey Key, bool Opacity, float Y, float Ratio)
    {
        RowParts Parts = NewFieldRow("Key", Y, 4);
        Key.ListGameObject = Parts.Row;
        InputField TimeField = Parts.Fields[0], DurationField = Parts.Fields[1], ValueField = Parts.Fields[2], EaseField = Parts.Fields[3];
        Place(TimeField.GetComponent<RectTransform>(), 5, 120, Ratio);
        Place(DurationField.GetComponent<RectTransform>(), 130, 95, Ratio);
        Place(ValueField.GetComponent<RectTransform>(), 230, 95, Ratio);
        Place(EaseField.GetComponent<RectTransform>(), 330, 65, Ratio);
        Place(Parts.Delete.GetComponent<RectTransform>(), 400, 95, Ratio);
        TimeField.text = Key.Time.ToString("f3");
        DurationField.text = Key.Duration.ToString("f3");
        ValueField.text = Key.Value.ToString(Opacity ? "f1" : "f2");
        EaseField.text = Key.Ease.ToString();

        TimeField.onEndEdit.AddListener((string Typed) =>
        {
            float Parsed;
            bool Ok = LimNumber.TryParseFloat(Typed, out Parsed) && Parsed >= 0;
            if (Ok) { Key.Time = Parsed; OperationManager.SortGroupKeys(Group); }
            MarkField(TimeField, Ok);
        });
        DurationField.onEndEdit.AddListener((string Typed) =>
        {
            float Parsed;
            bool Ok = LimNumber.TryParseFloat(Typed, out Parsed) && Parsed >= 0;
            if (Ok) Key.Duration = Parsed;
            MarkField(DurationField, Ok);
        });
        ValueField.onEndEdit.AddListener((string Typed) =>
        {
            float Parsed;
            bool Ok = LimNumber.TryParseFloat(Typed, out Parsed) && (!Opacity || (Parsed >= 0 && Parsed <= 100));
            if (Ok) Key.Value = Parsed;
            MarkField(ValueField, Ok);
        });
        EaseField.onEndEdit.AddListener((string Typed) =>
        {
            int Parsed;
            bool Ok = int.TryParse(Typed, out Parsed) && Parsed >= 0 && Parsed <= 12;
            if (Ok) Key.Ease = Parsed;
            MarkField(EaseField, Ok);
        });
        int Id = Group.Id;
        Parts.Delete.onClick.AddListener(() => { OperationManager.DeleteGroupKey(Id, Key); });
    }

    /// <summary>Fade by distance: where the notes finish appearing, and where they start to vanish.</summary>
    private float BuildFadeSection(LanotaTimeGroup Group, float Y, float Ratio)
    {
        Y -= TimeGroupsGap;
        GameObject Header = NewTimeGroupsRow("FadeHeader", Y);
        AddTimeGroupsText(Header.transform, LimLanguageManager.TextDict["TimeGroups_Fade"], 5, 490, Ratio);
        Y -= TimeGroupsRowHeight;

        RowParts Parts = NewFieldRow("Fade", Y, 2);
        DestroyImmediate(Parts.Delete.gameObject);
        AddTimeGroupsText(Parts.Row.transform, LimLanguageManager.TextDict["TimeGroups_FadeIn"], 5, 165, Ratio);
        Place(Parts.Fields[0].GetComponent<RectTransform>(), 170, 70, Ratio);
        AddTimeGroupsText(Parts.Row.transform, LimLanguageManager.TextDict["TimeGroups_FadeOut"], 250, 170, Ratio);
        Place(Parts.Fields[1].GetComponent<RectTransform>(), 425, 70, Ratio);
        InputField In = Parts.Fields[0], Out = Parts.Fields[1];
        In.text = Group.FadeIn.ToString("f0");
        Out.text = Group.FadeOut.ToString("f0");
        AddTimeGroupsHint(In, "TimeGroups_Hint_FadeIn");
        AddTimeGroupsHint(Out, "TimeGroups_Hint_FadeOut");
        int Id = Group.Id;
        In.onEndEdit.AddListener((string Typed) =>
        {
            float Parsed;
            bool Ok = LimNumber.TryParseFloat(Typed, out Parsed) && Parsed >= 0 && Parsed <= 100;
            if (Ok) OperationManager.SetGroupFade(Id, Parsed, Group.FadeOut);
            MarkField(In, Ok);
        });
        Out.onEndEdit.AddListener((string Typed) =>
        {
            float Parsed;
            bool Ok = LimNumber.TryParseFloat(Typed, out Parsed) && Parsed >= 0 && Parsed <= 100;
            if (Ok) OperationManager.SetGroupFade(Id, Group.FadeIn, Parsed);
            MarkField(Out, Ok);
        });
        return Y - TimeGroupsRowHeight;
    }

    /// <summary>Colour: a row of swatches, the first of them none, and a field for any other.</summary>
    private float BuildColorSection(LanotaTimeGroup Group, float Y, float Ratio)
    {
        Y -= TimeGroupsGap;
        GameObject Header = NewTimeGroupsRow("ColorHeader", Y);
        AddTimeGroupsText(Header.transform, LimLanguageManager.TextDict["TimeGroups_Color"], 5, 490, Ratio);
        Y -= TimeGroupsRowHeight;

        GameObject Swatches = NewTimeGroupsRow("Swatches", Y);
        int Id = Group.Id;
        float SwatchWidth = 490f / TimeGroupsSwatches.Length;
        for (int i = 0; i < TimeGroupsSwatches.Length; ++i)
        {
            string Hex = TimeGroupsSwatches[i];
            bool Chosen = string.Equals(Hex, Group.Color, System.StringComparison.OrdinalIgnoreCase);
            Button Swatch = AddTimeGroupsButton(Swatches.transform, i == 0 ? "TimeGroups_NoColor" : null, 5 + i * SwatchWidth, SwatchWidth - 4, Ratio, false, () =>
            {
                OperationManager.SetGroupColor(Id, Hex);
                RebuildTimeGroups();
            });
            Color Tint;
            Image Face = Swatch.GetComponent<Image>();
            if (Face != null) LimThemeManager.Paint(Face, LimTimeGroups.TryParseTint(Hex, out Tint) ? Tint : Color.white);
            if (Chosen)
            {
                // The chosen one is outlined, so it reads as chosen whatever its colour.
                Outline Ring = Swatch.gameObject.AddComponent<Outline>();
                Ring.effectColor = new Color(0.1f, 0.1f, 0.1f);
                Ring.effectDistance = new Vector2(2, -2);
            }
        }
        Y -= TimeGroupsRowHeight;

        RowParts Parts = NewFieldRow("Hex", Y, 1);
        DestroyImmediate(Parts.Delete.gameObject);
        AddTimeGroupsText(Parts.Row.transform, LimLanguageManager.TextDict["TimeGroups_Hex"], 5, 120, Ratio);
        InputField HexField = Parts.Fields[0];
        HexField.contentType = InputField.ContentType.Alphanumeric;
        HexField.characterLimit = 7;
        Place(HexField.GetComponent<RectTransform>(), 130, 120, Ratio);
        HexField.text = Group.Color;
        AddTimeGroupsHint(HexField, "TimeGroups_Hint_Color");
        HexField.onEndEdit.AddListener((string Typed) =>
        {
            bool Ok = OperationManager.SetGroupColor(Id, Typed);
            MarkField(HexField, Ok);
            if (Ok) RebuildTimeGroups();
        });
        // What the colour reaches: the glow of highlighted notes, the notes.
        Toggle Light = AddTimeGroupsToggle(Parts.Row.transform, "TimeGroups_ColorHighlight", 262, 110, Ratio, Group.ColorHighlight,
            (bool On) => { OperationManager.SetGroupColorParts(Id, Group.ColorNotes, On); });
        AddTimeGroupsHint(Light, "TimeGroups_Hint_ColorHighlight");
        Toggle Notes = AddTimeGroupsToggle(Parts.Row.transform, "TimeGroups_ColorNotes", 382, 110, Ratio, Group.ColorNotes,
            (bool On) => { OperationManager.SetGroupColorParts(Id, On, Group.ColorHighlight); });
        AddTimeGroupsHint(Notes, "TimeGroups_Hint_ColorNotes");
        return Y - TimeGroupsRowHeight;
    }

    /// <summary>
    /// A tick box and its words, the box a copy of the Basic component's
    /// Combination toggle so it looks like the inspector's own.
    /// </summary>
    private Toggle AddTimeGroupsToggle(Transform Row, string TextKey, float X, float Width, float Ratio, bool On, UnityEngine.Events.UnityAction<bool> OnChange)
    {
        Toggle Source = ComponentBasic != null ? ComponentBasic.Combination : null;
        if (Source == null) return null;
        GameObject Made = Instantiate(Source.gameObject, Row);
        LimThemeManager.Adopt(Source.gameObject, Made);
        Made.name = TextKey;
        Made.SetActive(true);
        StripInherited(Made);
        // Words the copy may carry are its original's; ours go beside it.
        foreach (Text Words in Made.GetComponentsInChildren<Text>(true)) DestroyImmediate(Words.gameObject);
        RectTransform Box = Made.GetComponent<RectTransform>();
        Box.anchorMin = Box.anchorMax = new Vector2(0, 1);
        Box.pivot = new Vector2(0, 0.5f);
        float Side = Mathf.Min(Box.rect.height > 0 ? Box.rect.height : 24, TimeGroupsRowHeight - 4);
        Box.sizeDelta = new Vector2(Side, Side);
        Box.anchoredPosition = new Vector2(X * Ratio, -TimeGroupsRowHeight / 2);
        Toggle Tick = Made.GetComponent<Toggle>();
        Tick.onValueChanged = new Toggle.ToggleEvent();
        Tick.isOn = On;
        Tick.interactable = true;
        Tick.onValueChanged.AddListener(OnChange);
        AddTimeGroupsText(Row, LimLanguageManager.TextDict[TextKey], X + Side / Ratio + 4, Width - Side / Ratio - 4, Ratio);
        return Tick;
    }

    // --------------------------------------------------------------- actions

    /// <summary>
    /// The first click arms the button, the second within a few seconds
    /// deletes, as the favourite groups and the angleline patterns ask.
    /// </summary>
    private void OnDeleteTimeGroupClick(int Id)
    {
        if (TimeGroupsDeleteArmedId == Id && Time.unscaledTime - TimeGroupsDeleteArmedAt <= TimeGroupsDeleteConfirmSeconds)
        {
            TimeGroupsDeleteArmedId = -1;
            OperationManager.DeleteTimeGroup(Id);
            return;
        }
        TimeGroupsDeleteArmedId = Id;
        TimeGroupsDeleteArmedAt = Time.unscaledTime;
        LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["TimeGroups_Msg_ConfirmDelete"]);
        RebuildTimeGroups();
    }

    private void MoveSelectionToActiveGroup()
    {
        if (OperationManager.SelectedTapNote.Count + OperationManager.SelectedHoldNote.Count == 0)
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["TimeGroups_Msg_NothingSelected"]);
            return;
        }
        int Moved = OperationManager.MoveSelectedNotesToGroup(LimTimeGroups.ActiveGroup);
        LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict[Moved != 0 ? "TimeGroups_Msg_Moved" : "TimeGroups_Msg_AlreadyThere"]);
    }

    private void AddSpeedToActiveGroup()
    {
        if (LimTimeGroups.ActiveGroup == LimTimeGroups.BaseGroup)
        {
            // The chart's own list: exactly what Create Scroll Speed does.
            if (TunerManager.ScrollManager.DisableChartSpeed) return;
            OperationManager.AddScrollSpeed(new LanotaScroll { Time = TunerManager.ChartTime < 0 ? 0 : TunerManager.ChartTime, Speed = 1 });
            RebuildTimeGroups();
            return;
        }
        if (!OperationManager.AddGroupScrollSpeed(LimTimeGroups.ActiveGroup, TunerManager.ChartTime))
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["TimeGroups_Msg_SpeedExists"]);
    }

    // --------------------------------------------------------------- helpers

    private struct RowParts
    {
        public GameObject Row;
        public List<InputField> Fields;
        public Button Delete;
    }

    /// <summary>
    /// A scroll speed row stripped of its script, with as many input fields
    /// as asked for (the row has two; more are copies of the second) and its
    /// delete button, every event emptied for the caller to fill.
    /// </summary>
    private RowParts NewFieldRow(string Name, float Y, int FieldCount)
    {
        GameObject Row = Instantiate(ComponentScrollSpeed.TimeValuePrefab, TimeGroupsContent);
        Row.name = Name;
        TimeValuePairManager Pair = Row.GetComponent<TimeValuePairManager>();
        InputField First = Pair.Time, Second = Pair.Value;
        Button Delete = Pair.Delete;
        DestroyImmediate(Pair);
        Row.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, Y);
        TimeGroupsRows.Add(Row);

        List<InputField> Fields = new List<InputField> { First, Second };
        while (Fields.Count < FieldCount) Fields.Add(Instantiate(Second.gameObject, Row.transform).GetComponent<InputField>());
        while (Fields.Count > FieldCount)
        {
            DestroyImmediate(Fields[Fields.Count - 1].gameObject);
            Fields.RemoveAt(Fields.Count - 1);
        }
        foreach (InputField Field in Fields)
        {
            Field.onValueChanged = new InputField.OnChangeEvent();
            Field.onEndEdit = new InputField.SubmitEvent();
            Field.interactable = true;
            Image Face = Field.GetComponent<Image>();
            if (Face != null) LimThemeManager.Paint(Face, TimeGroupsValid);
        }
        Delete.onClick = new Button.ButtonClickedEvent();
        Delete.interactable = true;
        return new RowParts { Row = Row, Fields = Fields, Delete = Delete };
    }

    private void MarkField(InputField Field, bool Ok)
    {
        Image Face = Field.GetComponent<Image>();
        if (Face != null) LimThemeManager.Paint(Face, Ok ? TimeGroupsValid : TimeGroupsInvalid);
    }

    private GameObject NewTimeGroupsRow(string Name, float Y)
    {
        GameObject Row = new GameObject(Name, typeof(RectTransform));
        RectTransform Rect = Row.GetComponent<RectTransform>();
        Rect.SetParent(TimeGroupsContent, false);
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(1, 1);
        Rect.pivot = new Vector2(0.5f, 1);
        Rect.sizeDelta = new Vector2(0, TimeGroupsRowHeight);
        Rect.anchoredPosition = new Vector2(0, Y);
        TimeGroupsRows.Add(Row);
        return Row;
    }

    /// <summary>
    /// Laid out in the 500-wide units the rest of the inspector uses, scaled
    /// to the width the window actually has.
    /// </summary>
    private static void Place(RectTransform Rect, float X, float Width, float Ratio)
    {
        if (Rect == null) return;
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(0, 1);
        Rect.pivot = new Vector2(0, 1);
        Rect.anchoredPosition = new Vector2(X * Ratio, 0);
        Rect.sizeDelta = new Vector2(Width * Ratio, TimeGroupsRowHeight);
    }

    /// <summary>
    /// A labelled button, made from a scroll speed row's delete button with
    /// its icon swapped for words (or for nothing, when TextKey is null).
    /// Pressed buttons wear the tab's pressed colour, so on and off read the
    /// same way here as in the tabs above.
    /// </summary>
    private Button AddTimeGroupsButton(Transform Row, string TextKey, float X, float Width, float Ratio, bool Pressed, UnityEngine.Events.UnityAction OnClick)
    {
        GameObject Holder = Instantiate(ComponentScrollSpeed.TimeValuePrefab);
        TimeValuePairManager Pair = Holder.GetComponent<TimeValuePairManager>();
        Button Source = Pair != null ? Pair.Delete : null;
        if (Source == null) { DestroyImmediate(Holder); return null; }
        Source.transform.SetParent(Row, false);
        // At once, so the rest of the copied row never runs a frame.
        DestroyImmediate(Holder);
        GameObject Made = Source.gameObject;
        Made.name = TextKey ?? "Button";
        for (int i = Made.transform.childCount - 1; i >= 0; --i) DestroyImmediate(Made.transform.GetChild(i).gameObject);
        Place(Made.GetComponent<RectTransform>(), X, Width, Ratio);

        Image Face = Made.GetComponent<Image>();
        if (Face != null) LimThemeManager.Paint(Face, Pressed ? PressedColor : Color.white);
        Button Btn = Made.GetComponent<Button>();
        Btn.onClick = new Button.ButtonClickedEvent();
        Btn.interactable = true;
        Btn.onClick.AddListener(OnClick);
        if (TextKey == null) return Btn;

        Text Label = LimUiBuilder.CreateLabel(Made.GetComponent<RectTransform>(), "Text", TimeGroupsFont, 16, TimeGroupsInk, TextAnchor.MiddleCenter);
        RectTransform LabelRect = Label.rectTransform;
        LabelRect.anchorMin = Vector2.zero;
        LabelRect.anchorMax = Vector2.one;
        LabelRect.pivot = new Vector2(0.5f, 0.5f);
        LabelRect.offsetMin = new Vector2(2, 0);
        LabelRect.offsetMax = new Vector2(-2, 0);
        Label.resizeTextForBestFit = true;
        Label.resizeTextMinSize = 8;
        Label.resizeTextMaxSize = 16;
        Label.text = LimLanguageManager.TextDict[TextKey];
        LimThemeManager.Paint(Label, TimeGroupsInk);
        return Btn;
    }

    private Text AddTimeGroupsText(Transform Row, string Words, float X, float Width, float Ratio)
    {
        // The panel heading's own colour, not the one a theme painted on it.
        Color Ink = TimeGroupsLabel != null ? LimThemeManager.OriginalOf(TimeGroupsLabel) : Color.white;
        Text Label = LimUiBuilder.CreateLabel(Row as RectTransform, "Text", TimeGroupsFont, 16, Ink, TextAnchor.MiddleLeft);
        LimThemeManager.Paint(Label, Ink);
        Place(Label.rectTransform, X, Width, Ratio);
        Label.resizeTextForBestFit = true;
        Label.resizeTextMinSize = 8;
        Label.resizeTextMaxSize = 16;
        Label.text = Words;
        return Label;
    }

    /// <summary>The hover balloon the rest of the editor uses, on a control that catches the pointer.</summary>
    private void AddTimeGroupsHint(Component Target, string HintKey)
    {
        if (Target == null) return;
        LimMouseOverHint Hint = Target.gameObject.AddComponent<LimMouseOverHint>();
        Hint.HintTextDictKey = HintKey;
        Hint.Font = TimeGroupsFont;
    }
}
