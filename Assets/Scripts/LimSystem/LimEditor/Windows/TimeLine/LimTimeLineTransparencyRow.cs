using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The fourth motion row, Motion (Transparency), under Rotation.
///
/// It is a copy of the Rotation row, so it arrives with the same label, the
/// same scrolling view and the same proportions, and it is placed in the
/// thirty pixels that were left free between the motions and the waveform
/// strip when the strip was made shorter. Nothing in the scene had to change
/// for it, in keeping with the rest of this branch.
///
/// The row carries the transparency motions' bars exactly as the Rotation
/// row carries its own: same prefab, same height, same click, and the small
/// diamond for a motion too short to click.
/// </summary>
public partial class LimTimeLineManager
{
    /// <summary>
    /// Under Rotation, which sits at -90, in the gap the shorter waveform
    /// strip left behind: the strip begins at -150 and the rows are 30 tall.
    /// </summary>
    private const float TransparencyRowY = -120f;
    /// <summary>
    /// The two arrows that step from one motion to the next run alongside the
    /// rows and were cut to fit three of them. They hang from the top with
    /// their pivot at the foot, so the position is the bottom edge: -150 with
    /// a height of 120 covers -30 to -150, which is all four rows.
    /// </summary>
    private const float MotionArrowsFoot = -150f;
    /// <summary>
    /// How far down the motion rows reach, as a depth below the top of the
    /// view. Four rows of thirty under the timing row: -30 to -150. Anything
    /// that asks "is the cursor on the rows?" measures against this, so the
    /// rows behave alike all the way down instead of the fourth one being a
    /// dead strip.
    /// </summary>
    public const float MotionRowsDepth = 150f;
    private const float MotionArrowsHeight = 120f;

    /// <summary>
    /// A soft coral, red leaning to orange, as light as the pastel blue,
    /// green and yellow of the other three rows so the four read as one set.
    /// The other three are set in the scene; this one is set here because
    /// the row itself is made here.
    ///
    /// Not serialized. A public field is, and an editor with the scene open
    /// keeps the value it last held across recompiles, so a change to the
    /// number below would not show until the scene was reopened, and saving
    /// the scene would have fixed the old colour into it for good. The scene
    /// holds no value for it, so nothing is lost by this.
    /// </summary>
    [System.NonSerialized]
    public Color Tp14 = new Color(0.95f, 0.62f, 0.52f);

    /// <summary>Where a transparency motion's bar would go. Null until built.</summary>
    public RectTransform TrsTransform { get; private set; }
    private Text TrsText;
    private bool TransparencyRowBuilt;

    private void BuildTransparencyRow()
    {
        if (TransparencyRowBuilt) return;
        if (RotTransform == null) return;
        // Content -> Viewport -> TimeLineView -> the row itself.
        Transform Row = RotTransform.parent;
        if (Row != null) Row = Row.parent;
        if (Row != null) Row = Row.parent;
        RectTransform Source = Row as RectTransform;
        if (Source == null || Source.parent == null) return;
        TransparencyRowBuilt = true;

        GameObject Clone = Instantiate(Source.gameObject, Source.parent);
        LimThemeManager.Adopt(Source.gameObject, Clone);
        Clone.name = "TimeLineMotionTrs";
        RectTransform Rect = Clone.GetComponent<RectTransform>();
        Rect.anchorMin = Source.anchorMin;
        Rect.anchorMax = Source.anchorMax;
        Rect.pivot = Source.pivot;
        Rect.sizeDelta = Source.sizeDelta;
        Rect.localScale = Source.localScale;
        Rect.anchoredPosition = new Vector2(Source.anchoredPosition.x, TransparencyRowY);
        // A copy arrives still reporting what is done to it back to the row
        // it came from, so the trigger it was carrying goes. The hover hint
        // stays: the one hint on these rows is Timeline_Scroll, which talks
        // about the timeline rather than about rotation, and a row without
        // it would be the one row that says nothing when pointed at. Its own
        // Start puts a fresh trigger back on for it.
        foreach (EventTrigger Inherited in Clone.GetComponentsInChildren<EventTrigger>(true)) DestroyImmediate(Inherited);

        // The scrolling view knows which of its own children holds the bars,
        // and Instantiate has already pointed it at the copy's.
        ScrollRect Scroll = Clone.GetComponentInChildren<ScrollRect>(true);
        TrsTransform = Scroll != null ? Scroll.content : null;
        if (TrsTransform != null)
        {
            // Whatever rotation bars happened to be on the row when it was
            // copied came along with it.
            for (int i = TrsTransform.childCount - 1; i >= 0; --i) DestroyImmediate(TrsTransform.GetChild(i).gameObject);
        }

        // The row's own label, the one text that is not inside the view.
        foreach (Text Candidate in Clone.GetComponentsInChildren<Text>(true))
        {
            if (Candidate.transform.parent != Rect) continue;
            TrsText = Candidate;
            break;
        }
        StretchMotionArrows(Source.parent as RectTransform);
        if (LimLanguageManager.TextDict != null) SetTransparencyRowText();
    }

    private static void StretchMotionArrows(RectTransform Rows)
    {
        if (Rows == null) return;
        for (int i = 0; i < Rows.childCount; ++i)
        {
            RectTransform Child = Rows.GetChild(i) as RectTransform;
            if (Child == null) continue;
            if (Child.name != "Prev" && Child.name != "Next") continue;
            Child.anchoredPosition = new Vector2(Child.anchoredPosition.x, MotionArrowsFoot);
            Child.sizeDelta = new Vector2(Child.sizeDelta.x, MotionArrowsHeight);
        }
    }

    public void InstantiateTransparency()
    {
        if (CameraManager.Transparency == null) return;
        foreach (Lanotalium.Chart.LanotaCameraTrs Trs in CameraManager.Transparency) InstantiateSingleTransparency(Trs);
    }
    public void InstantiateSingleTransparency(Lanotalium.Chart.LanotaCameraTrs Trs)
    {
        // A chart can be loaded before this window's Start has run, and the
        // row is built there; building it here instead is harmless, as it
        // is only ever built once.
        if (TrsTransform == null) BuildTransparencyRow();
        if (TrsTransform == null) return;
        if (Trs.TimeLineGameObject != null) Destroy(Trs.TimeLineGameObject);
        Trs.TimeLineGameObject = Instantiate(TimeLineObject, TrsTransform);
        Trs.TimeLineGameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(Trs.Time * Scale, 0);
        Trs.TimeLineGameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(Trs.Duration * Scale, 30);
        Trs.TimeLineGameObject.GetComponent<Image>().color = Tp14;
        Trs.TimeLineGameObject.GetComponent<Button>().onClick.AddListener(OperationManager.OnTimeLineClick);
        Trs.InstanceId = Trs.TimeLineGameObject.GetInstanceID();
        AddSmallMotionMarker(Trs);
    }

    public void SetTransparencyRowText()
    {
        if (TrsText == null) return;
        TrsText.text = LimLanguageManager.TextDict["Window_TimeLine_Trs_Label"];
    }
}
