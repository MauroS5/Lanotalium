using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Favourite Groups tool: patterns of notes and patterns of motions,
/// kept between sessions and put back with a click.
///
/// Ctrl+F, or the plus button, saves whatever is selected. The patterns are
/// listed in two runs, Notes first and Motions after, each row carrying the
/// pattern's name and three buttons: use it, rename it, forget it. Forgetting
/// takes two clicks, so a stray one costs nothing.
///
/// Using a pattern of notes raises the paste preview, the same ghost a copied
/// selection raises, and a click in the tuner drops it. Using a pattern of
/// motions lays them down at the playhead, because the timeline has no such
/// preview.
///
/// Like the other tools added since, this one is built from a copy of the
/// Angleline tool at runtime and the scene file carries nothing of it.
/// </summary>
public class LimFavouriteGroupsManager : MonoBehaviour
{
    private const float RowHeight = 30f;
    private const float RowStep = 30f;
    private const float ButtonSize = 26f;
    private const float ForgetClickSeconds = 0.4f;

    public LimCreatorToolBase ToolBase;
    /// <summary>
    /// This tool's own body. It has to be handed over rather than worked out
    /// from the template field, which belongs to the Angleline tool: asking
    /// the template for its parent built every row inside that other tool,
    /// where they were promptly clipped out of sight.
    /// </summary>
    public RectTransform ContentRect;
    public Text LabelText, NotesHeader, MotionsHeader;
    public InputField NameTemplate;
    public Toggle TickTemplate;

    private LimOperationManager _OperationManager;
    private readonly List<GameObject> _Rows = new List<GameObject>();
    private int _BuiltFor = -1;
    private int _PendingForget = -1;
    private float _PendingForgetTime = -1;

    private static List<Lanotalium.Editor.FavouritePattern> Patterns
    {
        get
        {
            if (LimSystem.Preferences.FavouritePatterns == null)
                LimSystem.Preferences.FavouritePatterns = new List<Lanotalium.Editor.FavouritePattern>();
            return LimSystem.Preferences.FavouritePatterns;
        }
    }

    public void Setup(LimOperationManager OperationManager)
    {
        _OperationManager = OperationManager;
    }

    public void SetTexts()
    {
        if (LabelText != null) LabelText.text = LimLanguageManager.TextDict["Window_Creator_Favourites"];
        if (NotesHeader != null) NotesHeader.text = LimLanguageManager.TextDict["Window_Creator_Favourites_Notes"];
        if (MotionsHeader != null) MotionsHeader.text = LimLanguageManager.TextDict["Window_Creator_Favourites_Motions"];
    }

    /// <summary>
    /// The list is watched rather than only rebuilt on demand: the
    /// preferences file is read after this tool has already been built, and
    /// Ctrl+F adds patterns from outside it.
    /// </summary>
    private void Update()
    {
        if (_BuiltFor != Patterns.Count) Rebuild();
    }

    public void AddSelection()
    {
        if (_OperationManager == null) return;
        if (_OperationManager.SaveSelectionAsFavourite() != null) Rebuild();
    }

    public void Rebuild()
    {
        foreach (GameObject Row in _Rows) if (Row != null) Destroy(Row);
        _Rows.Clear();
        if (NameTemplate == null || ContentRect == null) return;

        float Y = -5;
        Y = BuildSection(ContentRect, Y, false, NotesHeader);
        Y = BuildSection(ContentRect, Y, true, MotionsHeader);

        _BuiltFor = Patterns.Count;
        if (ToolBase != null) ToolBase.Height = 30 - Y + 5;
    }

    /// <summary>
    /// One run of the list: its heading, then every pattern of that kind.
    /// Answers with the height left after it.
    /// </summary>
    private float BuildSection(RectTransform Parent, float Y, bool Motions, Text Header)
    {
        if (Header != null)
        {
            Header.rectTransform.anchoredPosition = new Vector2(8, Y);
            Header.rectTransform.sizeDelta = new Vector2(200, RowHeight);
        }
        Y -= RowStep;

        for (int i = 0; i < Patterns.Count; ++i)
        {
            if (Patterns[i].HoldsMotions != Motions) continue;
            _Rows.Add(BuildRow(Parent, Y, i));
            Y -= RowStep;
        }
        return Y;
    }

