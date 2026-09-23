using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Create Motion (Transparency), under Create Motion (Rotation).
///
/// A copy of the Rotation button, so it looks like one of the family, placed
/// in the thirty pixels the motion buttons are spaced by and pushing
/// everything below it down by as much.
/// </summary>
public partial class LimCreatorManager
{
    /// <summary>The motion buttons sit 30 apart, not the 35 of the rows added later.</summary>
    private const float CreatorButtonStep = 30;
    private Text CreateMotionTrsText;

    private void CreateMotionTransparencyRow()
    {
        if (CreateMotionRotText == null) return;
        RectTransform Source = CreateMotionRotText.transform.parent as RectTransform;
        if (Source == null || Source.parent == null) return;

        float RowY = Source.anchoredPosition.y - CreatorButtonStep;
        for (int i = 0; i < Source.parent.childCount; ++i)
        {
            RectTransform Sibling = Source.parent.GetChild(i) as RectTransform;
            if (Sibling == null || Sibling == Source) continue;
            if (Sibling.anchoredPosition.y < Source.anchoredPosition.y)
                Sibling.anchoredPosition = new Vector2(Sibling.anchoredPosition.x, Sibling.anchoredPosition.y - CreatorButtonStep);
        }
        CreatorHeaderHeight += CreatorButtonStep;

        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = "CreateMotionTransparency";
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchorMin = Source.anchorMin;
        Rect.anchorMax = Source.anchorMax;
        Rect.pivot = Source.pivot;
        Rect.sizeDelta = Source.sizeDelta;
        Rect.anchoredPosition = new Vector2(Source.anchoredPosition.x, RowY);
        Rect.SetSiblingIndex(Source.GetSiblingIndex() + 1);
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
        foreach (UnityEngine.EventSystems.EventTrigger Inherited in Clone.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)) DestroyImmediate(Inherited);

        Button Btn = Clone.GetComponent<Button>();
        if (Btn != null)
        {
            // A fresh event, or the copy would still create rotations.
            Btn.onClick = new Button.ButtonClickedEvent();
            Btn.onClick.AddListener(CreateMotionTransparency);
        }
        CreateMotionTrsText = Clone.GetComponentInChildren<Text>(true);
        if (LimLanguageManager.TextDict != null) SetCreateMotionTrsText();
    }

    public void SetCreateMotionTrsText()
    {
        if (CreateMotionTrsText == null) return;
        CreateMotionTrsText.text = LimLanguageManager.TextDict["Window_Creator_CreateMotionTrs"];
    }

    /// <summary>
    /// A new transparency motion at the playhead. It starts at whatever the
    /// ring already is at that moment, so creating one changes nothing until
    /// its value is edited, the way a new rotation starts at 0 degrees.
    /// </summary>
    public void CreateMotionTransparency()
    {
        if (LimSystem.ChartContainer == null) return;
        Lanotalium.Chart.LanotaCameraTrs New = new Lanotalium.Chart.LanotaCameraTrs();
        New.Time = TunerManager.ChartTime;
        New.Type = 14;
        New.Duration = 0.00001f;
        New.ctp = Mathf.Round(TunerManager.CameraManager.CalculateTransparency(New.Time));
        OperationManager.AddTransparency(New);
        OperationManager.InspectorManager.ArrangeComponentsUi();
    }
}
