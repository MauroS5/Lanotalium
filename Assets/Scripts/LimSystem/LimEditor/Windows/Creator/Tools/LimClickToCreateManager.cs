using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LimClickToCreateManager : MonoBehaviour
{
    public RectTransform TunerWindowRect;
    public LimCreatorToolBase ToolBase;
    public LimTapNoteManager TapNoteManager;
    public LimHoldNoteManager HoldNoteManager;
    public LimTunerManager TunerManager;
    public ComponentBpmManager ComponentBpm;
    public LimAngleLineManager AnglelineManager;
    public LimOperationManager OperationManager;
    public Transform NoteCurserTransform;
    public Dropdown TypeDropdown, SizeDropdown;
    public Text EnableText, SizeText, TypeText, AttachToText, BeatlineText, AnglelineText;
    public Color PressedColor, UnPressedColor;
    public Image EnableImg, AttachBeatImg, AttachAngleImg;
    public Camera TunerCamera;
    public RectTransform PointerInfo;
    public Text PointerInfoText;

    public bool Enable
    {
        get
        {
            return isEnable;
        }
        set
        {
            if (TunerManager.isInitialized == false) return;
            isEnable = value;
            IsCreating = value;
            if (value)
            {
                EnableImg.color = PressedColor;
                InstantiateNoteCurser();
                EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                EnableImg.color = UnPressedColor;
                Destroy(NoteCurser);
            }
        }
    }
    public bool AttachToBeatline
    {
        get
        {
            return isAttachToBeatline;
        }
        set
        {
            isAttachToBeatline = value;
            if (value)
            {
                AttachBeatImg.color = PressedColor;
            }
            else
            {
                AttachBeatImg.color = UnPressedColor;
            }
        }
    }
    public bool AttachToAngleline
    {
        get
        {
            return isAttachToAngleline;
        }
        set
        {
            isAttachToAngleline = value;
            if (value)
            {
                AttachAngleImg.color = PressedColor;
            }
            else
            {
                AttachAngleImg.color = UnPressedColor;
            }
        }
    }

    /// <summary>True while click-to-create owns the left mouse button.</summary>
    public static bool IsCreating;
    /// <summary>Attach-to-beatline toggle, readable while dragging notes.</summary>
    public static bool SnapToBeatline;
    /// <summary>Attach-to-angleline toggle, readable while dragging notes.</summary>
    public static bool SnapToAngleline;
    /// <summary>The angleline tool, shared so dragging can snap to its lines.</summary>
    public static LimAngleLineManager SharedAnglelineManager;
    /// <summary>Parent transform used for on-ring preview objects.</summary>
    public static Transform SharedGhostParent;
    private GameObject NoteCurser;
    private bool isEnable, isAttachToBeatline, isAttachToAngleline;
    private float NoteCursorTiming, NoteCursorDegree;

    private void Update()
    {
        if (LimSystem.ChartContainer == null) return;
        SnapToBeatline = isAttachToBeatline;
        SnapToAngleline = isAttachToAngleline;
        SharedAnglelineManager = AnglelineManager;
        SharedGhostParent = NoteCurserTransform;
        UpdateNoteCurserTransformAndDetectCreate();
        UpdatePointerInfo();
        //DetectHotkeys();
    }
    private void DetectHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.F2)) Enable = !Enable;
    }
    private void OnDisable()
    {
        Enable = false;
        IsCreating = false;
    }
    public void SetTexts()
    {
        EnableText.text = LimLanguageManager.TextDict["ClickToCreate_Enable"];
        SizeText.text = LimLanguageManager.TextDict["ClickToCreate_Size"];
        TypeText.text = LimLanguageManager.TextDict["ClickToCreate_Type"];
        AttachToText.text = LimLanguageManager.TextDict["ClickToCreate_AttachTo"];
        BeatlineText.text = LimLanguageManager.TextDict["ClickToCreate_Beatline"];
        AnglelineText.text = LimLanguageManager.TextDict["ClickToCreate_Angleline"];
    }
    private int ConvertValueToType(int Value)
    {
        return Value == 0 ? 0 : Value + 1;
    }
    private int ConvertTypeToValue(int Type)
    {
        return Type == 0 ? 0 : Type - 1;
    }
    private GameObject GetCorrectNotePrefab()
    {
        int Type = ConvertValueToType(TypeDropdown.value);
        int Size = SizeDropdown.value;
        if (Type == 5) return HoldNoteManager.GetPrefab(Size, false);
        else return TapNoteManager.GetPrefab(Type, Size, false);
    }
    public void InstantiateNoteCurser()
    {
        if (NoteCurser != null) Destroy(NoteCurser);
        NoteCurser = Instantiate(GetCorrectNotePrefab(), NoteCurserTransform);
        NoteCurser.GetComponentInChildren<SpriteRenderer>().sortingLayerName = "ClickToCreate";
        NoteCurser.SetActive(false);
    }

    private float CalculateMovePercent(float Time)
    {
        return LimTunerCoordinate.TimeToMovePercent(Time, TunerManager);
    }
    private float CalculateEasedPercent(float Percent)
    {
        return LimTunerCoordinate.EasedPercent(Percent);
    }
    private float CalculateUnEasedPercent(float Percent)
    {
        return LimTunerCoordinate.UnEasedPercent(Percent);
    }
    private float CalculateCurserTime(float Percent)
    {
        return LimTunerCoordinate.PercentToTime(Percent, TunerManager);
    }
    private float CalculateCurserDegree(Vector3 Position)
    {
        return LimTunerCoordinate.ScreenDegree(Position);
    }
    private float CalculateAttachToBeatlineTime(float Time)
    {
        if (!AttachToBeatline) return Time;
        return OperationManager.FindAttachToBeatlineByTime(Time, 0.05f);
    }
    private float CalculateAttachToAnglelineDegree(float Degree)
    {
        if (!AttachToAngleline) return Degree;
        return AnglelineManager.FindAttachToAnglelineByDegree(Degree, 5);
    }

    private bool UpdateNoteCurserActive(float Distance)
    {
        if (Distance > 10 || Distance < 2)
        {
            if (NoteCurser.activeInHierarchy) NoteCurser.SetActive(false);
            return false;
        }
        else
        {
            if (!NoteCurser.activeInHierarchy) NoteCurser.SetActive(true);
            return true;
        }
    }
    private void UpdateNoteCurserTransformAndDetectCreate()
    {
        if (NoteCurser == null) return;
        Vector3 MousePosition = LimMousePosition.MousePosition;
        Vector3 TunerPosition = new Vector3
        {
            x = MousePosition.x - TunerWindowRect.anchoredPosition.x,
            y = TunerWindowRect.sizeDelta.y + (MousePosition.y - TunerWindowRect.anchoredPosition.y)
        };
        Vector3 Position = TunerCamera.ScreenToWorldPoint(new Vector3(TunerPosition.x, TunerPosition.y, -TunerCamera.transform.position.y));
        float Distance = Vector3.Distance(Position, new Vector3());
        if (!UpdateNoteCurserActive(Distance)) return;
        float UnEasedPercent = CalculateUnEasedPercent(Distance / 10) * 100;
        float Time = CalculateCurserTime(UnEasedPercent);
        if (float.IsNaN(Time))
        {
            if (NoteCurser.activeInHierarchy) NoteCurser.SetActive(false);
            return;
        }
        Time = CalculateAttachToBeatlineTime(Time);
        float Degree = CalculateCurserDegree(Position);
        Degree = CalculateAttachToAnglelineDegree(Degree);
        float Percent = CalculateEasedPercent(CalculateMovePercent(Time));
        NoteCurser.transform.rotation = Quaternion.Euler(new Vector3(90, Degree, 0));
        NoteCurser.transform.position = new Vector3(-Percent / 10 * Mathf.Sin(Degree * Mathf.Deg2Rad), 0, -Percent / 10 * Mathf.Cos(Degree * Mathf.Deg2Rad));
        NoteCurser.transform.localScale = new Vector3(Percent / 100, Percent / 100, 0);
        NoteCursorTiming = Time;
        NoteCursorDegree = Degree - TunerManager.CameraManager.CurrentRotation;
        if (Distance < 10) if (Input.GetMouseButtonDown(0) && LimMousePosition.IsMouseOverWindow(TunerWindowRect)) CreateNoteAtCurser(NoteCursorTiming, NoteCursorDegree);
    }
    private Vector2 CalculatePointerInfoPosition(Vector2 MousePosition)
    {
        Vector2 InfoPosition = MousePosition;
        if (MousePosition.x >= PointerInfo.sizeDelta.x) InfoPosition.x -= PointerInfo.sizeDelta.x;
        if (MousePosition.y < -PointerInfo.sizeDelta.y) InfoPosition.y += PointerInfo.sizeDelta.y;
        return InfoPosition;
    }
    private string DeltaTimeBetweenSelected()
    {
        if (OperationManager.SelectedTapNote.Count + OperationManager.SelectedHoldNote.Count != 1) return "-";
        if (OperationManager.SelectedTapNote.Count == 1) return (NoteCursorTiming - OperationManager.SelectedTapNote[0].Time).ToString();
        if (OperationManager.SelectedHoldNote.Count == 1) return (NoteCursorTiming - OperationManager.SelectedHoldNote[0].Time).ToString();
        return "-";
    }
    private float ClampedDegree(float Degree)
    {
        while (Degree > 360) Degree -= 360;
        while (Degree < 0) Degree += 360;
        return Degree;
    }
    private void UpdatePointerInfo()
    {
        if (NoteCurser == null) { if (PointerInfo.gameObject.activeInHierarchy) PointerInfo.gameObject.SetActive(false); return; }
        if (!NoteCurser.activeInHierarchy) { if (PointerInfo.gameObject.activeInHierarchy) PointerInfo.gameObject.SetActive(false); return; }
        if (!PointerInfo.gameObject.activeInHierarchy) PointerInfo.gameObject.SetActive(true);
        Vector2 MouseInTunerWindow = LimMousePosition.MousePositionInWindow(TunerWindowRect);
        PointerInfo.anchoredPosition = CalculatePointerInfoPosition(MouseInTunerWindow);
        PointerInfoText.text = string.Format("{0} : {1}\n{2} : {3}\n{4} : {5}", LimLanguageManager.TextDict["ClickToCreate_Timing"], NoteCursorTiming, LimLanguageManager.TextDict["ClickToCreate_Degree"],
            ClampedDegree(NoteCursorDegree), LimLanguageManager.TextDict["ClickToCreate_Deltatime"], DeltaTimeBetweenSelected());
    }
    private void CreateNoteAtCurser(float Time, float Degree)
    {
        if (!Enable) return;
        // A paste preview owns the left button while it is on screen.
        if (LimOperationManager.Instance != null && LimOperationManager.Instance.IsPasting) return;
        int Type = ConvertValueToType(TypeDropdown.value);
        int Size = SizeDropdown.value;
        if (Type == 5)
        {
            Lanotalium.Chart.LanotaHoldNote New = new Lanotalium.Chart.LanotaHoldNote();
            New.Type = 5;
            New.Duration = 1;
            New.Degree = Degree;
            New.Time = Time;
            New.Size = Size;
            OperationManager.AddHoldNote(New);
        }
        else
        {
            Lanotalium.Chart.LanotaTapNote New = new Lanotalium.Chart.LanotaTapNote();
            New.Type = Type;
            New.Time = Time;
            New.Degree = Degree;
            New.Size = Size;
            OperationManager.AddTapNote(New);
        }
    }

    public void OnEnableClick()
    {
        if (Enable) Enable = false;
        else Enable = true;
    }
    public void OnAttachToBeatlineClick()
    {
        if (AttachToBeatline) AttachToBeatline = false;
        else AttachToBeatline = true;
    }
    public void OnAttachToAnglelineClick()
    {
        if (AttachToAngleline) AttachToAngleline = false;
        else AttachToAngleline = true;
    }
}
