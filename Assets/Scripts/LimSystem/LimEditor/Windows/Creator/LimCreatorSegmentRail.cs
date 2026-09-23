using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Segment Rail Note row of the Creator, under Create Group Ease.
///
/// It is another copy of the Create Catch Rail row, so it arrives with the
/// same button and the same small field beside it. The field takes how many
/// pieces the selected rail is to be cut into, and the work is
/// LimOperationManager.SegmentRail.
///
/// One rail and nothing else has to be selected. Two would leave no saying
/// which was meant, and the pieces of the first would land in the selection
/// while the second was still being read.
/// </summary>
public partial class LimCreatorManager
{
    private Text SegmentRailText, SegmentRailHintText;
    private InputField SegmentRailInputField;

    private void CreateSegmentRailRow()
    {
        if (GroupEaseInputField == null) return;
        RectTransform Source = GroupEaseInputField.transform.parent as RectTransform;
        if (Source == null || Source.parent == null) return;

        float RowY = Source.anchoredPosition.y - CreatorRowStep;
        for (int i = 0; i < Source.parent.childCount; ++i)
        {
            RectTransform Sibling = Source.parent.GetChild(i) as RectTransform;
            if (Sibling == null || Sibling == Source) continue;
            if (Sibling.anchoredPosition.y < Source.anchoredPosition.y)
                Sibling.anchoredPosition = new Vector2(Sibling.anchoredPosition.x, Sibling.anchoredPosition.y - CreatorRowStep);
        }
        CreatorHeaderHeight += CreatorRowStep;

        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = "SegmentRailNote";
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchorMin = Source.anchorMin;
        Rect.anchorMax = Source.anchorMax;
        Rect.pivot = Source.pivot;
        Rect.sizeDelta = Source.sizeDelta;
        Rect.anchoredPosition = new Vector2(Source.anchoredPosition.x, RowY);
        Rect.SetSiblingIndex(Source.GetSiblingIndex() + 1);
        // The copy came explaining the row it was copied from, and reporting
        // to it as well.
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
        foreach (UnityEngine.EventSystems.EventTrigger Inherited in Clone.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)) DestroyImmediate(Inherited);

        Button Btn = Clone.GetComponent<Button>();
        if (Btn != null)
        {
            // Replacing the event drops the Create Group Ease call; removing
            // the listeners would leave that one in place.
            Btn.onClick = new Button.ButtonClickedEvent();
            Btn.onClick.AddListener(SegmentRailNote);
        }

        SegmentRailInputField = Clone.GetComponentInChildren<InputField>(true);
        if (SegmentRailInputField != null)
        {
            SegmentRailInputField.onValueChanged = new InputField.OnChangeEvent();
            SegmentRailInputField.onEndEdit = new InputField.SubmitEvent();
            SegmentRailInputField.text = string.Empty;
            SegmentRailHintText = SegmentRailInputField.placeholder as Text;
        }
        // The row's own label, which is the text that is not in the field.
        foreach (Text Candidate in Clone.GetComponentsInChildren<Text>(true))
        {
            if (SegmentRailInputField != null && Candidate.transform.IsChildOf(SegmentRailInputField.transform)) continue;
            SegmentRailText = Candidate;
            break;
        }
        if (LimLanguageManager.TextDict != null) SetSegmentRailTexts();
    }

    public void SetSegmentRailTexts()
    {
        if (SegmentRailText != null) SegmentRailText.text = LimLanguageManager.TextDict["Window_Creator_SegmentRail"];
        if (SegmentRailHintText != null) SegmentRailHintText.text = LimLanguageManager.TextDict["Window_Creator_SegmentRail_Segments"];
    }

    public void SegmentRailNote()
    {
        if (LimSystem.ChartContainer == null) return;
        if (SegmentRailInputField == null || OperationManager == null) return;

        int Segments;
        if (!int.TryParse(SegmentRailInputField.text, out Segments) || !LimOperationManager.IsRailSegmentCountInRange(Segments))
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_SegmentRail_ErrSegments"]);
            return;
        }
        if (OperationManager.SelectedTapNote.Count != 0 || OperationManager.SelectedHoldNote.Count != 1)
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_SegmentRail_ErrSelection"]);
            return;
        }
        if (!OperationManager.SegmentRail(OperationManager.SelectedHoldNote[0], Segments))
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_SegmentRail_ErrSelection"]);
            return;
        }
        LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_SegmentRail_Success"]);
    }
}
