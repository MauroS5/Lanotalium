using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kept angleline patterns, so a set of lines typed once can be brought back
/// with a click instead of being typed again.
///
/// The plus button beside the field keeps whatever is written in it. Each
/// kept pattern gets a row underneath with three buttons: the tick shows
/// those lines, the second forgets the pattern, the third puts it back in the
/// field to be changed, after which the plus button saves over it. Every
/// button says what it does when the pointer rests on it.
///
/// Only one pattern shows at a time. Two sets of lines on the ring at once
/// would sit on top of each other and there would be no telling which line a
/// note snapped to, so ticking one unticks the other.
///
/// The list lives in the preferences file, so it survives a restart, and the
/// whole row of controls is built here rather than in the scene.
/// </summary>
public partial class LimAngleLineManager
{
    private const float FavouriteRowStep = 35f;
    private const float FavouriteRowHeight = 30f;
    private const float FavouriteButtonSize = 30f;
    private static readonly Color IconColor = new Color(0.15f, 0.15f, 0.15f);

    private class FavouriteRow
    {
        public GameObject Root;
        public Toggle Tick;
    }

    private readonly List<FavouriteRow> _FavouriteRows = new List<FavouriteRow>();
    private float _BaseToolHeight;
    private int _ActiveFavourite = -1;
    private int _EditingFavourite = -1;
    /// <summary>
    /// The kept pattern the X has been pressed on once. A second press
    /// within ForgetClickSeconds removes it; anything else, including
    /// pressing the X of another row, starts again. The Favourite Groups
    /// tool asks the same way, and a list of patterns is easy to thin out
    /// by accident.
    /// </summary>
    private int _PendingDelete = -1;
    private float _PendingDeleteTime = -1;
    private const float ForgetClickSeconds = 0.4f;
    private bool _SuppressTickEvents;
    private bool _LastEnable;

    private List<string> Favourites
    {
        get
        {
            if (LimSystem.Preferences.AnglelineFavourites == null) LimSystem.Preferences.AnglelineFavourites = new List<string>();
            return LimSystem.Preferences.AnglelineFavourites;
        }
    }

    private Slider _OpacitySlider;
    private Text _OpacityLabel;

    private void Start()
    {
        if (ToolBase != null) _BaseToolHeight = ToolBase.ToolHeight;
        CreateAddButton();
        CreateOpacityRow();
        RebuildFavouriteRows();
    }

    /// <summary>
    /// The opacity slider, on its own row under the field. The kept patterns
    /// are listed below it.
    /// </summary>
    private void CreateOpacityRow()
    {
        if (AnglelineInputField == null) return;
        RectTransform Parent = AnglelineInputField.GetComponent<RectTransform>().parent as RectTransform;
        if (Parent == null) return;
        Font Face = AnglelineInputField.textComponent != null ? AnglelineInputField.textComponent.font : null;

        _OpacityLabel = LimUiBuilder.CreateLabel(Parent, "OpacityLabel", Face, 14, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft);
        RectTransform LabelRect = _OpacityLabel.rectTransform;
        LabelRect.anchoredPosition = new Vector2(4, -FavouriteRowStep - 5);
        LabelRect.sizeDelta = new Vector2(90, FavouriteRowHeight);

        _OpacitySlider = LimUiBuilder.CreateSlider(Parent, "Opacity", AnglelineInputField.GetComponent<Image>(), 0, 1, LineOpacity);
        RectTransform SliderRect = _OpacitySlider.GetComponent<RectTransform>();
        SliderRect.anchorMin = new Vector2(0, 1);
        SliderRect.anchorMax = new Vector2(1, 1);
        SliderRect.pivot = new Vector2(0.5f, 1);
        SliderRect.offsetMin = new Vector2(98, -FavouriteRowStep - 25);
        SliderRect.offsetMax = new Vector2(-5, -FavouriteRowStep - 10);
        _OpacitySlider.onValueChanged.AddListener((float Value) => { LineOpacity = Value; });
        RefreshOpacityLabel();
    }

    private void RefreshOpacityLabel()
    {
        if (_OpacityLabel == null || LimLanguageManager.TextDict == null) return;
        _OpacityLabel.text = LimLanguageManager.TextDict["Window_Creator_Opacity"];
    }

    /// <summary>
    /// Makes room at the right hand end of the field for the plus button.
    /// </summary>
    private void CreateAddButton()
    {
        if (AnglelineInputField == null) return;
        RectTransform Field = AnglelineInputField.GetComponent<RectTransform>();
        if (Field == null || Field.parent == null) return;

        Vector2 Max = Field.offsetMax;
        Max.x -= FavouriteButtonSize + 5;
        Field.offsetMax = Max;

        CreateSquareButton(Field.parent as RectTransform, "AddFavourite", "+", LimIcons.Favourite, -5, -5, true,
                           "Angleline_AddFavourite", AddCurrentToFavourites);
    }