    private GameObject BuildRow(RectTransform Parent, float Y, int Index)
    {
        Lanotalium.Editor.FavouritePattern Pattern = Patterns[Index];
        GameObject Root = new GameObject("Pattern" + Index, typeof(RectTransform));
        RectTransform Rect = Root.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(1, 1);
        Rect.pivot = new Vector2(0.5f, 1);
        Rect.sizeDelta = new Vector2(0, RowHeight);
        Rect.anchoredPosition = new Vector2(0, Y);

        // The name is an editable field kept locked, so renaming is the same
        // control rather than a second one appearing from nowhere.
        InputField Name = Instantiate(NameTemplate.gameObject, Rect).GetComponent<InputField>();
        LimThemeManager.Adopt(NameTemplate.gameObject, Name.gameObject);
        Name.name = "Name";
        StripHints(Name.gameObject);
        RectTransform NameRect = Name.GetComponent<RectTransform>();
        NameRect.anchorMin = new Vector2(0, 1);
        NameRect.anchorMax = new Vector2(1, 1);
        NameRect.pivot = new Vector2(0.5f, 1);
        NameRect.offsetMin = new Vector2(16, -RowHeight + 2);
        NameRect.offsetMax = new Vector2(-(ButtonSize * 3 + 14), -2);
        Name.onValueChanged = new InputField.OnChangeEvent();
        Name.onEndEdit = new InputField.SubmitEvent();
        Name.text = Pattern.Name;
        Name.interactable = true;
        Name.onEndEdit.AddListener((string Value) =>
        {
            Pattern.Name = string.IsNullOrEmpty(Value.Trim()) ? Pattern.Name : Value.Trim();
            Name.text = Pattern.Name;
        });

        int Captured = Index;
        CreateButton(Rect, "Use", LimIcons.Copy, "Favourites_Use", 3, () => { Use(Captured); });
        CreateButton(Rect, "Rename", LimIcons.Edit, "Favourites_Rename", 2, () => { BeginRename(Name); });
        CreateButton(Rect, "Forget", LimIcons.Delete, "Favourites_Forget", 1, () => { Forget(Captured); });
        return Root;
    }

