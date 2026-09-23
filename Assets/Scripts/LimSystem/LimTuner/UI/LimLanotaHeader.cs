using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The tuner's header dressed as Lanota's own, from Flowaria's UiTweak
/// "Header": the game's top bar, its lettering shaded from cream to gold, a
/// pause button, the difficulty badge ("MASTER 16+") and either the
/// designer's name or the score; along the top edge a thin light shows how
/// far into the song playback is. The header is the same object in the tuner
/// window and in full screen, which moves between the two canvases, so the
/// look goes wherever it goes.
///
/// Everything is laid out in fractions of the bar, measured on a screenshot
/// of the game, and laid out again whenever the bar changes size: the two
/// lines dividing the top bar's picture sit at 57.3 and 71.5 per cent of its
/// width, the pause button is 1.37 bars tall and centred at 5.3 per cent,
/// the name starts at 11.1 per cent, the badge is centred at 64.3 per cent,
/// the score's label starts at 74.8 per cent and its figures end at 97.8.
/// Text is sized from the bar's height rather than scaled, which blurred it.
///
/// The plugin asked for the difficulty in a dialog, could not be undone
/// without reloading the project, and forgot the difficulty with the
/// project. Here it is a switch in the UiTweak menu that puts every changed
/// colour, font, size and position back when turned off, and the difficulty
/// belongs to the project (<c>Difficulty</c> and <c>Level</c> in the .lap): a
/// click on the badge changes it, a click on the number types the level (a
/// trailing "+" is drawn raised, as the game does). The pause button plays
/// and pauses the song.
///
/// The interface theme records the header's grey as chrome and repaints it,
/// and the light theme darkens white icons, so while this look is on its
/// colours are written every frame; turning it off hands them back to the
/// theme through <see cref="LimThemeManager.Paint"/>.
/// </summary>
public class LimLanotaHeader : MonoBehaviour
{
    public LimTunerHeadManager Head;
    public LimTunerManager Tuner;

    /// <summary>
    /// The order a click on the badge goes through: the game's four, then the
    /// project's own (its word and colour in <c>DifficultyName</c> and
    /// <c>DifficultyColor</c>, set with a right click), then hidden.
    /// </summary>
    private static readonly string[] DifficultyNames = { "", "WHISPER", "ACOUSTIC", "ULTRA", "MASTER", "" };
    private static readonly Color[] DifficultyColors =
    {
        Color.clear,
        new Color(0.113f, 0.294f, 0.396f),
        new Color(0.031f, 0.47f, 0.454f),
        new Color(0.494f, 0.074f, 0.062f),
        new Color(0.431f, 0.141f, 0.525f),
        new Color(0.6f, 0.45f, 0.15f)
    };
    private const int CustomDifficulty = 5;
    private static readonly Color Cream = new Color(0.96f, 0.93f, 0.84f);
    /// <summary>
    /// The lettering's shading, measured row by row on the game's score
    /// figures beside the editor's own at the same size: palest high up, from
    /// a tenth to a third of the way down, then falling steadily to a dark
    /// greyish mauve at the foot. The ends are pushed further than the
    /// readings (brighter top, darker foot), since a letter's first and last
    /// rows are only partly covered and come out duller than their stop. A
    /// first reading put the palest point at the middle and the foot far
    /// lighter, which the user saw as too bright.
    /// </summary>
    private static readonly Color[] TextStops =
    {
        new Color(244 / 255f, 220 / 255f, 190 / 255f),
        new Color(250 / 255f, 227 / 255f, 196 / 255f),
        new Color(242 / 255f, 217 / 255f, 187 / 255f),
        new Color(222 / 255f, 198 / 255f, 171 / 255f),
        new Color(175 / 255f, 154 / 255f, 135 / 255f),
        new Color(128 / 255f, 110 / 255f, 98 / 255f),
        new Color(95 / 255f, 80 / 255f, 74 / 255f)
    };
    private static readonly float[] TextStopsAt = { 0, 0.12f, 0.35f, 0.5f, 0.7f, 0.9f, 1 };
    private static readonly Color ProgressColor = new Color(1f, 0.94f, 0.78f);

    // Where things sit, in fractions of the bar's width (W) and height (H).
    private const float PauseCentre = 0.053f, PauseSize = 1.37f;
    private const float NameStarts = 0.111f, NameEnds = 0.562f, NameSize = 0.40f;
    /// <summary>
    /// The purple glow reaches just past the letters, as in the game: 0.6
    /// bars tall. In the game its middle sits a little left of the lettering's
    /// and below it (the "+" counts in the lettering), so it is moved by
    /// GlowLeft of the width and GlowDown of the height from BadgeCentre.
    /// </summary>
    private const float BadgeCentre = 0.643f, BadgeWidth = 0.13f, BadgeHeight = 0.6f, GlowLeft = 0.009f, GlowDown = 0.058f;
    /// <summary>
    /// The level's figures 0.42 bars, the word 0.35 bars away and made as
    /// tall as the figures by <see cref="ArrangeBadge"/> (the user asked for
    /// the two the same size; BadgeNameSize is only where it starts from).
    /// </summary>
    /// <summary>The word's font size against the figures', and how far it is lowered (in bars) so the two stand on one line.</summary>
    private const float NameToFigures = 0.92f, NameDrop = 0.015f;
    private const float BadgeNameSize = 0.4f, LevelSize = 0.42f, BadgeGap = 0.35f, BadgeGlowAlpha = 0.6f;
    /// <summary>
    /// The game's "+" against the figures' height (their cap height): its
    /// ink 0.58 wide, its middle 0.26 above their top and its left edge on
    /// their right one. Measured on a screenshot of "16+". The glyph's own
    /// box is roomier than its visible arms, so the raise and the tuck are
    /// set by eye against the user's screenshots: at the measured figures
    /// the "+" stood 3 px clear of the figures and 6 px too high.
    /// </summary>
    private const float PlusWide = 0.58f, PlusRaise = 0.03f, PlusTuck = 0.14f;
    /// <summary>
    /// The game's bar is 5.95 per cent of the screen's width tall (0.106 of a
    /// 16:9 screen's height); the editor's is 7.7 per cent of the height,
    /// much lower. Kept to at most 14 per cent of the height on narrow windows.
    /// </summary>
    private const float BarShare = 0.0595f, BarMostOfHeight = 0.14f;
    private const float RightStarts = 0.748f, RightEnds = 0.978f, DesignerSize = 0.36f;
    /// <summary>
    /// The score as the game sets it, measured beside the editor's at the
    /// same bar height: the label 0.385 bars, the figures 0.475, from 74.2
    /// to 95.9 per cent of the width, the pair raised 0.045 bars since this
    /// face sits low in its line. At the game's height this face is set 15
    /// per cent narrower than the game's, so the characters are spaced out
    /// (<see cref="LimTextSpacing"/>): matched on width alone they came out
    /// a quarter too tall, which the user saw as too big.
    /// </summary>
    private const float ScoreStarts = 0.742f, ScoreEnds = 0.959f, LabelSize = 0.385f, FiguresSize = 0.475f, ScoreRaise = 0.045f;
    private const float LabelSpacing = 0.04f, FiguresSpacing = 0.1f;
    private const float ProgressHeight = 0.045f;
    /// <summary>The round flash at the progress line's tip, as wide as this share of the bar's height, as in the game.</summary>
    private const float SparkSize = 0.42f;
    private const int SmallestFontSize = 8, LevelCharacters = 4;
    /// <summary>Lanota's score counts to a million, shown in seven figures.</summary>
    private const int MaxScore = 1000000;

