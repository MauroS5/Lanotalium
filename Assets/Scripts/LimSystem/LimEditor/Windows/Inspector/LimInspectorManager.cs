using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class LimInspectorManager : MonoBehaviour
{
    public RectTransform ViewRect, ComponentRect;
    public LimWindowManager BaseWindow;
    public LimOperationManager OperationManager;
    public Image BpmListSwitcherImg, ScrollListSwitcherImg, DefaultSwitcherImg;
    public Color UnpressedColor, PressedColor;
    public Text BpmSwitcherText, ScrollSpeedSwitcherText, DefaultSwitcherText;
    public LimTunerManager TunerManager;

    public ComponentBasicManager ComponentBasic;
    public ComponentTypeManager ComponentType;
    public ComponentHoldNoteManager ComponentHoldNote;
    public ComponentMotionManager ComponentMotion;
    public ComponentBpmManager ComponentBpm;
    public ComponentScrollSpeedManager ComponentScrollSpeed;
    public ComponentDefaultManager ComponentDefault;

    public void SetTexts()
    {
        BaseWindow.WindowName = LimLanguageManager.TextDict["Window_Inspector_Label"];
        BpmSwitcherText.text = LimLanguageManager.TextDict["Window_Inspector_Switcher_Bpm"];
        ScrollSpeedSwitcherText.text = LimLanguageManager.TextDict["Window_Inspector_Switcher_Scroll"];
        DefaultSwitcherText.text = LimLanguageManager.TextDict["Window_Inspector_Switcher_Default"];
        SetTimeGroupsTexts();
    }
    private void Update()
    {
        DetectMouseScroll();
        UpdateTimeGroupsUi();
    }
    private void OnEnable()
    {
        ArrangeComponentsUi();
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
                    ComponentRect.anchoredPosition = new Vector2(0, Mathf.Clamp(ComponentRect.anchoredPosition.y + Scroll, 0, Mathf.Max(0, ComponentRect.sizeDelta.y - ViewRect.sizeDelta.y)));
                }
            }
        }
    }
    public void OnSelectChange()
    {
        ComponentBasic.OnSelectChange();
        ComponentType.OnSelectChange();
        ComponentHoldNote.OnSelectChange();
        ArrangeComponentsUi();
    }
    public void ArrangeComponentsUi()
    {
        float Height = 0;
        if (ComponentBasic.gameObject.activeInHierarchy)
        {
            Height -= ComponentBasic.ComponentRect.sizeDelta.y;
        }
        if (ComponentType.gameObject.activeInHierarchy)
        {
            ComponentType.ComponentRect.anchoredPosition = new Vector2(0, Height);
            Height -= ComponentType.ComponentRect.sizeDelta.y;
        }
        if (ComponentHoldNote.gameObject.activeInHierarchy)
        {
            ComponentHoldNote.ComponentRect.anchoredPosition = new Vector2(0, Height);
            Height -= ComponentHoldNote.ComponentRect.sizeDelta.y;
        }
        if (ComponentMotion.gameObject.activeInHierarchy)
        {
            ComponentMotion.ComponentRect.anchoredPosition = new Vector2(0, Height);
            Height -= ComponentMotion.ComponentRect.sizeDelta.y;
        }
        if (ComponentBpm.ComponentBpmView.activeInHierarchy)
        {
            ComponentBpm.ComponentRect.anchoredPosition = new Vector2(0, Height);
            Height -= ComponentBpm.ComponentRect.sizeDelta.y;
        }
        if (ComponentScrollSpeed.gameObject.activeInHierarchy)
        {
            ComponentScrollSpeed.ComponentRect.anchoredPosition = new Vector2(0, Height);
            Height -= ComponentScrollSpeed.ComponentRect.sizeDelta.y;
        }
        if (ComponentDefault.gameObject.activeInHierarchy)
        {
            ComponentDefault.ComponentRect.anchoredPosition = new Vector2(0, Height);
            Height -= ComponentDefault.ComponentRect.sizeDelta.y;
        }
        Height = ArrangeTimeGroupsUi(Height);
        ComponentRect.sizeDelta = new Vector2(0, -Height);
    }
    public void SwitchScrollSpeedList()
    {
        if (LimSystem.ChartContainer == null) return;
        if (TunerManager.ScrollManager.DisableChartSpeed) return;
        if (ComponentScrollSpeed.gameObject.activeInHierarchy)
        {
            LimThemeManager.Paint(ScrollListSwitcherImg, UnpressedColor);
            ComponentScrollSpeed.gameObject.SetActive(false);
            ArrangeComponentsUi();
        }
        else
        {
            CloseTimeGroups();
            LimThemeManager.Paint(ScrollListSwitcherImg, PressedColor);
            ComponentScrollSpeed.gameObject.SetActive(true);
            ComponentScrollSpeed.InstantiateScrollSpeedList();
            ArrangeComponentsUi();
        }
    }
    public void SwitchBpmList()
    {
        if (LimSystem.ChartContainer == null) return;
        if (ComponentBpm.ComponentBpmView.activeInHierarchy)
        {
            LimThemeManager.Paint(BpmListSwitcherImg, UnpressedColor);
            ComponentBpm.ComponentBpmView.SetActive(false);
            ArrangeComponentsUi();
        }
        else
        {
            CloseTimeGroups();
            LimThemeManager.Paint(BpmListSwitcherImg, PressedColor);
            ComponentBpm.ComponentBpmView.SetActive(true);
            ComponentBpm.InstantiateBpmList();
            ArrangeComponentsUi();
        }
    }
    public void SwitchDefault()
    {
        if (LimSystem.ChartContainer == null) return;
        if (ComponentDefault.gameObject.activeInHierarchy)
        {
            LimThemeManager.Paint(DefaultSwitcherImg, UnpressedColor);
            ComponentDefault.gameObject.SetActive(false);
            ArrangeComponentsUi();
        }
        else
        {
            CloseTimeGroups();
            LimThemeManager.Paint(DefaultSwitcherImg, PressedColor);
            ComponentDefault.gameObject.SetActive(true);
            ComponentDefault.LoadDefaultValues();
            ArrangeComponentsUi();
        }
    }
}
