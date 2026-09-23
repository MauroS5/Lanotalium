using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class LimCreatorManager : MonoBehaviour
{
    public RectTransform ViewRect, ContentRect;
    public LimTunerManager TunerManager;
    public LimWindowManager BaseWindow;
    public LimOperationManager OperationManager;
    public LimGizmoMotionManager GizmoMotionManager;
    public Text CreateTapText, CreateHoldText, CreateMotionHorText, CreateMotionVerText,
        CreateMotionRotText, CreateMotionMaunallyText, CreateBpmText, CreateScrollSpeedText,
        CreateCatchRailText, CreateCatchRailQuantityText, AutoHighlightText, DeleteSelectedText, ConvertSelectedToHoldNoteText;
    public Text ClickToCreateText, AngleLineText, CopierText;
    public LimClickToCreateManager ClickToCreateManager;
    public LimAngleLineManager AngleLineManager;
    public LimCopierManager CopierManager;
    /// <summary>Built at runtime between Angleline and Copier. See CreateGridTool.</summary>
    public LimGridManager GridManager;
    /// <summary>Built at runtime between Click To Create and Angleline.</summary>
    public LimFavouriteGroupsManager FavouriteGroupsManager;
    public InputField CreateCatchRailQuantityInputField;
    public List<Canvas> SubCanvases = new List<Canvas>();

    /// <summary>
    /// Row of Flip buttons built at runtime under Auto Highlight, so the
    /// scene keeps only the buttons it always had. See CreateFlipButtons.
    /// </summary>
    private Text FlipHorizontalText, FlipVerticalText, SelectEvenText, SelectOddText;
    private const float CreatorRowStep = 35;
    private float CreatorHeaderHeight = 375;

    public void SetTexts()
    {
        BaseWindow.WindowName = LimLanguageManager.TextDict["Window_Creator_Label"];
        CreateTapText.text = LimLanguageManager.TextDict["Window_Creator_CreateTap"];
        CreateHoldText.text = LimLanguageManager.TextDict["Window_Creator_CreateHold"];
        CreateMotionHorText.text = LimLanguageManager.TextDict["Window_Creator_CreateMotionHor"];
        CreateMotionVerText.text = LimLanguageManager.TextDict["Window_Creator_CreateMotionVer"];
        CreateMotionRotText.text = LimLanguageManager.TextDict["Window_Creator_CreateMotionRot"];
        CreateMotionMaunallyText.text = LimLanguageManager.TextDict["Window_Creator_CreateMotionManually"];
        CreateBpmText.text = LimLanguageManager.TextDict["Window_Creator_CreateBpm"];
        CreateScrollSpeedText.text = LimLanguageManager.TextDict["Window_Creator_CreateScrollSpeed"];
        CreateCatchRailText.text = LimLanguageManager.TextDict["Window_Creator_CreateCatchRail"];
        CreateCatchRailQuantityText.text = LimLanguageManager.TextDict["Window_Creator_CreateCatchRail_Quantity"];
        AutoHighlightText.text = LimLanguageManager.TextDict["Window_Creator_AutoHighlight"];
        DeleteSelectedText.text = LimLanguageManager.TextDict["Window_Creator_DeleteSelected"];
        ConvertSelectedToHoldNoteText.text = LimLanguageManager.TextDict["Window_Creator_ConvertSelectedToHoldNote"];
        ClickToCreateText.text = LimLanguageManager.TextDict["Window_Creator_ClickToCreate"];
        AngleLineText.text = LimLanguageManager.TextDict["Window_Creator_Angleline"];
        CopierText.text = LimLanguageManager.TextDict["Window_Creator_Copier"];
        if (GridManager != null) GridManager.SetTexts();
        if (FavouriteGroupsManager != null) FavouriteGroupsManager.SetTexts();
        // Built in Start, so a language change arriving first finds them null.
        if (FlipHorizontalText != null) FlipHorizontalText.text = LimLanguageManager.TextDict["Window_Creator_FlipHorizontal"];
        if (FlipVerticalText != null) FlipVerticalText.text = LimLanguageManager.TextDict["Window_Creator_FlipVertical"];
        if (SelectEvenText != null) SelectEvenText.text = LimLanguageManager.TextDict["Window_Creator_SelectEven"];
        if (SelectOddText != null) SelectOddText.text = LimLanguageManager.TextDict["Window_Creator_SelectOdd"];
        SetGroupEaseTexts();
        SetSegmentRailTexts();
        SetRailEaseTexts();
        SetCatchRailOptionTexts();
        SetSingleHoldText();
        SetCreateMotionTrsText();
    }
    private void Start()
    {
        // First: it sits with the motion buttons, above every row the rest
        // of these add, and each of those works from where its neighbours
        // already are, so they all land correctly beneath it.
        CreateMotionTransparencyRow();
        // Before the Flip row: that one pushes down whatever sits below Auto
        // Highlight, and this one has to have moved Auto Highlight first.
        CreateSingleHoldRow();
        CreateGroupEaseRow();
        CreateSegmentRailRow();
        CreateRailEaseRow();
        // Last of all. Create Group Ease, Segment Hold Note and Set Rail Ease
        // are copies of the Create Catch Rail row, one made from the next, so
        // anything already sitting on that row is copied onto all three: with
        // this built first, Reverse and Spins appeared on four rows instead
        // of the one they belong to.
        CreateCatchRailOptions();
        CreateFlipButtons();
        CreateFavouriteGroupsTool();
        CreateGridTool();
        // After every row above has been added: see LimCreatorCompactRows.cs.
        CompactCreatorRows();
        ArrangeCreatorsUi();
        BaseWindow.OnWindowSorted.AddListener(OnWindowSorted);
    }
    private void OnWindowSorted(int Order)
    {

    }
    private void Update()
    {
        DetectHotkeys();
        DetectMouseScroll();
    }
    private void DetectHotkeys()
    {
        if (Input.GetKey(KeyCode.LeftControl))
        {
            if (Input.GetKeyDown(KeyCode.T)) CreateTapNote();
            else if (Input.GetKeyDown(KeyCode.H)) CreateHoldNote();
            else if (Input.GetKeyDown(KeyCode.B)) CreateBpm();
            // Ctrl+F already made a scroll speed before it was asked to keep
            // favourites. With something selected the selection wins, since
            // a scroll speed is made with nothing selected anyway.
            else if (Input.GetKeyDown(KeyCode.F) && !HasAnythingSelected()) CreateScrollSpeed();
        }
    }
    private bool HasAnythingSelected()
    {
        if (OperationManager == null) return false;
        return OperationManager.SelectedTapNote.Count > 0
            || OperationManager.SelectedHoldNote.Count > 0
            || OperationManager.SelectedMotions.Count > 0;
    }
    private void DetectMouseScroll()
    {
        float Scroll = -Input.GetAxis("Mouse ScrollWheel") * 200;
        if (Scroll != 0)
        {
            Vector3 Mouse = LimMousePosition.MousePosition;
            if (Mouse.x >= ViewRect.anchoredPosition.x && Mouse.x <= ViewRect.anchoredPosition.x + ViewRect.sizeDelta.x)
            {
                if (Mouse.y >= ViewRect.anchoredPosition.y - ViewRect.sizeDelta.y && Mouse.y <= ViewRect.anchoredPosition.y)
                {
                    ContentRect.anchoredPosition = new Vector2(0, Mathf.Clamp(ContentRect.anchoredPosition.y + Scroll, 0, Mathf.Max(0, ContentRect.sizeDelta.y - ViewRect.sizeDelta.y)));
                }
            }
        }
    }

    public void CreateTapNote()
    {
        if (LimSystem.ChartContainer == null) return;
        Lanotalium.Chart.LanotaTapNote New = new Lanotalium.Chart.LanotaTapNote();
        New.Time = TunerManager.ChartTime;
        New.Group = LimTimeGroups.ActiveGroup;
        OperationManager.AddTapNote(New);
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }
    public void CreateHoldNote()
    {
        if (LimSystem.ChartContainer == null) return;
        Lanotalium.Chart.LanotaHoldNote New = new Lanotalium.Chart.LanotaHoldNote();
        New.Type = 5;
        New.Time = TunerManager.ChartTime;
        New.Duration = 1;
        New.Group = LimTimeGroups.ActiveGroup;
        OperationManager.AddHoldNote(New);
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }
    public void CreateMotionHorizontal()
    {
        if (LimSystem.ChartContainer == null) return;
        Lanotalium.Chart.LanotaCameraXZ New = new Lanotalium.Chart.LanotaCameraXZ();
        New.Time = TunerManager.ChartTime;
        New.Type = 8;
        New.Duration = 0.00001f;
        OperationManager.AddHorizontal(New);
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }
    public void CreateMotionVertical()
    {
        if (LimSystem.ChartContainer == null) return;
        Lanotalium.Chart.LanotaCameraY New = new Lanotalium.Chart.LanotaCameraY();
        New.Time = TunerManager.ChartTime;
        New.Type = 10;
        New.Duration = 0.00001f;
        OperationManager.AddVertical(New);
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }
    public void CreateMotionRotation()
    {
        if (LimSystem.ChartContainer == null) return;
        Lanotalium.Chart.LanotaCameraRot New = new Lanotalium.Chart.LanotaCameraRot();
        New.Time = TunerManager.ChartTime;
        New.Type = 13;
        New.Duration = 0.00001f;
        OperationManager.AddRotation(New);
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }
    public void CreateGizmoMotion()
    {
        if (LimSystem.ChartContainer == null) return;
        TunerManager.MediaPlayerManager.IsPlaying = false;
        GizmoMotionManager.Create();
    }
    public void CreateBpm()
    {
        if (LimSystem.ChartContainer == null) return;
        Lanotalium.Chart.LanotaChangeBpm New = new Lanotalium.Chart.LanotaChangeBpm();
        New.Time = TunerManager.ChartTime < 0 ? 0 : TunerManager.ChartTime;
        New.Bpm = 100;
        OperationManager.AddBpm(New);
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }
    public void CreateScrollSpeed()
    {
        if (LimSystem.ChartContainer == null) return;
        if (TunerManager.ScrollManager.DisableChartSpeed) return;
        // With a time group being worked on, the speed is that group's.
        if (LimTimeGroups.ActiveGroup != LimTimeGroups.BaseGroup)
        {
            OperationManager.AddGroupScrollSpeed(LimTimeGroups.ActiveGroup, TunerManager.ChartTime);
            return;
        }
        Lanotalium.Chart.LanotaScroll New = new Lanotalium.Chart.LanotaScroll
        {
            Time = TunerManager.ChartTime < 0 ? 0 : TunerManager.ChartTime,
            Speed = 1
        };
        OperationManager.AddScrollSpeed(New);
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }

    public void CreateCatchRail()
    {
        if (LimSystem.ChartContainer == null) return;
        int Quantity;
        if (OperationManager.SelectedTapNote.Count != 2) { LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_CreateCatchRail_ErrSelection"]); return; }
        if (!int.TryParse(CreateCatchRailQuantityInputField.text, out Quantity)) { LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_CreateCatchRail_ErrQuantity"]); return; }
        if (Quantity <= 0) { LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_CreateCatchRail_ErrRange"]); return; }
        OperationManager.SelectedTapNote.Sort((Lanotalium.Chart.LanotaTapNote a, Lanotalium.Chart.LanotaTapNote b) => { return a.Time.CompareTo(b.Time); });
        float DeltaTime = (OperationManager.SelectedTapNote[1].Time - OperationManager.SelectedTapNote[0].Time) / (Quantity + 1);
        // Taking the two degrees away from one another read a run from 350 to
        // 10 as a journey of -340 and sent it all the way round the back.
        // CatchRailSweep answers the short way unless Reverse says otherwise,
        // and carries the spins.
        float DeltaDegree = CatchRailSweep(OperationManager.SelectedTapNote[0].Degree, OperationManager.SelectedTapNote[1].Degree) / (Quantity + 1);
        for (int i = 1; i <= Quantity; ++i)
        {
            Lanotalium.Chart.LanotaTapNote New = OperationManager.SelectedTapNote[0].DeepCopy();
            New.Type = 4;
            New.Time += i * DeltaTime;
            New.Degree = LimMathUtil.NormalizeDegree(New.Degree + i * DeltaDegree);
            OperationManager.AddTapNote(New, true, false, false);
        }
        LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_CreateCatchRail_Success"]);
    }
    public void ConvertSelectedToHoldNote()
    {
        if (LimSystem.ChartContainer == null) return;
        OperationManager.SelectedTapNote.Sort((Lanotalium.Chart.LanotaTapNote a, Lanotalium.Chart.LanotaTapNote b) => { return a.Time.CompareTo(b.Time); });
        int Quantity = OperationManager.SelectedTapNote.Count;
        if (Quantity == 0) return;
        else if (Quantity == 1) { OperationManager.ConvertTapNoteToHoldNote(OperationManager.SelectedTapNote[0]); return; }
        else
        {
            Lanotalium.Chart.LanotaHoldNote New = new Lanotalium.Chart.LanotaHoldNote
            {
                Duration = OperationManager.SelectedTapNote[Quantity - 1].Time - OperationManager.SelectedTapNote[0].Time,
                Time = OperationManager.SelectedTapNote[0].Time,
                Degree = OperationManager.SelectedTapNote[0].Degree,
                Type = 5,
                Size = 1,
                Jcount = Quantity - 1,
                Joints = new List<Lanotalium.Chart.LanotaJoints>(),
                Group = OperationManager.SelectedTapNote[0].Group
            };
            float DegreeCount = New.Degree;
            float TimeCount = New.Time;
            for (int i = 1; i < Quantity; ++i)
            {
                Lanotalium.Chart.LanotaJoints NewJ = new Lanotalium.Chart.LanotaJoints();
                NewJ.Cfmi = 0;
                NewJ.dTime = Mathf.Max(0.0001f, OperationManager.SelectedTapNote[i].Time - TimeCount);
                NewJ.dDegree = OperationManager.SelectedTapNote[i].Degree - DegreeCount;
                New.Joints.Add(NewJ);
                TimeCount += NewJ.dTime;
                DegreeCount += NewJ.dDegree;
            }
            foreach (Lanotalium.Chart.LanotaTapNote Tap in OperationManager.SelectedTapNote) OperationManager.DeleteTapNote(Tap);
            OperationManager.SelectedTapNote.Clear();
            OperationManager.AddHoldNote(New, true, true, true);
        }
    }
    /// <summary>
    /// Notes this close together count as falling at the same moment. Taken
    /// from 184 real charts: the gaps between neighbouring notes pile up below
    /// 5 ms (chords placed a hair apart, over two thousand of them, which the
    /// old four-decimal comparison left unhighlighted), almost vanish from 8
    /// to 13 ms, and only become rhythm again from about 16 ms. 8 ms sits in
    /// that empty stretch, and is half a frame, so notes this close land on
    /// screen together; notes of a time group placed while its effects are
    /// showing are easily this far off the note they were meant to join.
    /// </summary>
    private const float AutoHighlightTolerance = 0.008f;

    /// <summary>
    /// Highlights every note that falls at the same moment as another, and
    /// clears it on every note that does not, whatever time groups they are
    /// in. The note's own time is what reaches the judge line, so that is all
    /// that is compared. Only notes whose highlight actually changes are
    /// rebuilt, and the whole pass is one undo.
    /// </summary>
    public void AutoHighlight()
    {
        List<KeyValuePair<float, object>> Notes = new List<KeyValuePair<float, object>>();
        foreach (Lanotalium.Chart.LanotaTapNote Tap in TunerManager.TapNoteManager.TapNote) Notes.Add(new KeyValuePair<float, object>(Tap.Time, Tap));
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in TunerManager.HoldNoteManager.HoldNote) Notes.Add(new KeyValuePair<float, object>(Hold.Time, Hold));
        Notes.Sort((KeyValuePair<float, object> A, KeyValuePair<float, object> B) => { return A.Key.CompareTo(B.Key); });

        // Runs of notes each within the tolerance of the one before.
        Dictionary<object, bool> Wanted = new Dictionary<object, bool>();
        int Start = 0;
        for (int i = 1; i <= Notes.Count; ++i)
        {
            if (i < Notes.Count && Notes[i].Key - Notes[i - 1].Key <= AutoHighlightTolerance) continue;
            bool Together = i - Start > 1;
            for (int k = Start; k < i; ++k) Wanted[Notes[k].Value] = Together;
            Start = i;
        }

        List<Lanotalium.Chart.LanotaTapNote> Taps = new List<Lanotalium.Chart.LanotaTapNote>();
        List<Lanotalium.Chart.LanotaHoldNote> Holds = new List<Lanotalium.Chart.LanotaHoldNote>();
        foreach (KeyValuePair<object, bool> Entry in Wanted)
        {
            Lanotalium.Chart.LanotaTapNote Tap = Entry.Key as Lanotalium.Chart.LanotaTapNote;
            if (Tap != null) { if (Tap.Combination != Entry.Value) Taps.Add(Tap); continue; }
            Lanotalium.Chart.LanotaHoldNote Hold = Entry.Key as Lanotalium.Chart.LanotaHoldNote;
            if (Hold != null && Hold.Combination != Entry.Value) Holds.Add(Hold);
        }
        if (Taps.Count + Holds.Count == 0) return;

        // Every note listed changes, so doing it again undoes it.
        System.Action Flip = () =>
        {
            foreach (Lanotalium.Chart.LanotaTapNote Tap in Taps) OperationManager.SetTapNoteCombination(Tap, !Tap.Combination, false);
            foreach (Lanotalium.Chart.LanotaHoldNote Hold in Holds) OperationManager.SetHoldNoteCombination(Hold, !Hold.Combination, false);
            OperationManager.InspectorManager.OnSelectChange();
        };
        Flip();
        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() => { Flip(); });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() => { Flip(); });
        OperationManager.AddToOperationSaver(OpSave);
    }

    public void DeleteSelected()
    {
        if (LimSystem.ChartContainer == null) return;
        OperationManager.DeleteAllSelected();
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }

    /// <summary>
    /// Adds two rows right under Auto Highlight, the Flip pair and the
    /// even / odd pair, by cloning that button: the clones inherit the look
    /// of the panel without the scene having to carry them.
    ///
    /// Everything that sat below Auto Highlight slides down by those rows,
    /// and the header the collapsible tools are stacked under grows by the
    /// same amount, so nothing ends up overlapping.
    /// </summary>
    private void CreateFlipButtons()
    {
        if (AutoHighlightText == null || OperationManager == null) return;
        RectTransform Source = AutoHighlightText.rectTransform.parent as RectTransform;
        if (Source == null || Source.parent == null) return;

        const int Rows = 2;
        float FlipRowY = Source.anchoredPosition.y - CreatorRowStep;
        float SelectRowY = FlipRowY - CreatorRowStep;
        for (int i = 0; i < Source.parent.childCount; ++i)
        {
            RectTransform Sibling = Source.parent.GetChild(i) as RectTransform;
            if (Sibling == null || Sibling == Source) continue;
            if (Sibling.anchoredPosition.y < Source.anchoredPosition.y)
                Sibling.anchoredPosition = new Vector2(Sibling.anchoredPosition.x, Sibling.anchoredPosition.y - CreatorRowStep * Rows);
        }
        CreatorHeaderHeight += CreatorRowStep * Rows;

        FlipHorizontalText = CreateCreatorButton(Source, "FlipHorizontal", FlipRowY, true, OperationManager.FlipSelectionHorizontal);
        FlipVerticalText = CreateCreatorButton(Source, "FlipVertical", FlipRowY, false, OperationManager.FlipSelectionVertical);
        SelectEvenText = CreateCreatorButton(Source, "SelectEven", SelectRowY, true, OperationManager.SelectEvenOfSelection);
        SelectOddText = CreateCreatorButton(Source, "SelectOdd", SelectRowY, false, OperationManager.SelectOddOfSelection);
        if (LimLanguageManager.TextDict != null) SetTexts();
    }
    private Text CreateCreatorButton(RectTransform Source, string Name, float RowY, bool LeftHalf, UnityEngine.Events.UnityAction OnClick)
    {
        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = Name;

        // Half the width each, with a small gap down the middle.
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchorMin = new Vector2(LeftHalf ? 0 : 0.5f, 1);
        Rect.anchorMax = new Vector2(LeftHalf ? 0.5f : 1, 1);
        Rect.pivot = Source.pivot;
        Rect.sizeDelta = new Vector2(-2.5f, Source.sizeDelta.y);
        Rect.anchoredPosition = new Vector2(LeftHalf ? -1.25f : 1.25f, RowY);
        Rect.SetSiblingIndex(Source.GetSiblingIndex() + 1);

        Button Btn = Clone.GetComponent<Button>();
        if (Btn != null)
        {
            // Replacing the event drops the Auto Highlight call the clone
            // inherited; RemoveAllListeners would leave that one in place.
            Btn.onClick = new Button.ButtonClickedEvent();
            Btn.onClick.AddListener(OnClick);
        }
        return Clone.GetComponentInChildren<Text>();
    }

    public void ArrangeCreatorsUi()
    {
        float Height = -CreatorHeaderHeight;
        Height -= ClickToCreateManager.ToolBase.Height;
        if (FavouriteGroupsManager != null)
        {
            FavouriteGroupsManager.ToolBase.ToolRect.anchoredPosition = new Vector2(0, Height);
            Height -= FavouriteGroupsManager.ToolBase.Height;
        }
        AngleLineManager.ToolBase.ToolRect.anchoredPosition = new Vector2(0, Height);
        Height -= AngleLineManager.ToolBase.Height;
        if (GridManager != null)
        {
            GridManager.ToolBase.ToolRect.anchoredPosition = new Vector2(0, Height);
            Height -= GridManager.ToolBase.Height;
        }
        CopierManager.ToolBase.ToolRect.anchoredPosition = new Vector2(0, Height);
        Height -= CopierManager.ToolBase.Height;
        ContentRect.sizeDelta = new Vector2(0, -Height);
    }
}