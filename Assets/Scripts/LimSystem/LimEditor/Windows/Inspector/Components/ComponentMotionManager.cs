using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComponentMotionManager : MonoBehaviour
{
    public RectTransform ViewRect, ComponentRect;
    public int UnFoldHeight;
    public LimInspectorManager InspectorManager;
    public LimOperationManager OperationManager;
    public LimGizmoMotionManager GizmoMotionManager;
    public bool EnableValueChange = true;
    public InputField Timing, Duration, Cfmi, Ctp0, Ctp1;
    public Image TimingImg, DurationImg, CfmiImg, Ctp0Img, Ctp1Img;
    public Text LabelText, TimingText, DurationText, CfmiText, Ctp0Text, Ctp1Text, TypeText, ManuallyMotionText;
    public Color InvalidColor, ValidColor;
    public Button Type8, Type11;

    private bool isFolded = false;
    public Lanotalium.Editor.ComponentMotionMode Mode = Lanotalium.Editor.ComponentMotionMode.Idle;
    public int Index = 0;
    private float UiWidth;

    /// <summary>
    /// Invert values, built at runtime as a row of its own under the rest of
    /// the component. See CreateInvertButton.
    /// </summary>
    private Text InvertValuesText;
    private RectTransform InvertValuesRect;

    private void Start()
    {
        CreateInvertButton();
        RefreshUiWidth();
    }

    /// <summary>
    /// Clones the type button to get a button that matches the panel, and
    /// hangs it one row below what the component already had, growing the
    /// component by that row so nothing is covered.
    /// </summary>
    private void CreateInvertButton()
    {
        if (Type8 == null || OperationManager == null) return;
        RectTransform Source = Type8.GetComponent<RectTransform>();
        if (Source == null || Source.parent == null) return;

        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = "InvertValues";
        InvertValuesRect = Clone.GetComponent<RectTransform>();
        InvertValuesRect.anchorMin = Source.anchorMin;
        InvertValuesRect.anchorMax = Source.anchorMax;
        InvertValuesRect.pivot = Source.pivot;

        Button Btn = Clone.GetComponent<Button>();
        if (Btn != null)
        {
            // A fresh event, or the clone would still switch the motion type.
            Btn.onClick = new Button.ButtonClickedEvent();
            Btn.onClick.AddListener(OperationManager.InvertSelectedMotionValues);
            Btn.interactable = true;
        }

        // The type buttons carry an icon, not a label, and a turning arrow
        // would say nothing about what this one does: the icon goes and a
        // written label takes its place.
        foreach (Transform Child in InvertValuesRect) Destroy(Child.gameObject);
        InvertValuesText = CreateInvertLabel(InvertValuesRect);

        UnFoldHeight += 35;
        if (!isFolded) ViewRect.sizeDelta = new Vector2(0, UnFoldHeight);
        ComponentRect.sizeDelta = new Vector2(0, ViewRect.sizeDelta.y - ViewRect.anchoredPosition.y);
        if (LimLanguageManager.TextDict != null) SetTexts();
    }
    /// <summary>
    /// The button's label, stretched over the whole button and borrowing the
    /// font of the component's own texts. Dark, because it sits on the light
    /// face of a button rather than on the panel.
    /// </summary>
    private Text CreateInvertLabel(RectTransform Button)
    {
        GameObject Holder = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Button, false);
        Rect.anchorMin = Vector2.zero;
        Rect.anchorMax = Vector2.one;
        Rect.pivot = new Vector2(0.5f, 0.5f);
        Rect.offsetMin = Vector2.zero;
        Rect.offsetMax = Vector2.zero;

        Text Label = Holder.GetComponent<Text>();
        if (LabelText != null)
        {
            Label.font = LabelText.font;
            Label.fontSize = LabelText.fontSize;
        }
        Label.alignment = TextAnchor.MiddleCenter;
        Label.color = new Color(0.15f, 0.15f, 0.15f);
        Label.raycastTarget = false;
        // Long words in another language shrink rather than spill out, but
        // never grow past the size the panel's own labels use.
        Label.resizeTextForBestFit = true;
        Label.resizeTextMinSize = 8;
        Label.resizeTextMaxSize = Label.fontSize > 0 ? Label.fontSize : 14;
        return Label;
    }

    private void Update()
    {
        OnUiWidthChange();
    }
    public void RefreshUiWidth()
    {
        UiWidth = ViewRect.rect.width;
        float Ratio = UiWidth / 500f;
        Timing.GetComponent<RectTransform>().sizeDelta = new Vector2(200 * Ratio, 30);
        Duration.GetComponent<RectTransform>().sizeDelta = new Vector2(200 * Ratio, 30);
        Cfmi.GetComponent<RectTransform>().sizeDelta = new Vector2(200 * Ratio, 30);
        Ctp0.GetComponent<RectTransform>().sizeDelta = new Vector2(200 * Ratio, 30);
        Ctp1.GetComponent<RectTransform>().sizeDelta = new Vector2(200 * Ratio, 30);
        Type8.GetComponent<RectTransform>().sizeDelta = new Vector2(100 * Ratio, 30);
        Type11.GetComponent<RectTransform>().sizeDelta = new Vector2(100 * Ratio, 30);
        Type11.GetComponent<RectTransform>().anchoredPosition = new Vector2(-105 * Ratio, -5);
        if (InvertValuesRect != null)
        {
            // Its own row under the type buttons, as wide as the right hand
            // column and flush with it: the type buttons and the fields are
            // anchored top right with a right hand pivot at x = -5, so the
            // same x keeps every right edge on one line.
            InvertValuesRect.sizeDelta = new Vector2(200 * Ratio, 30);
            InvertValuesRect.anchoredPosition = new Vector2(-5, -40);
        }
    }
    public void OnUiWidthChange()
    {
        if (UiWidth != ViewRect.rect.width)
        {
            RefreshUiWidth();
        }
    }
    public void SetTexts()
    {
        LabelText.text = LimLanguageManager.TextDict["Component_Motion_Label"];
        TimingText.text = LimLanguageManager.TextDict["Component_Motion_Timing"];
        DurationText.text = LimLanguageManager.TextDict["Component_Motion_Duration"];
        CfmiText.text = LimLanguageManager.TextDict["Component_Motion_Cfmi"];
        Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0"];
        Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1"];
        TypeText.text = LimLanguageManager.TextDict["Component_Motion_Type"];
        ManuallyMotionText.text = LimLanguageManager.TextDict["Component_Motion_ManuallyMotion"];
        // Built in Start, so a language change arriving first finds it null.
        if (InvertValuesText != null) InvertValuesText.text = LimLanguageManager.TextDict["Component_Motion_InvertValues"];
    }
    public void Fold()
    {
        if (isFolded)
        {
            ViewRect.sizeDelta = new Vector2(0, UnFoldHeight); isFolded = false;
        }
        else if (!isFolded)
        {
            ViewRect.sizeDelta = new Vector2(0, 0); isFolded = true;
        }
        ComponentRect.sizeDelta = new Vector2(0, ViewRect.sizeDelta.y - ViewRect.anchoredPosition.y);
        InspectorManager.ArrangeComponentsUi();
    }
    public void SetMode(Lanotalium.Editor.ComponentMotionMode Mode, int Index = -1)
    {
        EnableValueChange = false;
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Idle || Index == -1)
        {
            gameObject.SetActive(false);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            gameObject.SetActive(true);
            if (OperationManager.TunerManager.CameraManager.Horizontal[Index].Type == 8)
            {
                Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0_Tp8"];
                Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1_Tp8"];
                Type8.interactable = false;
                Type11.interactable = true;
            }
            else if (OperationManager.TunerManager.CameraManager.Horizontal[Index].Type == 11)
            {
                Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0_Tp11"];
                Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1_Tp11"];
                Type8.interactable = true;
                Type11.interactable = false;
            }
            Timing.text = OperationManager.TunerManager.CameraManager.Horizontal[Index].Time.ToString("f5");
            Duration.text = OperationManager.TunerManager.CameraManager.Horizontal[Index].Duration.ToString("f5");
            Cfmi.text = OperationManager.TunerManager.CameraManager.Horizontal[Index].cfmi.ToString();
            Ctp0.text = OperationManager.TunerManager.CameraManager.Horizontal[Index].ctp.ToString("f5");
            Ctp1.text = OperationManager.TunerManager.CameraManager.Horizontal[Index].ctp1.ToString("f5");
            Timing.interactable = true;
            Ctp0.interactable = true;
            Ctp1.interactable = true;
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Vertical)
        {
            gameObject.SetActive(true);
            Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0_Tp10"];
            Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1_Tp10"];
            Timing.text = OperationManager.TunerManager.CameraManager.Vertical[Index].Time.ToString("f5");
            Duration.text = OperationManager.TunerManager.CameraManager.Vertical[Index].Duration.ToString("f5");
            Cfmi.text = OperationManager.TunerManager.CameraManager.Vertical[Index].cfmi.ToString();
            Ctp0.text = OperationManager.TunerManager.CameraManager.Vertical[Index].ctp.ToString("f5");
            Timing.interactable = true;
            Ctp0.interactable = true;
            Ctp1.interactable = false;
            Type8.interactable = false;
            Type11.interactable = false;
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Rotation)
        {
            gameObject.SetActive(true);
            Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0_Tp13"];
            Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1_Tp13"];
            Timing.text = OperationManager.TunerManager.CameraManager.Rotation[Index].Time.ToString("f5");
            Duration.text = OperationManager.TunerManager.CameraManager.Rotation[Index].Duration.ToString("f5");
            Cfmi.text = OperationManager.TunerManager.CameraManager.Rotation[Index].cfmi.ToString();
            Ctp0.text = OperationManager.TunerManager.CameraManager.Rotation[Index].ctp.ToString("f5");
            Timing.interactable = true;
            Ctp0.interactable = true;
            Ctp1.interactable = false;
            Type8.interactable = false;
            Type11.interactable = false;
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Transparency)
        {
            // Timing, Duration, Ease and the transparency itself; the second
            // value means nothing to a fade and stays shut, as it does for
            // Vertical and Rotation.
            gameObject.SetActive(true);
            Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0_Tp14"];
            Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1_Tp14"];
            Timing.text = OperationManager.TunerManager.CameraManager.Transparency[Index].Time.ToString("f5");
            Duration.text = OperationManager.TunerManager.CameraManager.Transparency[Index].Duration.ToString("f5");
            Cfmi.text = OperationManager.TunerManager.CameraManager.Transparency[Index].cfmi.ToString();
            Ctp0.text = OperationManager.TunerManager.CameraManager.Transparency[Index].ctp.ToString("f5");
            Timing.interactable = true;
            Ctp0.interactable = true;
            Ctp1.interactable = false;
            Type8.interactable = false;
            Type11.interactable = false;
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Multiple)
        {
            gameObject.SetActive(true);
            ShowMultipleSelection();
        }
        this.Index = Index;
        this.Mode = Mode;
        EnableValueChange = true;
    }
    /// <summary>
    /// What several selected motions look like in one panel.
    ///
    /// A field shows the value when every selected motion agrees on it and a
    /// dash when they do not, and typing in it writes to all of them. Timing
    /// is the exception and stays shut: one timing for several motions would
    /// pile them on top of each other.
    ///
    /// The labels follow the selection: all of one type and they read as that
    /// type does, mixed types and they fall back to Ctp0 and Ctp1.
    /// </summary>
    private void ShowMultipleSelection()
    {
        List<Lanotalium.Chart.LanotaCameraBase> Selection = OperationManager.SelectedMotions;
        int SharedType = -1;
        bool SameType = true;
        bool AnyHorizontal = false;
        foreach (Lanotalium.Chart.LanotaCameraBase Motion in Selection)
        {
            if (SharedType == -1) SharedType = Motion.Type;
            else if (SharedType != Motion.Type) SameType = false;
            if (Motion.Type == 8 || Motion.Type == 11) AnyHorizontal = true;
        }

        // Guarded: the lookup throws on a key it does not have, and a motion
        // of some unexpected type would take the whole panel down with it.
        string Ctp0Key = "Component_Motion_Ctp0_Tp" + SharedType;
        string Ctp1Key = "Component_Motion_Ctp1_Tp" + SharedType;
        if (SameType && SharedType != -1 && LimLanguageManager.TextDict.ContainsKey(Ctp0Key) && LimLanguageManager.TextDict.ContainsKey(Ctp1Key))
        {
            Ctp0Text.text = LimLanguageManager.TextDict[Ctp0Key];
            Ctp1Text.text = LimLanguageManager.TextDict[Ctp1Key];
        }
        else
        {
            Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0"];
            Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1"];
        }

        Timing.text = " - ";
        Duration.text = SharedDuration(Selection);
        Cfmi.text = SharedEase(Selection);
        Ctp0.text = SharedCtp0(Selection);
        Ctp1.text = AnyHorizontal ? SharedCtp1(Selection) : " - ";

        Timing.interactable = false;
        Duration.interactable = true;
        Cfmi.interactable = true;
        Ctp0.interactable = true;
        Ctp1.interactable = AnyHorizontal;
        Type8.interactable = false;
        Type11.interactable = false;
    }

    private static string SharedDuration(List<Lanotalium.Chart.LanotaCameraBase> Selection)
    {
        float First = 0; bool Started = false;
        foreach (Lanotalium.Chart.LanotaCameraBase Motion in Selection)
        {
            if (!Started) { First = Motion.Duration; Started = true; }
            else if (Motion.Duration != First) return " - ";
        }
        return Started ? First.ToString("f5") : " - ";
    }

    private static string SharedEase(List<Lanotalium.Chart.LanotaCameraBase> Selection)
    {
        int First = 0; bool Started = false;
        foreach (Lanotalium.Chart.LanotaCameraBase Motion in Selection)
        {
            if (!Started) { First = Motion.cfmi; Started = true; }
            else if (Motion.cfmi != First) return " - ";
        }
        return Started ? First.ToString() : " - ";
    }

    private static string SharedCtp0(List<Lanotalium.Chart.LanotaCameraBase> Selection)
    {
        float First = 0; bool Started = false;
        foreach (Lanotalium.Chart.LanotaCameraBase Motion in Selection)
        {
            if (!Started) { First = Motion.ctp; Started = true; }
            else if (Motion.ctp != First) return " - ";
        }
        return Started ? First.ToString("f5") : " - ";
    }

    private static string SharedCtp1(List<Lanotalium.Chart.LanotaCameraBase> Selection)
    {
        float First = 0; bool Started = false;
        foreach (Lanotalium.Chart.LanotaCameraBase Motion in Selection)
        {
            if (Motion.Type != 8 && Motion.Type != 11) continue;
            if (!Started) { First = Motion.ctp1; Started = true; }
            else if (Motion.ctp1 != First) return " - ";
        }
        return Started ? First.ToString("f5") : " - ";
    }

    public void DeleteCurrentSelected()
    {
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            Destroy(OperationManager.TunerManager.CameraManager.Horizontal[Index].TimeLineGameObject);
            OperationManager.TunerManager.CameraManager.Horizontal.RemoveAt(Index);
            SetMode(Lanotalium.Editor.ComponentMotionMode.Idle);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Vertical)
        {
            Destroy(OperationManager.TunerManager.CameraManager.Vertical[Index].TimeLineGameObject);
            OperationManager.TunerManager.CameraManager.Vertical.RemoveAt(Index);
            SetMode(Lanotalium.Editor.ComponentMotionMode.Idle);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Rotation)
        {
            Destroy(OperationManager.TunerManager.CameraManager.Rotation[Index].TimeLineGameObject);
            OperationManager.TunerManager.CameraManager.Rotation.RemoveAt(Index);
            SetMode(Lanotalium.Editor.ComponentMotionMode.Idle);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Transparency)
        {
            Destroy(OperationManager.TunerManager.CameraManager.Transparency[Index].TimeLineGameObject);
            OperationManager.TunerManager.CameraManager.Transparency.RemoveAt(Index);
            SetMode(Lanotalium.Editor.ComponentMotionMode.Idle);
        }
    }
    public void SetTypeTo8()
    {
        if (!EnableValueChange) return;
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            OperationManager.SetHorizontalType(OperationManager.TunerManager.CameraManager.Horizontal[Index], 8);
            Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0_Tp8"];
            Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1_Tp8"];
        }
        Type8.interactable = false;
        Type11.interactable = true;
    }
    public void SetTypeTo11()
    {
        if (!EnableValueChange) return;
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            OperationManager.SetHorizontalType(OperationManager.TunerManager.CameraManager.Horizontal[Index], 11);
            Ctp0Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp0_Tp11"];
            Ctp1Text.text = LimLanguageManager.TextDict["Component_Motion_Ctp1_Tp11"];
        }
        Type8.interactable = true;
        Type11.interactable = false;
    }
    public void OnTimingChange()
    {
        if (!EnableValueChange) return;
        float TimingTmp;
        if (!LimNumber.TryParseFloat(Timing.text, out TimingTmp))
        {
            TimingImg.color = InvalidColor;
            return;
        }
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            if (!OperationManager.CheckHorizontalTimeValid(OperationManager.TunerManager.CameraManager.Horizontal[Index], TimingTmp))
            {
                TimingImg.color = InvalidColor;
                return;
            }
            OperationManager.SetHorizontalTime(OperationManager.TunerManager.CameraManager.Horizontal[Index], TimingTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Vertical)
        {
            if (!OperationManager.CheckVerticalTimeValid(OperationManager.TunerManager.CameraManager.Vertical[Index], TimingTmp))
            {
                TimingImg.color = InvalidColor;
                return;
            }
            OperationManager.SetVerticalTime(OperationManager.TunerManager.CameraManager.Vertical[Index], TimingTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Rotation)
        {
            if (!OperationManager.CheckRotationTimeValid(OperationManager.TunerManager.CameraManager.Rotation[Index], TimingTmp))
            {
                TimingImg.color = InvalidColor;
                return;
            }
            OperationManager.SetRotationTime(OperationManager.TunerManager.CameraManager.Rotation[Index], TimingTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Transparency)
        {
            if (!OperationManager.CheckTransparencyTimeValid(OperationManager.TunerManager.CameraManager.Transparency[Index], TimingTmp))
            {
                TimingImg.color = InvalidColor;
                return;
            }
            OperationManager.SetTransparencyTime(OperationManager.TunerManager.CameraManager.Transparency[Index], TimingTmp);
        }
        TimingImg.color = ValidColor;
    }
    public void OnDurationChange()
    {
        if (!EnableValueChange) return;
        float DurationTmp;
        if (!LimNumber.TryParseFloat(Duration.text, out DurationTmp))
        {
            DurationImg.color = InvalidColor;
            return;
        }
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            if (!OperationManager.CheckHorizontalDurationValid(OperationManager.TunerManager.CameraManager.Horizontal[Index], DurationTmp))
            {
                DurationImg.color = InvalidColor;
                return;
            }
            OperationManager.SetHorizontalDuration(OperationManager.TunerManager.CameraManager.Horizontal[Index], DurationTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Vertical)
        {
            if (!OperationManager.CheckVerticalDurationValid(OperationManager.TunerManager.CameraManager.Vertical[Index], DurationTmp))
            {
                DurationImg.color = InvalidColor;
                return;
            }
            OperationManager.SetVerticalDuration(OperationManager.TunerManager.CameraManager.Vertical[Index], DurationTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Rotation)
        {
            if (!OperationManager.CheckRotationDurationValid(OperationManager.TunerManager.CameraManager.Rotation[Index], DurationTmp))
            {
                DurationImg.color = InvalidColor;
                return;
            }
            OperationManager.SetRotationDuration(OperationManager.TunerManager.CameraManager.Rotation[Index], DurationTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Transparency)
        {
            if (!OperationManager.CheckTransparencyDurationValid(OperationManager.TunerManager.CameraManager.Transparency[Index], DurationTmp))
            {
                DurationImg.color = InvalidColor;
                return;
            }
            OperationManager.SetTransparencyDuration(OperationManager.TunerManager.CameraManager.Transparency[Index], DurationTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Multiple)
        {
            foreach (Lanotalium.Chart.LanotaCameraBase Base in OperationManager.SelectedMotions)
            {
                switch (Base.Type)
                {
                    case 8:
                    case 11:
                        Lanotalium.Chart.LanotaCameraXZ XZ = Base as Lanotalium.Chart.LanotaCameraXZ;
                        if (!OperationManager.CheckHorizontalDurationValid(XZ, DurationTmp))
                        {
                            DurationImg.color = InvalidColor;
                            return;
                        }
                        OperationManager.SetHorizontalDuration(XZ, DurationTmp);
                        break;
                    case 10:
                        Lanotalium.Chart.LanotaCameraY Y = Base as Lanotalium.Chart.LanotaCameraY;
                        if (!OperationManager.CheckVerticalDurationValid(Y, DurationTmp))
                        {
                            DurationImg.color = InvalidColor;
                            return;
                        }
                        OperationManager.SetVerticalDuration(Y, DurationTmp);
                        break;
                    case 13:
                        Lanotalium.Chart.LanotaCameraRot Rot = Base as Lanotalium.Chart.LanotaCameraRot;
                        if (!OperationManager.CheckRotationDurationValid(Rot, DurationTmp))
                        {
                            DurationImg.color = InvalidColor;
                            return;
                        }
                        OperationManager.SetRotationDuration(Rot, DurationTmp);
                        break;
                    case 14:
                        Lanotalium.Chart.LanotaCameraTrs Trs = Base as Lanotalium.Chart.LanotaCameraTrs;
                        if (!OperationManager.CheckTransparencyDurationValid(Trs, DurationTmp))
                        {
                            DurationImg.color = InvalidColor;
                            return;
                        }
                        OperationManager.SetTransparencyDuration(Trs, DurationTmp);
                        break;
                }
            }
        }
        DurationImg.color = ValidColor;
    }
    public void OnEaseChange()
    {
        if (!EnableValueChange) return;
        int EaseTmp;
        if (!int.TryParse(Cfmi.text, out EaseTmp))
        {
            CfmiImg.color = InvalidColor;
            return;
        }
        if (EaseTmp < 0 || EaseTmp > 12)
        {
            CfmiImg.color = InvalidColor;
            return;
        }
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            OperationManager.SetHorizontalEase(OperationManager.TunerManager.CameraManager.Horizontal[Index], EaseTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Vertical)
        {
            OperationManager.SetVerticalEase(OperationManager.TunerManager.CameraManager.Vertical[Index], EaseTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Rotation)
        {
            OperationManager.SetRotationEase(OperationManager.TunerManager.CameraManager.Rotation[Index], EaseTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Transparency)
        {
            OperationManager.SetTransparencyEase(OperationManager.TunerManager.CameraManager.Transparency[Index], EaseTmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Multiple)
        {
            foreach (Lanotalium.Chart.LanotaCameraBase Motion in OperationManager.SelectedMotions) Motion.cfmi = EaseTmp;
        }
        CfmiImg.color = ValidColor;
    }
    public void OnCtp0Change()
    {
        if (!EnableValueChange) return;
        float Ctp0Tmp;
        if (!LimNumber.TryParseFloat(Ctp0.text, out Ctp0Tmp))
        {
            Ctp0Img.color = InvalidColor;
            return;
        }
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            OperationManager.SetHorizontalDegree(OperationManager.TunerManager.CameraManager.Horizontal[Index], Ctp0Tmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Vertical)
        {
            OperationManager.SetVerticalHeight(OperationManager.TunerManager.CameraManager.Vertical[Index], Ctp0Tmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Rotation)
        {
            OperationManager.SetRotationDegree(OperationManager.TunerManager.CameraManager.Rotation[Index], Ctp0Tmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Transparency)
        {
            // Outside 0 to 100 is refused rather than quietly clamped, so
            // what the field shows is always what the motion holds.
            if (Ctp0Tmp < 0 || Ctp0Tmp > LimCameraManager.OpaqueTransparency)
            {
                Ctp0Img.color = InvalidColor;
                return;
            }
            OperationManager.SetTransparencyValue(OperationManager.TunerManager.CameraManager.Transparency[Index], Ctp0Tmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Multiple)
        {
            // Degree, height, rotation or transparency depending on the
            // motion, which is what the one field means for each of them.
            // A transparency is kept inside its 0 to 100.
            foreach (Lanotalium.Chart.LanotaCameraBase Motion in OperationManager.SelectedMotions)
                Motion.ctp = Motion.Type == 14 ? Mathf.Clamp(Ctp0Tmp, 0, LimCameraManager.OpaqueTransparency) : Ctp0Tmp;
        }
        Ctp0Img.color = ValidColor;
    }
    public void OnCtp1Change()
    {
        if (!EnableValueChange) return;
        float Ctp1Tmp;
        if (!LimNumber.TryParseFloat(Ctp1.text, out Ctp1Tmp))
        {
            Ctp1Img.color = InvalidColor;
            return;
        }
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal)
        {
            OperationManager.SetHorizontalRadius(OperationManager.TunerManager.CameraManager.Horizontal[Index], Ctp1Tmp);
        }
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Multiple)
        {
            // Only the horizontals have a radius; the others are left alone
            // rather than given a number that means nothing to them.
            foreach (Lanotalium.Chart.LanotaCameraBase Motion in OperationManager.SelectedMotions)
                if (Motion.Type == 8 || Motion.Type == 11) Motion.ctp1 = Ctp1Tmp;
        }
        Ctp1Img.color = ValidColor;
    }
    public void OpenManuallyMotionEditor()
    {
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Multiple) return;
        OperationManager.TunerManager.MediaPlayerManager.IsPlaying = false;
        if (Mode == Lanotalium.Editor.ComponentMotionMode.Horizontal) GizmoMotionManager.Edit(OperationManager.TunerManager.CameraManager.Horizontal[Index]);
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Vertical) GizmoMotionManager.Edit(OperationManager.TunerManager.CameraManager.Vertical[Index]);
        else if (Mode == Lanotalium.Editor.ComponentMotionMode.Rotation) GizmoMotionManager.Edit(OperationManager.TunerManager.CameraManager.Rotation[Index]);
    }
}
