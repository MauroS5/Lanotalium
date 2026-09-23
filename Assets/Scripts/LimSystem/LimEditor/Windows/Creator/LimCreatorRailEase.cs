using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Set Rail Ease row of the Creator, under Segment Rail Note.
///
/// Another copy of the Create Catch Rail row, so it arrives with the same
/// button and the same small field. The field takes an ease from 0 to 12,
/// the same numbers a motion takes, and it is written to every joint of
/// every selected rail at once. Unlike the rows above it this one is happy
/// with any number of rails selected, since that is the whole point of it.
/// </summary>
public partial class LimCreatorManager
{
    private Text RailEaseText, RailEaseHintText;
    private InputField RailEaseInputField;

    private void CreateRailEaseRow()
    {
        if (SegmentRailInputField == null) return;
        RectTransform Source = SegmentRailInputField.transform.parent as RectTransform;
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
        Clone.name = "SetRailEase";
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
            Btn.onClick = new Button.ButtonClickedEvent();
            Btn.onClick.AddListener(SetRailEase);
        }

        RailEaseInputField = Clone.GetComponentInChildren<InputField>(true);
        if (RailEaseInputField != null)
        {
            RailEaseInputField.onValueChanged = new InputField.OnChangeEvent();
            RailEaseInputField.onEndEdit = new InputField.SubmitEvent();
            RailEaseInputField.text = string.Empty;
            RailEaseHintText = RailEaseInputField.placeholder as Text;
        }
        foreach (Text Candidate in Clone.GetComponentsInChildren<Text>(true))
        {
            if (RailEaseInputField != null && Candidate.transform.IsChildOf(RailEaseInputField.transform)) continue;
            RailEaseText = Candidate;
            break;
        }
        if (LimLanguageManager.TextDict != null) SetRailEaseTexts();
    }

    public void SetRailEaseTexts()
    {
        if (RailEaseText != null) RailEaseText.text = LimLanguageManager.TextDict["Window_Creator_RailEase"];
        if (RailEaseHintText != null) RailEaseHintText.text = LimLanguageManager.TextDict["Window_Creator_RailEase_Ease"];
    }

    public void SetRailEase()
    {
        if (LimSystem.ChartContainer == null) return;
        if (RailEaseInputField == null || OperationManager == null) return;

        int Ease;
        if (!int.TryParse(RailEaseInputField.text, out Ease) || !LimOperationManager.IsGroupEaseInRange(Ease))
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_RailEase_ErrEase"]);
            return;
        }
        if (!OperationManager.SetSelectedRailsEase(Ease))
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_RailEase_ErrSelection"]);
            return;
        }
        LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_RailEase_Success"]);
    }
}
