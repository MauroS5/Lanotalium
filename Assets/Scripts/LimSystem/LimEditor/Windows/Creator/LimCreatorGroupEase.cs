using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Create Group Ease row of the Creator, under Create Catch Rail.
///
/// It is a copy of that row, so it arrives with the same button and the same
/// small field beside it, and everything below slides down to make room. The
/// field takes an ease number, the same 0 to 12 a motion takes, and 0 lays the
/// selected run out in a straight line again.
///
/// The work itself is LimOperationManager.ApplyGroupEase: the notes and the
/// single undo entry belong there, not to a panel.
/// </summary>
public partial class LimCreatorManager
{
    private Text GroupEaseText, GroupEaseHintText;
    private InputField GroupEaseInputField;

    private void CreateGroupEaseRow()
    {
        if (CreateCatchRailQuantityInputField == null) return;
        RectTransform Source = CreateCatchRailQuantityInputField.transform.parent as RectTransform;
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
        Clone.name = "CreateGroupEase";
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
            // Replacing the event drops the Create Catch Rail call; removing
            // the listeners would leave that one in place.
            Btn.onClick = new Button.ButtonClickedEvent();
            Btn.onClick.AddListener(CreateGroupEase);
        }

        GroupEaseInputField = Clone.GetComponentInChildren<InputField>(true);
        if (GroupEaseInputField != null)
        {
            GroupEaseInputField.onValueChanged = new InputField.OnChangeEvent();
            GroupEaseInputField.onEndEdit = new InputField.SubmitEvent();
            GroupEaseInputField.text = string.Empty;
            GroupEaseHintText = GroupEaseInputField.placeholder as Text;
        }
        // The row's own label, which is the text that is not in the field.
        foreach (Text Candidate in Clone.GetComponentsInChildren<Text>(true))
        {
            if (GroupEaseInputField != null && Candidate.transform.IsChildOf(GroupEaseInputField.transform)) continue;
            GroupEaseText = Candidate;
            break;
        }
        if (LimLanguageManager.TextDict != null) SetGroupEaseTexts();
    }

    public void SetGroupEaseTexts()
    {
        if (GroupEaseText != null) GroupEaseText.text = LimLanguageManager.TextDict["Window_Creator_CreateGroupEase"];
        if (GroupEaseHintText != null) GroupEaseHintText.text = LimLanguageManager.TextDict["Window_Creator_CreateGroupEase_Ease"];
    }

    public void CreateGroupEase()
    {
        if (LimSystem.ChartContainer == null) return;
        if (GroupEaseInputField == null || OperationManager == null) return;

        int Mode;
        if (!int.TryParse(GroupEaseInputField.text, out Mode) || !LimOperationManager.IsGroupEaseInRange(Mode))
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_CreateGroupEase_ErrEase"]);
            return;
        }
        if (!OperationManager.ApplyGroupEase(Mode))
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_CreateGroupEase_ErrSelection"]);
            return;
        }
        LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_CreateGroupEase_Success"]);
    }
}