    private bool Applied, ResourcesTried;
    private GameObject BarPrefab;
    private Sprite TopBar;
    private Font HeaderFont;

    private RectTransform HeadRect;
    private Image HeadImage;
    private Sprite OldOverride;
    private Text ChartName, Placeholder, DesignerText, Copyright;
    private RectTransform DesignerRect;
    private Snapshot OldName, OldPlaceholder, OldDesigner;
    private RectSnapshot OldNameRect, OldDesignerRect;
    private Color OldHeadColor, OldCopyrightColor;
    private int DesignerFontSize = 24;
    private string FittedDesigner;
    private float FittedWidth = -1;

    private GameObject Bar;
    private Image Badge, PauseIcon, Progress, ProgressGlow, ProgressSpark;
    private static Sprite SparkSprite;
    private Text BadgeName, LevelShown, LevelEdit, ScoreLabel, ScoreValue;
    private Text LevelPlus;
    private readonly TextGenerator Measure = new TextGenerator();
    private InputField LevelField;
    private GameObject Score;
    private int ShownScore = -1, ShownDifficulty = -1;
    private string ShownLevel, ArrangedFor;
    private float BarHeight;
    private Vector2 GlowShift;
    private Vector2 LaidOut;

    /// <summary>A Text's own settings, to hand back on turning off.</summary>
    private struct Snapshot
    {
        public Font Font;
        public int Size, Min, Max;
        public bool Fit;
        public Color Colour;
        public TextAnchor Alignment;

        public static Snapshot Of(Text Label)
        {
            return new Snapshot
            {
                Font = Label.font, Size = Label.fontSize, Fit = Label.resizeTextForBestFit, Min = Label.resizeTextMinSize,
                Max = Label.resizeTextMaxSize, Colour = LimThemeManager.OriginalOf(Label), Alignment = Label.alignment
            };
        }

        public void Restore(Text Label)
        {
            Label.font = Font;
            Label.fontSize = Size;
            Label.resizeTextForBestFit = Fit;
            Label.resizeTextMinSize = Min;
            Label.resizeTextMaxSize = Max;
            Label.alignment = Alignment;
            LimThemeManager.Paint(Label, Colour);
        }
    }

    private struct RectSnapshot
    {
        public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;

        public static RectSnapshot Of(RectTransform Rect)
        {
            return new RectSnapshot { AnchorMin = Rect.anchorMin, AnchorMax = Rect.anchorMax, Pivot = Rect.pivot, Position = Rect.anchoredPosition, Size = Rect.sizeDelta };
        }

        public void Restore(RectTransform Rect)
        {
            Rect.anchorMin = AnchorMin;
            Rect.anchorMax = AnchorMax;
            Rect.pivot = Pivot;
            Rect.anchoredPosition = Position;
            Rect.sizeDelta = Size;
        }
    }

    private void LateUpdate()
    {
        bool Wanted = LimSystem.Preferences.LanotaHeader && LoadResources();
        if (Wanted && !Applied) Apply();
        else if (!Wanted && Applied) Revert();
        if (!Applied) return;
        if (Bar == null)
        {
            // Something removed it; start over rather than half dressed.
            Revert();
            return;
        }
        // The head manager sets its own height every frame; this comes after.
        RectTransform Parent = HeadRect.parent as RectTransform;
        if (Parent != null)
        {
            float Height = Mathf.Min(Parent.rect.width * BarShare, Parent.rect.height * BarMostOfHeight);
            if (HeadRect.sizeDelta.y != Height) HeadRect.sizeDelta = new Vector2(HeadRect.sizeDelta.x, Height);
        }
        Vector2 Size = HeadRect.rect.size;
        if (Size != LaidOut) Layout(Size);
        HoldColours();
        ShowDifficulty();
        ShowLevel();
        ShowScore();
        ShowProgress();
        FitDesigner();
    }