    /// <summary>
    /// A small square button with the face of the checkbox beside it, labelled
    /// with one or two characters. X is measured from the left edge of the
    /// tool, or from the right edge when FromRight is set.
    /// </summary>
    private GameObject CreateSquareButton(RectTransform Parent, string Name, string Label, string Icon, float X, float Y,
                                          bool FromRight, string HintKey, UnityEngine.Events.UnityAction OnClick)
    {
        if (Parent == null) return null;
        GameObject Holder = new GameObject(Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = new Vector2(FromRight ? 1 : 0, 1);
        Rect.anchorMax = new Vector2(FromRight ? 1 : 0, 1);
        Rect.pivot = new Vector2(FromRight ? 1 : 0, 1);
        Rect.anchoredPosition = new Vector2(X, Y);
        Rect.sizeDelta = new Vector2(FavouriteButtonSize, FavouriteRowHeight);

        Image Face = Holder.GetComponent<Image>();
        Image Reference = EnableToggle != null ? EnableToggle.targetGraphic as Image : null;
        if (Reference != null)
        {
            Face.sprite = Reference.sprite;
            Face.type = Reference.type;
            Face.color = Reference.color;
        }

        Button Btn = Holder.GetComponent<Button>();
        Btn.targetGraphic = Face;
        Btn.onClick.AddListener(OnClick);

        // An icon when there is one, the letter it used to have otherwise, so
        // a missing file leaves a usable button rather than a blank one.
        if (!LimIcons.TryPlace(Rect, Icon, IconColor))
        {
            Text Caption = CreateLabel(Rect, Label, TextAnchor.MiddleCenter);
            if (Caption != null) Caption.color = IconColor;
        }

        AddHint(Holder, HintKey);
        return Holder;
    }

    private Text CreateLabel(RectTransform Parent, string Content, TextAnchor Alignment)
    {
        GameObject Holder = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = Vector2.zero;
        Rect.anchorMax = Vector2.one;
        Rect.offsetMin = new Vector2(2, 0);
        Rect.offsetMax = new Vector2(-2, 0);

        Text Label = Holder.GetComponent<Text>();
        Label.text = Content;
        Label.alignment = Alignment;
        Label.raycastTarget = false;
        Font Reference = AnglelineInputField != null && AnglelineInputField.textComponent != null
            ? AnglelineInputField.textComponent.font : null;
        if (Reference != null) Label.font = Reference;
        Label.fontSize = 16;
        return Label;
    }

    /// <summary>
    /// Hangs a hover note on a control, in whichever language is loaded.
    /// </summary>
    private void AddHint(GameObject Target, string HintKey)
    {
        if (string.IsNullOrEmpty(HintKey)) return;
        LimMouseOverHint Hint = Target.AddComponent<LimMouseOverHint>();
        Hint.HintTextDictKey = HintKey;
        if (AnglelineInputField != null && AnglelineInputField.textComponent != null)
            Hint.Font = AnglelineInputField.textComponent.font;
    }

    public void AddCurrentToFavourites()
    {
        if (AnglelineInputField == null) return;
        string Pattern = AnglelineInputField.text.Trim();
        if (Pattern.Length == 0) return;
        // Nothing unreadable gets kept: the field turns red instead.
        if (!TryParse(Pattern))
        {
            AnglelineImg.color = InvalidColor;
            return;
        }
        AnglelineImg.color = ValidColor;

        if (_EditingFavourite >= 0 && _EditingFavourite < Favourites.Count)
        {
            Favourites[_EditingFavourite] = Pattern;
            _EditingFavourite = -1;
        }
        else if (!Favourites.Contains(Pattern))
        {
            Favourites.Add(Pattern);
        }
        RebuildFavouriteRows();
    }

    private void RebuildFavouriteRows()
    {
        foreach (FavouriteRow Row in _FavouriteRows) if (Row.Root != null) Destroy(Row.Root);
        _FavouriteRows.Clear();
        if (AnglelineInputField == null) return;
        RectTransform Parent = AnglelineInputField.GetComponent<RectTransform>().parent as RectTransform;
        if (Parent == null) return;

        for (int i = 0; i < Favourites.Count; ++i) _FavouriteRows.Add(CreateFavouriteRow(Parent, i));

        // The tool grows with the list, and the Creator window restacks.
        if (ToolBase != null) ToolBase.Height = 30 + _BaseToolHeight + FavouriteRowStep + Favourites.Count * FavouriteRowStep;
        RefreshOpacityLabel();
        RefreshTicks();
    }

    private FavouriteRow CreateFavouriteRow(RectTransform Parent, int Index)
    {
        GameObject Root = new GameObject("Favourite" + Index, typeof(RectTransform));
        RectTransform Rect = Root.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(1, 1);
        Rect.pivot = new Vector2(0.5f, 1);
        Rect.sizeDelta = new Vector2(0, FavouriteRowHeight);
        // One row for the field, one for the opacity slider, then the list.
        Rect.anchoredPosition = new Vector2(0, -(FavouriteRowStep * (Index + 2) + 5));

        int Captured = Index;
        FavouriteRow Row = new FavouriteRow { Root = Root };

        // The same checkbox the tool already had, so showing a kept pattern
        // looks like switching the tool on, which is what it does.
        if (EnableToggle != null)
        {
            GameObject Tick = Instantiate(EnableToggle.gameObject, Rect);
            LimThemeManager.Adopt(EnableToggle.gameObject, Tick);
            Tick.name = "Show";
            RectTransform TickRect = Tick.GetComponent<RectTransform>();
            TickRect.anchorMin = new Vector2(0, 1);
            TickRect.anchorMax = new Vector2(0, 1);
            TickRect.pivot = new Vector2(0, 1);
            TickRect.anchoredPosition = new Vector2(4, 0);
            TickRect.sizeDelta = new Vector2(FavouriteButtonSize, FavouriteRowHeight);
            Row.Tick = Tick.GetComponent<Toggle>();
            if (Row.Tick != null)
            {
                // A fresh event: the clone inherited the tool's own toggle.
                Row.Tick.onValueChanged = new Toggle.ToggleEvent();
                Row.Tick.onValueChanged.AddListener((bool On) => { OnFavouriteTicked(Captured, On); });
            }
            AddHint(Tick, "Angleline_UseFavourite");
        }

        CreateSquareButton(Rect, "Forget", "X", LimIcons.Delete, 39, 0, false, "Angleline_DeleteFavourite",
                           () => { DeleteFavourite(Captured); });
        CreateSquareButton(Rect, "Edit", "...", LimIcons.Edit, 74, 0, false, "Angleline_EditFavourite",
                           () => { EditFavourite(Captured); });

        Text Pattern = CreateLabel(Rect, Favourites[Index], TextAnchor.MiddleLeft);
        if (Pattern != null)
        {
            Pattern.rectTransform.offsetMin = new Vector2(109, 0);
            Pattern.rectTransform.offsetMax = new Vector2(-5, 0);
        }
        return Row;
    }

    /// <summary>
    /// The tool can also be switched off by its own checkbox, or by anything
    /// else that sets Enable; the rows follow, so a tick never stays lit
    /// beside a ring with no lines on it.
    ///
    /// The list itself is watched as well. LimSystem reads the preferences
    /// file in its own Start and replaces the whole preferences object, which
    /// can happen after this tool has already built its rows: without this
    /// the kept patterns only reappeared after adding another one, which is
    /// why they seemed to be forgotten between sessions.
    /// </summary>
    private void LateUpdate()
    {
        if (_FavouriteRows.Count != Favourites.Count) RebuildFavouriteRows();
        if (_LastEnable == Enable) return;
        _LastEnable = Enable;
        RefreshTicks();
    }

    private void OnFavouriteTicked(int Index, bool On)
    {
        if (_SuppressTickEvents) return;
        if (On) ShowFavourite(Index);
        else HideFavourites();
    }

    /// <summary>
    /// Puts a kept pattern on the ring: it goes into the field, is read from
    /// there as if it had been typed, and the tool is switched on.
    /// </summary>
    private void ShowFavourite(int Index)
    {
        if (Index < 0 || Index >= Favourites.Count) return;
        _ActiveFavourite = Index;
        if (AnglelineInputField != null)
        {
            AnglelineInputField.text = Favourites[Index];
            OnAnglelineInputFieldChange();
        }
        Enable = true;
        RefreshTicks();
    }

    private void HideFavourites()
    {
        _ActiveFavourite = -1;
        Enable = false;
        RefreshTicks();
    }

    private void DeleteFavourite(int Index)
    {
        if (Index < 0 || Index >= Favourites.Count) return;
        if (_PendingDelete != Index || Time.unscaledTime - _PendingDeleteTime > ForgetClickSeconds)
        {
            _PendingDelete = Index;
            _PendingDeleteTime = Time.unscaledTime;
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_ConfirmForget"]);
            return;
        }
        _PendingDelete = -1;

        if (_EditingFavourite == Index) _EditingFavourite = -1;
        else if (_EditingFavourite > Index) _EditingFavourite--;

        bool WasShowing = _ActiveFavourite == Index;
        Favourites.RemoveAt(Index);
        if (WasShowing) { _ActiveFavourite = -1; Enable = false; }
        else if (_ActiveFavourite > Index) _ActiveFavourite--;
        RebuildFavouriteRows();
    }

    /// <summary>
    /// Sends a kept pattern back to the field to be changed. The plus button
    /// then saves over that entry instead of adding another one.
    /// </summary>
    private void EditFavourite(int Index)
    {
        if (Index < 0 || Index >= Favourites.Count) return;
        _EditingFavourite = Index;
        if (AnglelineInputField == null) return;
        AnglelineInputField.text = Favourites[Index];
        OnAnglelineInputFieldChange();
    }

    /// <summary>
    /// One tick at a time, and none of them ticked while the tool is off,
    /// however it came to be off.
    /// </summary>
    private void RefreshTicks()
    {
        if (!Enable) _ActiveFavourite = -1;
        _SuppressTickEvents = true;
        for (int i = 0; i < _FavouriteRows.Count; ++i)
        {
            Toggle Tick = _FavouriteRows[i].Tick;
            if (Tick != null) Tick.isOn = i == _ActiveFavourite;
        }
        _SuppressTickEvents = false;
    }
}
