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

    /// <summary>The tool itself, so a keyboard shortcut can reach its dropdowns.</summary>
    public static LimClickToCreateManager Instance;
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

    /// <summary>How far the pointer has to travel before a click becomes a drag.</summary>
    private const float StretchThresholdPixels = 4f;
    /// <summary>A rail drawn backwards keeps at least this much length.</summary>
    private const float MinRailDuration = 0.01f;
    private Lanotalium.Chart.LanotaHoldNote StretchingRail;
    private bool StretchStarted;
    private Vector3 StretchOrigin;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (LimSystem.ChartContainer == null) return;
        SnapToBeatline = isAttachToBeatline;
        SnapToAngleline = isAttachToAngleline;
        SharedAnglelineManager = AnglelineManager;
        SharedGhostParent = NoteCurserTransform;
        UpdateNoteCurserTransformAndDetectCreate();
        UpdateRailStretch();
        UpdatePointerInfo();
        DetectHotkeys();
    }

    /// <summary>
    /// C switches click-to-create on and off. Switching it on also switches
    /// on both Attach To toggles, which is how notes are placed nearly all of
    /// the time; switching it off leaves them as they are, so turning one of
    /// them off by hand only lasts until the next time C is pressed.
    ///
    /// Not while typing, and not with Ctrl held down, which is the copy
    /// shortcut.
    /// </summary>
    private void DetectHotkeys()
    {
        if (!Input.GetKeyDown(KeyCode.C)) return;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return;
        if (IsTypingInTextField()) return;

        Enable = !Enable;
        if (Enable)
        {
            AttachToBeatline = true;
            AttachToAngleline = true;
        }
    }
    private static bool IsTypingInTextField()
    {
        EventSystem Events = EventSystem.current;
        if (Events == null || Events.currentSelectedGameObject == null) return false;
        return Events.currentSelectedGameObject.GetComponent<InputField>() != null;
    }
    private void OnDisable()
    {
        Enable = false;
        IsCreating = false;
    }
    /// <summary>
    /// The number row picks the kind of note the next click will leave
    /// behind: 1 Click, 2 Flick In, 3 Flick Out, 4 Catch, 5 Rail, which is
    /// the order the list is in. Handled in LimOperationManagerNoteKeys,
    /// where the same keys resize a selection while this tool is off.
    /// </summary>
    public void SetTypeByNumber(int Number)
    {
        if (TypeDropdown == null) return;
        int Value = Mathf.Clamp(Number - 1, 0, TypeDropdown.options.Count - 1);
        if (TypeDropdown.value != Value) TypeDropdown.value = Value;
        TypeDropdown.RefreshShownValue();
        // The cursor on the ring shows what would be created, so it is made
        // again whether or not the list decided the value had changed.
        if (Enable) InstantiateNoteCurser();
    }

    /// <summary>
    /// Shift and the number row pick how big the next note will be: 1 is
    /// size 0, the one Lanota gives a note by default, and 4 is size 3, so
    /// the four keys read in the order the list does. Handled in
    /// LimOperationManagerNoteKeys along with the type keys.
    /// </summary>
    public void SetSizeByNumber(int Number)
    {
        if (SizeDropdown == null) return;
        int Value = Mathf.Clamp(Number - 1, 0, SizeDropdown.options.Count - 1);
        if (SizeDropdown.value != Value) SizeDropdown.value = Value;
        SizeDropdown.RefreshShownValue();
        // The cursor on the ring is the note that would be created, so it is
        // made again whether or not the list decided the value had changed.
        if (Enable) InstantiateNoteCurser();
    }

    /// <summary>
    /// The list used to read 0, 2, 3, 4, 5, which are the numbers Lanota
    /// gives the five kinds of note in its own files. On screen that leading
    /// zero only made the order look wrong, so the list is numbered 1 to 5
    /// here, the same numbers the keyboard shortcuts use. The type written
    /// into the chart is worked out by ConvertValueToType and has not
    /// changed. Kept to bare digits because the box is eighty pixels wide.
    /// </summary>
    private void RemakeTypeDropdown()
    {
        if (TypeDropdown == null) return;
        int Value = TypeDropdown.value;
        TypeDropdown.options = new List<Dropdown.OptionData>
        {
            new Dropdown.OptionData("1"),
            new Dropdown.OptionData("2"),
            new Dropdown.OptionData("3"),
            new Dropdown.OptionData("4"),
            new Dropdown.OptionData("5")
        };
        TypeDropdown.value = Mathf.Clamp(Value, 0, TypeDropdown.options.Count - 1);
        TypeDropdown.RefreshShownValue();
    }

    public void SetTexts()
    {
        RemakeTypeDropdown();
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
        // Ctrl and the left button drag the tuner around; that click must not
        // leave a note behind on the way.
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return;
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
        // The cursor's degree has the camera's whole accumulated rotation
        // taken off it, which can be thousands of degrees by this point in a
        // chart. The note keeps the place on the circle, written plainly.
        Degree = LimMathUtil.NormalizeDegree(Degree);
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
            New.Group = LimTimeGroups.ActiveGroup;
            OperationManager.AddHoldNote(New);
            BeginRailStretch(New);
        }
        else
        {
            Lanotalium.Chart.LanotaTapNote New = new Lanotalium.Chart.LanotaTapNote();
            New.Type = Type;
            New.Time = Time;
            New.Degree = Degree;
            New.Size = Size;
            New.Group = LimTimeGroups.ActiveGroup;
            OperationManager.AddTapNote(New);
        }
    }

    /// <summary>
    /// Drawing a rail out with the button still held down.
    ///
    /// The click leaves the head where it was clicked and the end of the rail
    /// follows the pointer until the button comes up: away from the middle it
    /// grows longer, round the ring it leans, and the two together draw a rail
    /// running diagonally to wherever it is let go. Letting go without moving
    /// leaves the one second a rail has always been created with, which is
    /// what a plain click used to do and still does.
    ///
    /// Both ends snap the way a note being placed does, by the same two
    /// Attach To toggles, so the rail lands between the same lines its head
    /// did. It stays a plain rail with no joints until it is actually leant
    /// to one side.
    ///
    /// The stretch is part of creating the note: the single undo entry the
    /// creation already made takes the whole rail away, length, lean and all.
    /// </summary>
    private void BeginRailStretch(Lanotalium.Chart.LanotaHoldNote Rail)
    {
        StretchingRail = Rail;
        StretchStarted = false;
        StretchOrigin = LimMousePosition.MousePosition;
    }

    private void UpdateRailStretch()
    {
        if (StretchingRail == null) return;
        if (!Input.GetMouseButton(0)) { StretchingRail = null; StretchStarted = false; return; }

        if (!StretchStarted)
        {
            // Below this the gesture was a click, not a drag, and the rail
            // keeps the length it was created with.
            if (Vector3.Distance(LimMousePosition.MousePosition, StretchOrigin) < StretchThresholdPixels) return;
            StretchStarted = true;
        }

        float Time, Degree;
        if (!LimTunerCoordinate.TryGetChartPointAtMouse(TunerWindowRect, TunerCamera, TunerManager, out Time, out Degree)) return;
        Time = CalculateAttachToBeatlineTime(Time);
        Degree = CalculateAttachToAnglelineDegree(Degree);

        // The pointer's degree is the one on screen, which carries the whole
        // rotation the camera has turned through; the rail is written in the
        // chart's own. Measured as a turn away from the head so that a rail
        // drawn across 0 leans the short way round rather than the long way.
        float Chart = Degree - TunerManager.CameraManager.CurrentRotation;
        float End = StretchingRail.Degree + Mathf.DeltaAngle(StretchingRail.Degree, Chart);
        OperationManager.StretchRailEnd(StretchingRail, Mathf.Max(Time, StretchingRail.Time + MinRailDuration), End);
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