    private bool LoadResources()
    {
        if (!ResourcesTried)
        {
            ResourcesTried = true;
            BarPrefab = LimUiTweakAssets.Load<GameObject>(LimUiTweakAssets.HeaderBundle, "LanotaHeader");
            TopBar = LimUiTweakAssets.Load<Sprite>(LimUiTweakAssets.HeaderBundle, "TopBar");
            HeaderFont = LimUiTweakAssets.Load<Font>(LimUiTweakAssets.HeaderBundle, "Assets/UiTweak/HeaderTweak/Fonts/kawoszeh_header.ttf");
        }
        return BarPrefab != null && TopBar != null && HeaderFont != null && Head != null;
    }

    private void Apply()
    {
        HeadRect = Head.transform as RectTransform;
        HeadImage = Head.GetComponent<Image>();
        ChartName = Head.ChartName;
        DesignerRect = Head.DesignerInputField != null ? Head.DesignerInputField.GetComponent<RectTransform>() : null;
        Placeholder = Head.DesignerInputField != null ? Head.DesignerInputField.placeholder as Text : null;
        DesignerText = Head.DesignerInputField != null ? Head.DesignerInputField.textComponent : null;
        Transform CopyrightPlace = Head.transform.Find("Copyright");
        Copyright = CopyrightPlace != null ? CopyrightPlace.GetComponent<Text>() : null;

        if (HeadImage != null)
        {
            OldHeadColor = LimThemeManager.OriginalOf(HeadImage);
            OldOverride = HeadImage.overrideSprite;
            HeadImage.overrideSprite = TopBar;
        }
        if (ChartName != null)
        {
            OldName = Snapshot.Of(ChartName);
            OldNameRect = RectSnapshot.Of(ChartName.rectTransform);
            ChartName.font = HeaderFont;
            ChartName.alignment = TextAnchor.MiddleLeft;
            ChartName.resizeTextForBestFit = true;
            ChartName.resizeTextMinSize = SmallestFontSize;
            Dress(ChartName.gameObject);
        }
        if (Placeholder != null)
        {
            OldPlaceholder = Snapshot.Of(Placeholder);
            Placeholder.font = HeaderFont;
            Placeholder.alignment = TextAnchor.MiddleRight;
            Placeholder.resizeTextForBestFit = true;
            Placeholder.resizeTextMinSize = SmallestFontSize;
        }
        if (DesignerText != null)
        {
            OldDesigner = Snapshot.Of(DesignerText);
            DesignerText.font = HeaderFont;
            DesignerText.alignment = TextAnchor.MiddleRight;
            // Not best fit: the input field shows only the part of its text
            // that fits and scrolls the rest, so best fit never saw it too
            // long. FitDesigner sizes it from the whole name instead.
            DesignerText.resizeTextForBestFit = false;
            Dress(DesignerText.gameObject);
        }
        if (DesignerRect != null) OldDesignerRect = RectSnapshot.Of(DesignerRect);
        if (Copyright != null) OldCopyrightColor = LimThemeManager.OriginalOf(Copyright);

        Bar = Instantiate(BarPrefab, Head.transform, false);
        Bar.name = "LanotaHeader";
        Bar.transform.SetAsFirstSibling();
        BuildControls();
        Applied = true;
        ShownDifficulty = -1;
        ShownLevel = null;
        ShownScore = -1;
        FittedDesigner = null;
        LaidOut = Vector2.zero;
        Layout(HeadRect.rect.size);
        HoldColours();
        ShowDifficulty();
    }

    private void Revert()
    {
        Applied = false;
        if (Bar != null) Destroy(Bar);
        Bar = null;
        Score = null;
        if (HeadImage != null)
        {
            HeadImage.overrideSprite = OldOverride;
            LimThemeManager.Paint(HeadImage, OldHeadColor);
        }
        if (ChartName != null)
        {
            Undress(ChartName.gameObject);
            OldName.Restore(ChartName);
            OldNameRect.Restore(ChartName.rectTransform);
        }
        if (Placeholder != null) OldPlaceholder.Restore(Placeholder);
        if (DesignerText != null)
        {
            Undress(DesignerText.gameObject);
            OldDesigner.Restore(DesignerText);
        }
        if (DesignerRect != null)
        {
            OldDesignerRect.Restore(DesignerRect);
            DesignerRect.gameObject.SetActive(true);
        }
        if (Copyright != null) LimThemeManager.Paint(Copyright, OldCopyrightColor);
    }

    /// <summary>
    /// The game's lettering: shaded from cream to gold down each letter, with
    /// a faint rim of its own colour that gives the thin face some body. The
    /// badge's small letters go without the rim: at their size it only
    /// blurred their edges, where the game's are crisp.
    /// </summary>
    private static void Dress(GameObject Holder, bool Rim = true)
    {
        LimTextGradient Shade = Holder.GetComponent<LimTextGradient>();
        if (Shade == null) Shade = Holder.AddComponent<LimTextGradient>();
        Shade.Set(TextStops, TextStopsAt);
        Outline Body = Holder.GetComponent<Outline>();
        if (!Rim)
        {
            if (Body != null) DestroyImmediate(Body);
            return;
        }
        if (Body == null) Body = Holder.AddComponent<Outline>();
        Body.effectColor = new Color(0.75f, 0.66f, 0.58f, 0.35f);
        Body.effectDistance = new Vector2(0.6f, -0.6f);
        Body.useGraphicAlpha = true;
    }

    private static void Undress(GameObject Holder)
    {
        LimTextGradient Shade = Holder.GetComponent<LimTextGradient>();
        if (Shade != null) DestroyImmediate(Shade);
        Outline Body = Holder.GetComponent<Outline>();
        if (Body != null) DestroyImmediate(Body);
    }

