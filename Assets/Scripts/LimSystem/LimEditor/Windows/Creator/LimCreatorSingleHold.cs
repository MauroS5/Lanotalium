using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Convert To Single Hold Note row, under Convert To Hold Note.
///
/// A copy of the row above it, which is a plain button with no field, so it
/// arrives looking like what it is. Several hold notes lying end to end are
/// joined into one: the first keeps its head and the rest become its joints.
/// The work is LimOperationManager.MergeSelectedRails.
/// </summary>
public partial class LimCreatorManager
{
    private Text SingleHoldText;

    private void CreateSingleHoldRow()
    {
        if (ConvertSelectedToHoldNoteText == null) return;
        RectTransform Source = ConvertSelectedToHoldNoteText.transform.parent as RectTransform;
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
        Clone.name = "ConvertToSingleHoldNote";
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
            // Replacing the event drops the Convert To Hold Note call;
            // removing the listeners would leave that one in place.
            Btn.onClick = new Button.ButtonClickedEvent();
            Btn.onClick.AddListener(ConvertSelectedToSingleHoldNote);
        }
        SingleHoldText = Clone.GetComponentInChildren<Text>(true);
        if (LimLanguageManager.TextDict != null) SetSingleHoldText();
    }

    public void SetSingleHoldText()
    {
        if (SingleHoldText == null) return;
        SingleHoldText.text = LimLanguageManager.TextDict["Window_Creator_ConvertToSingleHoldNote"];
    }

    public void ConvertSelectedToSingleHoldNote()
    {
        if (LimSystem.ChartContainer == null) return;
        if (OperationManager == null) return;
        if (!OperationManager.MergeSelectedRails())
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_ConvertToSingleHoldNote_Err"]);
            return;
        }
        LimNotifyIcon.ShowMessage(LimLanguageManager.TextDict["Window_Creator_ConvertToSingleHoldNote_Success"]);
    }
}
