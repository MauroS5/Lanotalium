using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComponentBpmManager : MonoBehaviour
{
    public LimInspectorManager InspectorManager;
    public LimOperationManager OperationManager;
    public LimTunerManager TunerManager;
    public RectTransform ViewRect, ComponentRect;
    public Color InvalidColor, ValidColor, PressedColor, UnPressedColor;
    public GameObject TimeValuePrefab, BeatlinePrefab;
    public GameObject ComponentBpmView;
    public Transform PairsTransform, BeatlineTransform;
    public Text LabelText, TimingText, BpmText, BeatlineText, DensityText, FixSelectedText, FixAllText;
    public List<float> BeatlineTimes = new List<float>();
    public List<GameObject> Beatlines = new List<GameObject>();

    /// <summary>
    /// Which subdivision of its beat each beatline is, running alongside
    /// BeatlineTimes. 0 is the beat itself, the one the metronome claps.
    /// </summary>
    public List<int> BeatlineSubdivisions = new List<int>();

    // Light blue on the beat, green on the half, light orange on the
    // quarters, light pink for anything finer.
    private static readonly Color BeatlineOnBeatColor = new Color(0.45f, 0.75f, 1f);
    private static readonly Color BeatlineHalfColor = new Color(0.45f, 0.9f, 0.55f);
    private static readonly Color BeatlineQuarterColor = new Color(1f, 0.72f, 0.35f);
    private static readonly Color BeatlineFinerColor = new Color(1f, 0.62f, 0.8f);
    private Material BeatlineMaterial;

    /// <summary>Set by the component's own slider; 1 is the look these lines always had.</summary>
    public float LineOpacity = 1f;
    public InputField DensityInputField;
    public Image BeatlineImg, DensityImg;
    public float UnFoldHeight;
    public bool EnableBeatline
    {
        get
        {
            return isBeatlineOpen;
        }
        set
        {
            isBeatlineOpen = value;
            if (value)
            {
                ReCalculateBeatlineTimes();
                BeatlineImg.color = PressedColor;
            }
            else
            {
                GenerateCorrectQuantityBeatline(0);
                BeatlineImg.color = UnPressedColor;
            }
        }
    }
    public float BeatlineDensity
    {
        get
        {
            return Density;
        }
        set
        {
            Density = value;
            ReCalculateBeatlineTimes();
        }
    }

    private bool isFolded = false, isBeatlineOpen = false;
    private float UiWidth, Density = 1;

    private const float OpacityRowHeight = 35f;
    private Slider OpacitySlider;
    private Text OpacityLabel;

    private void Start()
    {
        ComponentRect.sizeDelta = new Vector2(0, ViewRect.sizeDelta.y - ViewRect.anchoredPosition.y);
        CreateOpacityRow();
        RefreshUiWidth();
    }

    /// <summary>
    /// The beatlines' opacity slider, on a row of its own.
    ///
    /// It goes straight under the row of buttons, and the column headers and
    /// the list of bpm entries below it are pushed down to make room: the
    /// component lays its rows out at fixed heights, so a new one has to be
    /// opened up rather than dropped on top.
    /// </summary>
    private void CreateOpacityRow()
    {
        if (DensityInputField == null) return;
        RectTransform Parent = DensityInputField.GetComponent<RectTransform>().parent as RectTransform;
        if (Parent == null) return;

        // The buttons sit on the first row, so the second one is free once
        // everything from there down has moved.
        const float RowY = -35f;
        for (int i = 0; i < Parent.childCount; ++i)
        {
            RectTransform Sibling = Parent.GetChild(i) as RectTransform;
            if (Sibling == null) continue;
            if (Sibling.anchoredPosition.y <= RowY + 0.5f)
                Sibling.anchoredPosition = new Vector2(Sibling.anchoredPosition.x, Sibling.anchoredPosition.y - OpacityRowHeight);
        }

        Font Face = DensityInputField.textComponent != null ? DensityInputField.textComponent.font : null;
        OpacityLabel = LimUiBuilder.CreateLabel(Parent, "OpacityLabel", Face, 14, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft);
        OpacityLabel.rectTransform.anchoredPosition = new Vector2(10, RowY);
        OpacityLabel.rectTransform.sizeDelta = new Vector2(90, 30);

        OpacitySlider = LimUiBuilder.CreateSlider(Parent, "Opacity", DensityInputField.GetComponent<Image>(), 0, 1, LineOpacity);
        RectTransform SliderRect = OpacitySlider.GetComponent<RectTransform>();
        SliderRect.anchorMin = new Vector2(0, 1);
        SliderRect.anchorMax = new Vector2(1, 1);
        SliderRect.pivot = new Vector2(0.5f, 1);
        SliderRect.offsetMin = new Vector2(104, RowY - 23);
        SliderRect.offsetMax = new Vector2(-10, RowY - 8);
        OpacitySlider.onValueChanged.AddListener((float Value) => { LineOpacity = Value; });

        UnFoldHeight += OpacityRowHeight;
        if (!isFolded) ViewRect.sizeDelta = new Vector2(0, UnFoldHeight);
        ComponentRect.sizeDelta = new Vector2(0, ViewRect.sizeDelta.y - ViewRect.anchoredPosition.y);
        if (LimLanguageManager.TextDict != null) SetTexts();
    }

    private void Update()
    {
        OnUiWidthChange();
        UpdateBeatline();
    }
    private void OnDisable()
    {
        EnableBeatline = false;
    }
    public void RefreshUiWidth()
    {
        UiWidth = ViewRect.rect.width;
        float Ratio = UiWidth / 500f;
        TimingText.GetComponent<RectTransform>().anchoredPosition = new Vector2(10 * Ratio, 0);
        TimingText.GetComponent<RectTransform>().sizeDelta = new Vector2(160 * Ratio, 30);
        BpmText.GetComponent<RectTransform>().anchoredPosition = new Vector2(200 * Ratio, 0);
        BpmText.GetComponent<RectTransform>().sizeDelta = new Vector2(160 * Ratio, 30);
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
        LabelText.text = LimLanguageManager.TextDict["Component_Bpm_Label"];
        TimingText.text = LimLanguageManager.TextDict["Component_Bpm_Timing"];
        BpmText.text = LimLanguageManager.TextDict["Component_Bpm_Bpm"];
        BeatlineText.text = LimLanguageManager.TextDict["Component_Bpm_Beatline"];
        DensityText.text = LimLanguageManager.TextDict["Component_Bpm_Density"];
        FixSelectedText.text = LimLanguageManager.TextDict["Component_Bpm_FixSelected"];
        FixAllText.text = LimLanguageManager.TextDict["Component_Bpm_FixAll"];
        // Built in Start, so a language change arriving first finds it null.
        if (OpacityLabel != null) OpacityLabel.text = LimLanguageManager.TextDict["Window_Creator_Opacity"];
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
    public void InstantiateBpmList()
    {
        float Height = 0;
        foreach (Lanotalium.Chart.LanotaChangeBpm Bpm in OperationManager.TunerManager.BpmManager.Bpm)
        {
            if (Bpm.ListGameObject != null) Destroy(Bpm.ListGameObject);
            Bpm.ListGameObject = Instantiate(TimeValuePrefab, PairsTransform);
            Bpm.ListGameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, Height);
            Bpm.ListGameObject.GetComponent<TimeValuePairManager>().OperationManager = OperationManager;
            Bpm.ListGameObject.GetComponent<TimeValuePairManager>().Initialize(Bpm);
            Bpm.InstanceId = Bpm.ListGameObject.GetInstanceID();
            Height -= 30;
        }
        // The opacity row added at startup is part of the fixed height now.
        ViewRect.sizeDelta = new Vector2(0, (OpacitySlider != null ? 70 + OpacityRowHeight : 70) - Height);
        UnFoldHeight = ViewRect.sizeDelta.y;
        ComponentRect.sizeDelta = new Vector2(0, ViewRect.sizeDelta.y - ViewRect.anchoredPosition.y);
        isFolded = false;
        InspectorManager.ArrangeComponentsUi();
    }

    public void StartDetectBpm()
    {

    }
    IEnumerator ManuallyDetectBpmCoroutine()
    {
        yield return null;
    }

    public void OnDensityChange()
    {
        float DensityTmp;
        if (!LimNumber.TryParseFloat(DensityInputField.text, out DensityTmp))
        {
            DensityImg.color = InvalidColor;
            return;
        }
        if (DensityTmp > 16 || DensityTmp < 0)
        {
            DensityImg.color = InvalidColor;
            return;
        }
        BeatlineDensity = DensityTmp;
        DensityImg.color = ValidColor;
    }
    public void OnClickBeatlineBtn()
    {
        if (LimSystem.ChartContainer == null) return;
        if (EnableBeatline == true) EnableBeatline = false;
        else EnableBeatline = true;
    }
    public void ReCalculateBeatlineTimes()
    {
        BeatlineTimes.Clear();
        BeatlineSubdivisions.Clear();
        int Subdivisions = Mathf.Max(1, Mathf.RoundToInt(Density));
        for (int i = 0; i < TunerManager.BpmManager.Bpm.Count; ++i)
        {
            float BpmDeltaTime = (60 / TunerManager.BpmManager.Bpm[i].Bpm) / Density;
            float StartTime = (i == 0 ? 0 : TunerManager.BpmManager.Bpm[i].Time);
            float EndTime = (i == TunerManager.BpmManager.Bpm.Count - 1 ? TunerManager.MediaPlayerManager.Length : TunerManager.BpmManager.Bpm[i + 1].Time);
            // Counted from the start of each bpm section, so the line on the
            // beat is always the one the metronome claps on.
            int Step = 0;
            for (float t = StartTime; t <= EndTime; t += BpmDeltaTime)
            {
                BeatlineTimes.Add(t);
                BeatlineSubdivisions.Add(Step % Subdivisions);
                ++Step;
            }
        }
    }

    /// <summary>
    /// Colour of the line sitting at one subdivision of a beat, so the eye
    /// can read a dense grid: the beat itself, then the half, then the
    /// quarters, then everything finer.
    ///
    /// With a density of 8 a beat reads blue, pink, orange, pink, green,
    /// pink, orange, pink, and the next beat is blue again.
    /// </summary>
    private Color GetBeatlineColor(int Subdivision)
    {
        int Subdivisions = Mathf.Max(1, Mathf.RoundToInt(Density));
        if (Subdivision == 0) return BeatlineOnBeatColor;
        if (Subdivision * 2 == Subdivisions) return BeatlineHalfColor;
        if (Subdivision * 4 == Subdivisions || Subdivision * 4 == Subdivisions * 3) return BeatlineQuarterColor;
        return BeatlineFinerColor;
    }
    public int FindBeatlineTimesPositionByTime(float Time)
    {
        int Index = 0;
        foreach (float t in BeatlineTimes)
        {
            if (t > Time) break;
            Index++;
        }
        return Index;
    }
    private float CalculateMovePercent(float JudgeTime)
    {
        int StartScroll = 0, EndScroll = 0;
        float Percent = 100;
        for (int i = 0; i < TunerManager.ScrollManager.Scroll.Count - 1; ++i)
        {
            if (TunerManager.ChartTime >= TunerManager.ScrollManager.Scroll[i].Time && TunerManager.ChartTime < TunerManager.ScrollManager.Scroll[i + 1].Time) StartScroll = i;
            if (JudgeTime >= TunerManager.ScrollManager.Scroll[i].Time && JudgeTime < TunerManager.ScrollManager.Scroll[i + 1].Time) EndScroll = i;
        }
        if (TunerManager.ScrollManager.Scroll.Count != 0)
        {
            if (TunerManager.ChartTime >= TunerManager.ScrollManager.Scroll[TunerManager.ScrollManager.Scroll.Count - 1].Time) StartScroll = TunerManager.ScrollManager.Scroll.Count - 1;
            if (JudgeTime >= TunerManager.ScrollManager.Scroll[TunerManager.ScrollManager.Scroll.Count - 1].Time) EndScroll = TunerManager.ScrollManager.Scroll.Count - 1;
        }
        for (int i = StartScroll; i <= EndScroll; ++i)
        {
            if (StartScroll == EndScroll) Percent -= (JudgeTime - TunerManager.ChartTime) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
            else if (StartScroll != EndScroll)
            {
                if (i == StartScroll) Percent -= (TunerManager.ScrollManager.Scroll[i + 1].Time - TunerManager.ChartTime) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
                else if (i != EndScroll && i != StartScroll) Percent -= (TunerManager.ScrollManager.Scroll[i + 1].Time - TunerManager.ScrollManager.Scroll[i].Time) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
                else if (i == EndScroll) Percent -= (JudgeTime - TunerManager.ScrollManager.Scroll[i].Time) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
            }
        }
        Percent = Mathf.Clamp(Percent, 0, 100);
        return Percent;
    }
    private float CalculateEasedPercent(float Percent)
    {
        return Mathf.Pow(2, 10 * (Percent / 100 - 1)) * 100;
    }
    private void GenerateCorrectQuantityBeatline(int BeatlineCount)
    {
        int DeltaQuantity = BeatlineCount - Beatlines.Count;
        if (DeltaQuantity == 0) return;
        if (DeltaQuantity < 0)
        {
            for (int i = 0; i > DeltaQuantity; i--)
            {
                Destroy(Beatlines[Beatlines.Count - 1]);
                Beatlines.RemoveAt(Beatlines.Count - 1);
            }
        }
        else if (DeltaQuantity > 0)
        {
            for (int i = 0; i < DeltaQuantity; ++i)
            {
                Beatlines.Add(Instantiate(BeatlinePrefab, BeatlineTransform));
            }
        }
    }
    /// <summary>
    /// The prefab's own material, the one every tint is derived from.
    /// </summary>
    private Material GetBeatlineMaterial()
    {
        if (BeatlineMaterial != null) return BeatlineMaterial;
        if (BeatlinePrefab == null) return null;
        LineRenderer Line = BeatlinePrefab.GetComponent<LineRenderer>();
        if (Line != null) BeatlineMaterial = Line.sharedMaterial;
        return BeatlineMaterial;
    }
    private Vector3[] DrawCircle(float Radius)
    {
        List<Vector3> Points = new List<Vector3>();
        for (int i = 0; i < 360; ++i)
        {
            Points.Add(new Vector3(Radius * Mathf.Cos(i * Mathf.Deg2Rad), 0, Radius * Mathf.Sin(i * Mathf.Deg2Rad)));
        }
        return Points.ToArray();
    }
    public void UpdateBeatline()
    {
        if (!isBeatlineOpen) return;
        float CurrentTime = TunerManager.ChartTime;
        int StartIndex = FindBeatlineTimesPositionByTime(CurrentTime);
        List<float> BeatlinesToDrawPercent = new List<float>();
        for (int i = StartIndex; i < BeatlineTimes.Count; ++i)
        {
            float Percent = CalculateEasedPercent(CalculateMovePercent(BeatlineTimes[i]));
            if (Percent < 20) break;
            BeatlinesToDrawPercent.Add(Percent);
        }
        GenerateCorrectQuantityBeatline(BeatlinesToDrawPercent.Count);
        for (int i = 0; i < BeatlinesToDrawPercent.Count; ++i)
        {
            LineRenderer Line = Beatlines[i].GetComponent<LineRenderer>();
            Line.SetPositions(DrawCircle(BeatlinesToDrawPercent[i] / 10));

            int Index = StartIndex + i;
            int Subdivision = Index < BeatlineSubdivisions.Count ? BeatlineSubdivisions[Index] : 0;
            LimLineColor.Apply(Line, GetBeatlineMaterial(), GetBeatlineColor(Subdivision) * Mathf.Clamp01(LineOpacity));
        }
    }

    public float FindPrevOrNextBeatline(float Time, bool Forward)
    {
        if (Beatlines.Count == 0) return Time;
        int Index = OperationManager.FindNearestBeatlineIndexByTime(Time);
        if (Forward) return BeatlineTimes[Mathf.Clamp(Index + 1, 0, BeatlineTimes.Count - 1)];
        else return BeatlineTimes[Mathf.Clamp(Index - 1, 0, BeatlineTimes.Count - 1)];
    }

    public void FixSelectedNotes()
    {
        if (LimSystem.ChartContainer == null) return;
        ReCalculateBeatlineTimes();
        OperationManager.FixSelectedNotesToBeatline();
    }
    public void FixAllNotes()
    {
        if (LimSystem.ChartContainer == null) return;
        ReCalculateBeatlineTimes();
        OperationManager.FixAllNotesToBeatline();
    }
}