    /// <summary>Puts everything where the game has it, for a bar this size.</summary>
    private void Layout(Vector2 Size)
    {
        LaidOut = Size;
        float W = Size.x, H = Size.y;
        BarHeight = H;
        ArrangedFor = null;
        if (W <= 0 || H <= 0) return;

        if (PauseIcon != null)
        {
            RectTransform Pause = PauseIcon.rectTransform;
            Pause.anchorMin = Pause.anchorMax = new Vector2(0, 1);
            Pause.pivot = new Vector2(0.5f, 1);
            Pause.localScale = Vector3.one;
            Sprite Picture = PauseIcon.sprite;
            float Aspect = Picture != null ? Picture.rect.width / Picture.rect.height : 1;
            Pause.sizeDelta = new Vector2(H * PauseSize * Aspect, H * PauseSize);
            Pause.anchoredPosition = new Vector2(W * PauseCentre, 0);
        }
        if (ChartName != null)
        {
            Span(ChartName.rectTransform, NameStarts, NameEnds);
            ChartName.fontSize = ChartName.resizeTextMaxSize = Font(H * NameSize);
        }
        if (DesignerRect != null) Span(DesignerRect, RightStarts, RightEnds);
        DesignerFontSize = Font(H * DesignerSize);
        FittedDesigner = null;
        if (Placeholder != null) Placeholder.fontSize = Placeholder.resizeTextMaxSize = Font(H * DesignerSize * 0.9f);
        if (Score != null)
        {
            RectTransform ScoreRect = Score.transform as RectTransform;
            Span(ScoreRect, ScoreStarts, ScoreEnds);
            ScoreRect.anchoredPosition = new Vector2(0, Mathf.Round(H * ScoreRaise));
            ScoreLabel.fontSize = Font(H * LabelSize);
            ScoreValue.fontSize = Font(H * FiguresSize);
        }
        if (Badge != null)
        {
            RectTransform Glow = Badge.rectTransform;
            Glow.anchorMin = Glow.anchorMax = new Vector2(0, 0.5f);
            Glow.pivot = new Vector2(0.5f, 0.5f);
            Glow.localScale = Vector3.one;
            // On whole pixels, and the badge an even number of them wide and
            // tall, so its letters are not drawn between two pixels.
            // The glow is moved and the lettering, its children, moved back by
            // the same amount (in ArrangeBadge), so only the glow shifts.
            GlowShift = new Vector2(-Mathf.Round(W * GlowLeft), -Mathf.Round(H * GlowDown));
            Glow.anchoredPosition = new Vector2(Mathf.Round(W * BadgeCentre), 0) + GlowShift;
            Glow.sizeDelta = new Vector2(Mathf.Round(W * BadgeWidth / 2) * 2, Mathf.Round(H * BadgeHeight / 2) * 2);
            if (BadgeName != null) BadgeName.fontSize = Font(H * BadgeNameSize);
            if (LevelShown != null) LevelShown.fontSize = LevelEdit.fontSize = Font(H * LevelSize);
        }
        if (Progress != null)
        {
            Progress.rectTransform.sizeDelta = new Vector2(0, Mathf.Max(2, H * ProgressHeight));
            ProgressGlow.rectTransform.sizeDelta = new Vector2(0, Mathf.Max(6, H * ProgressHeight * 3.5f));
            float Spark = Mathf.Max(8, Mathf.Round(H * SparkSize));
            ProgressSpark.rectTransform.sizeDelta = new Vector2(Spark, Spark * 1.5f);
        }
    }

    /// <summary>Stretches a rect across the bar's full height between two fractions of its width.</summary>
    private static void Span(RectTransform Rect, float From, float To)
    {
        Rect.anchorMin = new Vector2(From, 0);
        Rect.anchorMax = new Vector2(To, 1);
        Rect.pivot = new Vector2(0.5f, 0.5f);
        Rect.anchoredPosition = Vector2.zero;
        Rect.sizeDelta = Vector2.zero;
    }

    private static int Font(float Size)
    {
        return Mathf.Max(SmallestFontSize, Mathf.RoundToInt(Size));
    }

    /// <summary>
    /// The designer's name at its full size, or smaller when the whole of it
    /// would not fit its field; left alone while it is being typed.
    /// </summary>
    private void FitDesigner()
    {
        if (DesignerText == null || Head.DesignerInputField == null || Head.DesignerInputField.isFocused) return;
        if (!DesignerRect.gameObject.activeInHierarchy) return;
        string Full = Head.DesignerInputField.text ?? string.Empty;
        float Room = DesignerText.rectTransform.rect.width;
        if (Full == FittedDesigner && Room == FittedWidth) return;
        FittedDesigner = Full;
        FittedWidth = Room;
        TextGenerationSettings Settings = DesignerText.GetGenerationSettings(new Vector2(float.MaxValue, DesignerText.rectTransform.rect.height));
        Settings.fontSize = DesignerFontSize;
        Settings.resizeTextForBestFit = false;
        Settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        float Needed = DesignerText.cachedTextGeneratorForLayout.GetPreferredWidth(Full, Settings) / DesignerText.pixelsPerUnit;
        int Size = DesignerFontSize;
        if (Needed > Room && Needed > 0) Size = Mathf.Max(SmallestFontSize, Mathf.FloorToInt(DesignerFontSize * Room / Needed));
        if (DesignerText.fontSize != Size) DesignerText.fontSize = Size;
    }

    /// <summary>
    /// The bar at its own colours (the plugin tinted it grey, which halved
    /// the gold of its rim and dividers; the game shows them bright), the
    /// lettering shaded (its own colour is white,
    /// the shading does the rest), the copyright line hidden (the game has
    /// none), the pause icon white. The placeholder keeps its transparency.
    /// </summary>
    private void HoldColours()
    {
        if (HeadImage != null && HeadImage.color != Color.white) HeadImage.color = Color.white;
        if (ChartName != null && ChartName.color != Color.white) ChartName.color = Color.white;
        if (DesignerText != null && DesignerText.color != Color.white) DesignerText.color = Color.white;
        if (Placeholder != null)
        {
            Color Faint = new Color(Cream.r, Cream.g, Cream.b, OldPlaceholder.Colour.a * 0.8f);
            if (Placeholder.color != Faint) Placeholder.color = Faint;
        }
        if (Copyright != null && Copyright.color != Color.clear) Copyright.color = Color.clear;
        if (PauseIcon != null && PauseIcon.color != Color.white) PauseIcon.color = Color.white;
    }