    /// <summary>
    /// Slot counts back from the right hand edge: 1 is the last button.
    /// </summary>
    private void CreateButton(RectTransform Parent, string Name, string Icon, string HintKey, int Slot, UnityEngine.Events.UnityAction OnClick)
    {
        GameObject Holder = new GameObject(Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform Rect = Holder.GetComponent<RectTransform>();
        Rect.SetParent(Parent, false);
        Rect.anchorMin = new Vector2(1, 1);
        Rect.anchorMax = new Vector2(1, 1);
        Rect.pivot = new Vector2(1, 1);
        Rect.anchoredPosition = new Vector2(-4 - (Slot - 1) * (ButtonSize + 2), -2);
        Rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        Image Face = Holder.GetComponent<Image>();
        Image Reference = TickTemplate != null ? TickTemplate.targetGraphic as Image : null;
        if (Reference != null)
        {
            Face.sprite = Reference.sprite;
            Face.type = Reference.type;
            Face.color = Reference.color;
        }
        Button Btn = Holder.GetComponent<Button>();
        Btn.targetGraphic = Face;
        Btn.onClick.AddListener(OnClick);

        LimIcons.TryPlace(Rect, Icon, new Color(0.15f, 0.15f, 0.15f), 4f);
        AddHint(Holder, HintKey);
    }

    private void AddHint(GameObject Target, string HintKey)
    {
        LimMouseOverHint Hint = Target.AddComponent<LimMouseOverHint>();
        Hint.HintTextDictKey = HintKey;
        if (NameTemplate != null && NameTemplate.textComponent != null) Hint.Font = NameTemplate.textComponent.font;
    }

    private static void StripHints(GameObject Clone)
    {
        foreach (LimMouseOverHint Hint in Clone.GetComponentsInChildren<LimMouseOverHint>(true)) DestroyImmediate(Hint);
    }

    private void Use(int Index)
    {
        if (_OperationManager == null || Index < 0 || Index >= Patterns.Count) return;
        _OperationManager.UseFavourite(Patterns[Index]);
    }

    private static void BeginRename(InputField Name)
    {
        Name.Select();
        Name.ActivateInputField();
    }

    /// <summary>
    /// Writes the angleline patterns and the saved groups to one file, which
    /// is the whole point: a set of patterns is worth passing around.
    /// </summary>
    public void Export()
    {
        Lanotalium.Editor.FavouriteExport Bundle = new Lanotalium.Editor.FavouriteExport();
        if (LimSystem.Preferences.AnglelineFavourites != null) Bundle.Anglelines.AddRange(LimSystem.Preferences.AnglelineFavourites);
        Bundle.Patterns.AddRange(Patterns);

        string Path = WindowsDialogUtility.SaveFileDialog(LimLanguageManager.TextDict["Favourites_File_Title"],
                                                          LimLanguageManager.TextDict["Favourites_File_Filter"], "Favourites.json");
        if (string.IsNullOrEmpty(Path)) return;
        // The dialog hands back exactly what was typed, so a name without an
        // extension would be saved as a file Windows cannot open by itself.
        if (!Path.ToLower().EndsWith(".json")) Path += ".json";
        try
        {
            System.IO.File.WriteAllText(Path, Newtonsoft.Json.JsonConvert.SerializeObject(Bundle));
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_Exported"]);
        }
        catch (System.Exception)
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_FileFailed"]);
        }
    }

    /// <summary>
    /// Reads such a file and adds what it holds to what is already here.
    /// Nothing is replaced: an import that wiped the list would be a nasty
    /// surprise, and anything unwanted can be deleted a row at a time.
    /// </summary>
    public void Import()
    {
        string Path = WindowsDialogUtility.OpenFileDialog(LimLanguageManager.TextDict["Favourites_File_Title"],
                                                          LimLanguageManager.TextDict["Favourites_File_Filter"], null);
        if (string.IsNullOrEmpty(Path)) return;
        Lanotalium.Editor.FavouriteExport Bundle;
        try
        {
            Bundle = Newtonsoft.Json.JsonConvert.DeserializeObject<Lanotalium.Editor.FavouriteExport>(System.IO.File.ReadAllText(Path));
        }
        catch (System.Exception)
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_FileFailed"]);
            return;
        }
        if (Bundle == null)
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_FileFailed"]);
            return;
        }

        if (Bundle.Anglelines != null)
        {
            if (LimSystem.Preferences.AnglelineFavourites == null) LimSystem.Preferences.AnglelineFavourites = new List<string>();
            foreach (string Pattern in Bundle.Anglelines)
                if (!LimSystem.Preferences.AnglelineFavourites.Contains(Pattern)) LimSystem.Preferences.AnglelineFavourites.Add(Pattern);
        }
        if (Bundle.Patterns != null)
        {
            foreach (Lanotalium.Editor.FavouritePattern Pattern in Bundle.Patterns)
                if (Pattern != null && Pattern.Count > 0) Patterns.Add(Pattern);
        }
        LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_Imported"]);
        Rebuild();
    }

    /// <summary>
    /// Two clicks to forget one: the first arms it, the second within a
    /// moment carries it out.
    /// </summary>
    private void Forget(int Index)
    {
        if (Index < 0 || Index >= Patterns.Count) return;
        if (_PendingForget == Index && Time.unscaledTime - _PendingForgetTime <= ForgetClickSeconds)
        {
            Patterns.RemoveAt(Index);
            _PendingForget = -1;
            Rebuild();
            return;
        }
        _PendingForget = Index;
        _PendingForgetTime = Time.unscaledTime;
        LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_ConfirmForget"]);
    }
}