    /// <summary>
    /// The badge becomes a button with the level drawn over it, the pause
    /// icon a button, and the score and the progress light are added.
    /// </summary>
    private void BuildControls()
    {
        Transform Glow = Bar.transform.Find("DifficultyGlow");
        Badge = Glow != null ? Glow.GetComponent<Image>() : null;
        Font HintFont = OldName.Font;
        if (Badge != null)
        {
            Badge.preserveAspect = false;
            Transform Name = Glow.Find("TextName");
            BadgeName = Name != null ? Name.GetComponent<Text>() : null;
            if (BadgeName != null)
            {
                Place(BadgeName.rectTransform, new Vector2(0.02f, 0), new Vector2(0.56f, 1));
                BadgeName.alignment = TextAnchor.MiddleLeft;
                BadgeName.horizontalOverflow = HorizontalWrapMode.Overflow;
                BadgeName.color = Color.white;
                Dress(BadgeName.gameObject, false);
                BadgeName.raycastTarget = false;
            }
            // The prefab's own number goes; the level is drawn by the texts below.
            Transform OldLevel = Glow.Find("TextLevel");
            if (OldLevel != null) DestroyImmediate(OldLevel.gameObject);
            Button Cycle = Badge.gameObject.AddComponent<Button>();
            Cycle.transition = Selectable.Transition.None;
            Cycle.targetGraphic = Badge;
            Cycle.onClick.AddListener(NextDifficulty);
            Badge.gameObject.AddComponent<LimRightClick>().Clicked = OpenCustomDifficulty;
            AddHint(Badge.gameObject, "Header_Difficulty", HintFont);
            BuildLevel(Badge.rectTransform, HintFont);
        }

        BuildScore();
        BuildProgress();

        Transform Pause = Bar.transform.Find("Pause");
        PauseIcon = Pause != null ? Pause.GetComponent<Image>() : null;
        if (PauseIcon != null)
        {
            Button Toggle = PauseIcon.gameObject.AddComponent<Button>();
            Toggle.transition = Selectable.Transition.None;
            Toggle.targetGraphic = PauseIcon;
            Toggle.onClick.AddListener(TogglePlayback);
            AddHint(PauseIcon.gameObject, "Header_Pause", HintFont);
        }
    }

    private static void Place(RectTransform Rect, Vector2 Min, Vector2 Max)
    {
        Rect.anchorMin = Min;
        Rect.anchorMax = Max;
        Rect.pivot = new Vector2(0.5f, 0.5f);
        Rect.anchoredPosition = Vector2.zero;
        Rect.sizeDelta = Vector2.zero;
    }

    private Text NewText(Transform Parent, string Name, TextAnchor Alignment)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        Holder.layer = Parent.gameObject.layer;
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Place(Rect, Vector2.zero, Vector2.one);
        Text Label = Holder.AddComponent<Text>();
        Label.font = HeaderFont;
        Label.alignment = Alignment;
        Label.horizontalOverflow = HorizontalWrapMode.Overflow;
        Label.verticalOverflow = VerticalWrapMode.Overflow;
        Label.raycastTarget = false;
        Label.supportRichText = false;
        return Label;
    }

    /// <summary>
    /// The level, right of the difficulty's name: its figures shaded like the
    /// rest, a trailing "+" small and raised. Over them lies an input field,
    /// whose own text only shows while the level is being typed.
    /// </summary>
    private void BuildLevel(RectTransform Glow, Font HintFont)
    {
        GameObject Holder = new GameObject("LevelField", typeof(RectTransform));
        Holder.layer = Glow.gameObject.layer;
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Glow, false);
        Place(Rect, new Vector2(0.62f, 0), new Vector2(0.98f, 1));
        Image Catcher = Holder.AddComponent<Image>();
        Catcher.color = Color.clear;

        LevelShown = NewText(Rect, "Level", TextAnchor.MiddleLeft);
        LevelShown.color = Color.white;
        Dress(LevelShown.gameObject, false);
        // The header face's own "+", shaded like the figures.
        LevelPlus = NewText(Rect, "Plus", TextAnchor.MiddleLeft);
        LevelPlus.text = "+";
        LevelPlus.color = Color.white;
        Dress(LevelPlus.gameObject, false);
        LevelEdit = NewText(Rect, "Typing", TextAnchor.MiddleLeft);
        LevelEdit.color = Cream;
        LevelEdit.horizontalOverflow = HorizontalWrapMode.Wrap;

        LevelField = Holder.AddComponent<InputField>();
        LevelField.transition = Selectable.Transition.None;
        LevelField.targetGraphic = Catcher;
        LevelField.textComponent = LevelEdit;
        LevelField.characterLimit = LevelCharacters;
        LevelField.lineType = InputField.LineType.SingleLine;
        LevelField.caretColor = Cream;
        LevelField.customCaretColor = true;
        LevelField.onEndEdit.AddListener(OnLevelTyped);
        AddHint(Holder, "Header_Level", HintFont);
    }

    /// <summary>
    /// The figures while the level is not being typed; the typed text while
    /// it is. The "+" is placed just after the figures' own width.
    /// </summary>
    private void ShowLevel()
    {
        if (LevelField == null) return;
        bool Typing = LevelField.isFocused;
        string Level = Project != null && Project.Level != null ? Project.Level : "";
        bool Plus = Level.EndsWith("+");
        string Figures = Plus ? Level.Substring(0, Level.Length - 1) : Level;
        LevelEdit.enabled = Typing;
        LevelShown.enabled = LevelPlus.enabled = !Typing;
        if (Typing) return;
        if (LevelShown.text != Figures) LevelShown.text = Figures;
        LevelPlus.gameObject.SetActive(Plus);
        ArrangeBadge(Figures, Plus);
    }

    /// <summary>
    /// The word and the level side by side, 0.35 bars apart, the pair centred
    /// in the badge; the "+" against the figures' top right.
    /// </summary>
    private void ArrangeBadge(string Figures, bool Plus)
    {
        if (BadgeName == null || BarHeight <= 0) return;
        string Key = BadgeName.text + "|" + Figures + "|" + Plus;
        if (Key == ArrangedFor) return;
        ArrangedFor = Key;
        float H = BarHeight;
        float LevelWidth = LevelShown.preferredWidth;
        // Where the figures' ink actually is, so the star sits on their corner
        // whatever the face's spacing.
        float Top = LevelShown.fontSize * 0.35f, Bottom = -Top, Right = LevelWidth;
        Rect Figured = new Rect();
        bool Measured = Plus && Figures.Length != 0 && Ink(LevelShown, Figures, H, out Figured);
        if (Measured)
        {
            Top = Figured.yMax; Bottom = Figured.yMin; Right = Figured.xMax;
        }
        float Cap = Top - Bottom;
        // The word as tall as the figures: this face's capitals stand 6 per
        // cent taller than its figures at one font size (measured on the
        // user's screenshot, 19 px of word at 0.29 bars against 26 px of
        // figures at 0.42). Measuring the glyph boxes instead left the word
        // 15 per cent short, as the capitals' boxes carry room the ink does not.
        BadgeName.fontSize = Mathf.Max(1, Mathf.RoundToInt(LevelShown.fontSize * NameToFigures));
        float NameWidth = BadgeName.preferredWidth;
        // The "+" at the size that makes its ink the game's width, then
        // placed by its ink rather than by its line box.
        Rect Crossed = new Rect(0, 0, Cap * PlusWide, Cap * PlusWide);
        if (Plus)
        {
            LevelPlus.fontSize = LevelShown.fontSize;
            if (Ink(LevelPlus, "+", H, out Crossed) && Crossed.width > 0)
            {
                LevelPlus.fontSize = Mathf.Max(1, Mathf.RoundToInt(LevelShown.fontSize * Cap * PlusWide / Crossed.width));
                Ink(LevelPlus, "+", H, out Crossed);
            }
        }
        float PlusLeft = Right - Cap * PlusTuck;
        float PlusWidth = Plus ? Mathf.Max(0, PlusLeft + Crossed.width - LevelWidth) : 0;
        float Gap = Figures.Length != 0 ? H * BadgeGap : 0;
        float Field = Mathf.Max(LevelWidth + PlusWidth, H * 0.5f);
        float Left = -(NameWidth + Gap + LevelWidth + PlusWidth) / 2;
        RectTransform NameRect = BadgeName.rectTransform;
        NameRect.anchorMin = NameRect.anchorMax = new Vector2(0.5f, 0.5f);
        NameRect.pivot = new Vector2(0, 0.5f);
        NameRect.sizeDelta = new Vector2(NameWidth + 2, H);
        Left = Mathf.Round(Left);
        NameRect.anchoredPosition = new Vector2(Left, -Mathf.Round(H * NameDrop)) - GlowShift;
        RectTransform FieldRect = LevelField.transform as RectTransform;
        FieldRect.anchorMin = FieldRect.anchorMax = new Vector2(0.5f, 0.5f);
        FieldRect.pivot = new Vector2(0, 0.5f);
        FieldRect.sizeDelta = new Vector2(Field, H);
        FieldRect.anchoredPosition = new Vector2(Mathf.Round(Left + NameWidth + Gap), 0) - GlowShift;
        RectTransform PlusRect = LevelPlus.rectTransform;
        PlusRect.anchorMin = PlusRect.anchorMax = new Vector2(0, 0.5f);
        PlusRect.pivot = new Vector2(0, 0.5f);
        PlusRect.sizeDelta = new Vector2(Crossed.xMax + 4, H);
        PlusRect.anchoredPosition = new Vector2(Mathf.Round(PlusLeft - Crossed.xMin), Mathf.Round(Top + Cap * PlusRaise - Crossed.center.y));
    }

    /// <summary>
    /// Where a text's glyphs actually put ink, relative to the left middle
    /// of a line box as tall as the bar, as <see cref="ArrangeBadge"/> lays
    /// its texts out.
    /// </summary>
    private bool Ink(Text Label, string Words, float H, out Rect Box)
    {
        TextGenerationSettings Settings = Label.GetGenerationSettings(new Vector2(0, H));
        Settings.pivot = new Vector2(0, 0.5f);
        Measure.Populate(Words, Settings);
        float Scale = Label.pixelsPerUnit > 0 ? 1 / Label.pixelsPerUnit : 1;
        float High = float.MinValue, Low = float.MaxValue, Near = float.MaxValue, Far = float.MinValue;
        foreach (UIVertex Vertex in Measure.verts)
        {
            High = Mathf.Max(High, Vertex.position.y * Scale);
            Low = Mathf.Min(Low, Vertex.position.y * Scale);
            Near = Mathf.Min(Near, Vertex.position.x * Scale);
            Far = Mathf.Max(Far, Vertex.position.x * Scale);
        }
        Box = High > Low ? Rect.MinMaxRect(Near, Low, Far, High) : new Rect();
        return High > Low;
    }

    /// <summary>
    /// Asks for the project's own difficulty, its word and its colour, and
    /// shows it on the badge. A right click on the badge, or the UiTweak menu.
    /// </summary>
    public void OpenCustomDifficulty()
    {
        if (Project == null || EasyRequest.EasyRequestManager.Instance == null) return;
        StartCoroutine(AskCustomDifficulty());
    }

    private System.Collections.IEnumerator AskCustomDifficulty()
    {
        string Name = string.IsNullOrEmpty(Project.DifficultyName) ? "SPECIAL" : Project.DifficultyName;
        string Colour = "#" + (string.IsNullOrEmpty(Project.DifficultyColor) ? "9A7326" : Project.DifficultyColor.TrimStart('#'));
        bool Spanish = LimSystem.Preferences.LanguageName != "English";
        string Title = LimLanguageManager.TextDict["Header_CustomDifficulty"];
        if (Spanish)
        {
            EasyRequest.Request<CustomDifficultyEs> Form = new EasyRequest.Request<CustomDifficultyEs>();
            Form.Object.Name = Name;
            Form.Object.Colour = Colour;
            yield return LimColourForm.Run(this, Form, Title, 1);
            if (Form.Succeed) UseCustomDifficulty(Form.Object.Name, Form.Object.Colour);
        }
        else
        {
            EasyRequest.Request<CustomDifficultyEn> Form = new EasyRequest.Request<CustomDifficultyEn>();
            Form.Object.Name = Name;
            Form.Object.Colour = Colour;
            yield return LimColourForm.Run(this, Form, Title, 1);
            if (Form.Succeed) UseCustomDifficulty(Form.Object.Name, Form.Object.Colour);
        }
    }

    private void UseCustomDifficulty(string Name, string Colour)
    {
        if (Project == null) return;
        Project.DifficultyName = (Name ?? string.Empty).Trim();
        Project.DifficultyColor = (Colour ?? string.Empty).Trim().TrimStart('#');
        Project.Difficulty = CustomDifficulty;
        ShownDifficulty = -1;
        ShowDifficulty();
    }

    public class CustomDifficultyEs
    {
        [EasyRequest.Name("Nombre de la dificultad")]
        public string Name;
        [EasyRequest.Name("Color (HEX)")]
        public string Colour;
    }

    public class CustomDifficultyEn
    {
        [EasyRequest.Name("Difficulty name")]
        public string Name;
        [EasyRequest.Name("Colour (HEX)")]
        public string Colour;
    }

    /// <summary>
    /// "Score" and the seven figures, where the designer's name is, as the
    /// game lays them out: both shaded, the figures larger.
    /// </summary>
    private void BuildScore()
    {
        Score = new GameObject("Score", typeof(RectTransform));
        Score.layer = Bar.layer;
        Score.transform.SetParent(Bar.transform, false);
        ScoreLabel = NewText(Score.transform, "Label", TextAnchor.MiddleLeft);
        ScoreLabel.text = "Score";
        ScoreLabel.color = Color.white;
        // Spaced before shaded: the shading cuts each character in pieces.
        ScoreLabel.gameObject.AddComponent<LimTextSpacing>().Share = LabelSpacing;
        Dress(ScoreLabel.gameObject);
        ScoreValue = NewText(Score.transform, "Value", TextAnchor.MiddleRight);
        ScoreValue.color = Color.white;
        ScoreValue.gameObject.AddComponent<LimTextSpacing>().Share = FiguresSpacing;
        Dress(ScoreValue.gameObject);
        ShownScore = -1;
    }

    /// <summary>
    /// A thin light along the top edge of the bar, from the left, as long as
    /// the share of the song already played, with a soft glow round it.
    /// </summary>
    private void BuildProgress()
    {
        ProgressGlow = ProgressPiece("ProgressGlow", new Color(ProgressColor.r, ProgressColor.g, ProgressColor.b, 0.25f));
        Progress = ProgressPiece("Progress", ProgressColor);
        // The flash rides the line's right end, its round head on the line
        // and a short ray hanging below it, as the game draws it.
        GameObject Holder = new GameObject("Spark", typeof(RectTransform));
        Holder.layer = Bar.layer;
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Progress.rectTransform, false);
        Rect.anchorMin = Rect.anchorMax = new Vector2(1, 0.5f);
        Rect.pivot = new Vector2(0.5f, 2f / 3f);
        Rect.anchoredPosition = Vector2.zero;
        ProgressSpark = Holder.AddComponent<Image>();
        ProgressSpark.sprite = MakeSpark();
        ProgressSpark.color = Color.white;
        ProgressSpark.raycastTarget = false;
    }

    /// <summary>
    /// A soft round glow, white at the middle and warm at its edge, in the
    /// top two thirds of the picture, with a faint ray running down from it.
    /// Drawn once.
    /// </summary>
    private static Sprite MakeSpark()
    {
        if (SparkSprite != null) return SparkSprite;
        const int Wide = 64, Tall = 96;
        float CentreX = Wide / 2f, CentreY = Tall * 2f / 3f, Radius = Wide / 2f;
        Texture2D Picture = new Texture2D(Wide, Tall, TextureFormat.RGBA32, false);
        Picture.wrapMode = TextureWrapMode.Clamp;
        Color[] Pixels = new Color[Wide * Tall];
        for (int Y = 0; Y < Tall; ++Y)
        {
            for (int X = 0; X < Wide; ++X)
            {
                float Dx = X + 0.5f - CentreX, Dy = Y + 0.5f - CentreY;
                float Distance = Mathf.Sqrt(Dx * Dx + Dy * Dy) / Radius;
                float Glow = Mathf.Exp(-Distance * Distance * 3f);
                float Core = Mathf.Exp(-Distance * Distance * 18f);
                float Ray = Dy < 0 ? Mathf.Exp(-Dx * Dx / 3f) * Mathf.Clamp01(1 + Dy / (Tall * 0.62f)) * 0.55f : 0;
                float Alpha = Mathf.Clamp01(Glow * 0.85f + Core + Ray);
                Color Shade = Color.Lerp(new Color(1f, 0.86f, 0.62f), Color.white, Mathf.Clamp01(Core * 1.5f + Glow * 0.4f));
                Shade.a = Alpha;
                Pixels[Y * Wide + X] = Shade;
            }
        }
        Picture.SetPixels(Pixels);
        Picture.Apply();
        SparkSprite = Sprite.Create(Picture, new Rect(0, 0, Wide, Tall), new Vector2(0.5f, 2f / 3f), 100);
        return SparkSprite;
    }

    private Image ProgressPiece(string Name, Color Colour)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform));
        Holder.layer = Bar.layer;
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Bar.transform, false);
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(0, 1);
        Rect.pivot = new Vector2(0, 0.5f);
        Rect.anchoredPosition = Vector2.zero;
        Image Look = Holder.AddComponent<Image>();
        Look.color = Colour;
        Look.raycastTarget = false;
        return Look;
    }

    private void ShowProgress()
    {
        if (Progress == null) return;
        bool Wanted = LimSystem.Preferences.ProgressBar;
        if (Progress.gameObject.activeSelf != Wanted)
        {
            Progress.gameObject.SetActive(Wanted);
            ProgressGlow.gameObject.SetActive(Wanted);
        }
        if (!Wanted || Tuner == null || !Tuner.isInitialized || LimSystem.ChartContainer == null || LimSystem.ChartContainer.ChartData == null) return;
        float Length = LimSystem.ChartContainer.ChartData.SongLength;
        float Share = Length > 0 ? Mathf.Clamp01(Tuner.ChartTime / Length) : 0;
        RectTransform Rect = Progress.rectTransform;
        // Anchored at the left; the right anchor carries the share.
        Rect.anchorMax = new Vector2(Share, 1);
        Rect.offsetMax = new Vector2(0, Rect.offsetMax.y);
        RectTransform GlowRect = ProgressGlow.rectTransform;
        GlowRect.anchorMax = new Vector2(Share, 1);
        GlowRect.offsetMax = new Vector2(0, GlowRect.offsetMax.y);
        if (ProgressSpark.enabled != Share > 0) ProgressSpark.enabled = Share > 0;
    }

    /// <summary>
    /// With Show Score on, the score takes the designer's place: every note
    /// passed is a perfect hit, so it is the share of the chart's combo
    /// reached so far, out of a million.
    /// </summary>
    private void ShowScore()
    {
        bool Wanted = LimSystem.Preferences.ShowScore && Score != null;
        if (Score != null && Score.activeSelf != Wanted) Score.SetActive(Wanted);
        if (DesignerRect != null && DesignerRect.gameObject.activeSelf == Wanted) DesignerRect.gameObject.SetActive(!Wanted);
        if (!Wanted || Tuner == null || !Tuner.isInitialized) return;
        int Total = LimChartClock.Combo(Tuner, float.MaxValue);
        int Reached = LimChartClock.Combo(Tuner, Tuner.ChartTime);
        int Value = Total > 0 ? (int)((long)MaxScore * Reached / Total) : 0;
        if (Value == ShownScore) return;
        ShownScore = Value;
        ScoreValue.text = Value.ToString("D7");
    }

    private static void AddHint(GameObject Target, string Key, Font HintFont)
    {
        LimMouseOverHint Hint = Target.AddComponent<LimMouseOverHint>();
        Hint.HintTextDictKey = Key;
        Hint.Font = HintFont;
    }

    private static Lanotalium.Project.LanotaliumProject Project
    {
        get { return LimProjectManager.CurrentProject; }
    }

    private void NextDifficulty()
    {
        if (Project == null) return;
        int Current = Mathf.Clamp(Project.Difficulty, 0, DifficultyNames.Length - 1);
        Project.Difficulty = (Current + 1) % DifficultyNames.Length;
        ShowDifficulty();
    }

    private void OnLevelTyped(string Typed)
    {
        if (Project == null) return;
        Project.Level = Typed.Trim();
        ShownLevel = null;
        ShowDifficulty();
    }

    private void TogglePlayback()
    {
        if (Tuner == null || Tuner.MediaPlayerManager == null || !Tuner.isInitialized) return;
        Tuner.MediaPlayerManager.IsPlaying = !Tuner.MediaPlayerManager.IsPlaying;
        // Otherwise the space bar would press it again.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void ShowDifficulty()
    {
        int Difficulty = Project != null ? Mathf.Clamp(Project.Difficulty, 0, DifficultyNames.Length - 1) : 0;
        string Level = Project != null && Project.Level != null ? Project.Level : "";
        if (Difficulty == CustomDifficulty) Level += "|" + Project.DifficultyName + "|" + Project.DifficultyColor;
        if (Difficulty == ShownDifficulty && Level == ShownLevel) return;
        ShownDifficulty = Difficulty;
        ShownLevel = Level;
        Color Glow = DifficultyColors[Difficulty];
        string Word = DifficultyNames[Difficulty];
        if (Difficulty == CustomDifficulty)
        {
            Color Own;
            if (LimTimeGroups.TryParseTint(Project.DifficultyColor, out Own)) Glow = Own;
            Word = string.IsNullOrEmpty(Project.DifficultyName) ? "SPECIAL" : Project.DifficultyName.ToUpperInvariant();
        }
        // A soft glow behind the words, not a solid pill.
        if (Badge != null) Badge.color = Difficulty == 0 ? Color.clear : new Color(Glow.r, Glow.g, Glow.b, BadgeGlowAlpha);
        if (BadgeName != null) BadgeName.text = Word;
        ArrangedFor = null;
        if (LevelField != null)
        {
            LevelField.gameObject.SetActive(Difficulty != 0);
            if (!LevelField.isFocused) LevelField.text = Project != null && Project.Level != null ? Project.Level : "";
        }
    }
}

/// <summary>Tells its owner when the right button is clicked on its object; the left stays with the Button.</summary>
public class LimRightClick : MonoBehaviour, IPointerClickHandler
{
    public System.Action Clicked;

    public void OnPointerClick(PointerEventData Data)
    {
        if (Data.button == PointerEventData.InputButton.Right && Clicked != null) Clicked();
    }
}
