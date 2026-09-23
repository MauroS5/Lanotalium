using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dresses an EasyRequest form that asks for a colour as a HEX code: a swatch
/// of the typed colour left of the field, and below the rows a colour picker
/// with a button that writes the picked colour into the field. The form is
/// shared by every request, so all of it is taken away again when the form
/// closes. Used by the Lanota Header's custom difficulty.
/// </summary>
public static class LimColourForm
{
    private const float PickerSide = 230f, StripWidth = 24f, PickerGap = 12f, ButtonHeight = 30f, ButtonWidth = 180f;

    /// <summary>Runs Form on the shared EasyRequest manager with the colour tools added to its ColourField-th text row.</summary>
    public static IEnumerator Run<T>(MonoBehaviour Host, EasyRequest.Request<T> Form, string Title, int ColourField) where T : class, new()
    {
        EasyRequest.EasyRequestManager Manager = EasyRequest.EasyRequestManager.Instance;
        // Started here rather than yielded, so the form is built by the time
        // this returns and can be added to before it is first drawn.
        Coroutine Asking = Manager.StartCoroutine(Manager.Request(Form, Title));
        List<GameObject> Added = new List<GameObject>();
        try { Decorate(Manager, ColourField, Added); }
        catch (System.Exception E) { Debug.LogException(E); }
        yield return Asking;
        foreach (GameObject Made in Added) if (Made != null) Object.Destroy(Made);
    }

    private static void Decorate(EasyRequest.EasyRequestManager Manager, int ColourField, List<GameObject> Added)
    {
        EasyRequest.RequestString[] Rows = Manager.RequestListContent.GetComponentsInChildren<EasyRequest.RequestString>(true);
        if (ColourField >= Rows.Length) return;
        InputField Field = Rows[ColourField].StringInputField;
        RectTransform FieldRect = Field.GetComponent<RectTransform>();
        RectTransform Content = Manager.RequestListContent;
        Canvas.ForceUpdateCanvases();

        // The swatch: square, the field's height, just left of it. The row
        // was made this very frame and has no size yet, so it keeps itself
        // beside the field every frame instead of being placed once.
        GameObject SwatchHolder = new GameObject("ColourSwatch", typeof(RectTransform));
        SwatchHolder.layer = Field.gameObject.layer;
        RectTransform Swatch = SwatchHolder.GetComponent<RectTransform>();
        Swatch.SetParent(FieldRect.parent, false);
        Swatch.pivot = new Vector2(0.5f, 0.5f);
        SwatchHolder.AddComponent<LimSitsLeftOf>().Target = FieldRect;
        Image Chip = SwatchHolder.AddComponent<Image>();
        Chip.raycastTarget = false;
        Outline Edge = SwatchHolder.AddComponent<Outline>();
        Edge.effectColor = new Color(0, 0, 0, 0.7f);
        Edge.effectDistance = new Vector2(1, -1);
        Added.Add(SwatchHolder);
        UnityEngine.Events.UnityAction<string> ShowTyped = (string Typed) =>
        {
            Color Tint;
            bool Ok = LimTimeGroups.TryParseTint(Typed, out Tint);
            Chip.color = Ok ? Tint : new Color(0, 0, 0, 0);
        };
        Field.onValueChanged.AddListener(ShowTyped);
        ShowTyped(Field.text);

        // The picker, centred below the rows, and its button below it.
        float Top = Content.sizeDelta.y + 12;
        GameObject PickerHolder = new GameObject("ColourPicker", typeof(RectTransform));
        PickerHolder.layer = Content.gameObject.layer;
        RectTransform PickerRect = PickerHolder.GetComponent<RectTransform>();
        PickerRect.SetParent(Content, false);
        PickerRect.anchorMin = PickerRect.anchorMax = new Vector2(0.5f, 1);
        PickerRect.pivot = new Vector2(0.5f, 1);
        PickerRect.sizeDelta = new Vector2(PickerSide + PickerGap + StripWidth, PickerSide);
        PickerRect.anchoredPosition = new Vector2(0, -Top);
        Added.Add(PickerHolder);
        LimColourPicker Picker = LimColourPicker.Create(PickerRect, StripWidth, PickerGap);
        Color Start;
        if (LimTimeGroups.TryParseTint(Field.text, out Start)) Picker.Value = Start;

        Button Source = Manager.ConfirmText != null ? Manager.ConfirmText.GetComponentInParent<Button>() : null;
        if (Source != null)
        {
            GameObject Apply = Object.Instantiate(Source.gameObject, Content);
            LimThemeManager.Adopt(Source.gameObject, Apply);
            Apply.name = "ApplyColour";
            foreach (UnityEngine.EventSystems.EventTrigger Trigger in Apply.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)) Object.Destroy(Trigger);
            RectTransform ApplyRect = Apply.GetComponent<RectTransform>();
            ApplyRect.anchorMin = ApplyRect.anchorMax = new Vector2(0.5f, 1);
            ApplyRect.pivot = new Vector2(0.5f, 1);
            ApplyRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            ApplyRect.anchoredPosition = new Vector2(0, -(Top + PickerSide + 12));
            Text Words = Apply.GetComponentInChildren<Text>(true);
            if (Words != null) Words.text = LimLanguageManager.TextDict["Header_ApplyColour"];
            Button Press = Apply.GetComponent<Button>();
            Press.onClick = new Button.ButtonClickedEvent();
            Press.onClick.AddListener(() => { Field.text = "#" + ColorUtility.ToHtmlStringRGB(Picker.Value); });
            Added.Add(Apply);
            Content.sizeDelta = new Vector2(Content.sizeDelta.x, Top + PickerSide + 12 + ButtonHeight + 12);
        }
        else Content.sizeDelta = new Vector2(Content.sizeDelta.x, Top + PickerSide + 12);
    }
}

/// <summary>Keeps a square as tall as Target just left of it, with a small gap.</summary>
public class LimSitsLeftOf : MonoBehaviour
{
    public RectTransform Target;
    public float Gap = 8f;

    private void LateUpdate()
    {
        if (Target == null) return;
        RectTransform Rect = transform as RectTransform;
        float Side = Target.rect.height;
        if (Rect.sizeDelta.x != Side) Rect.sizeDelta = new Vector2(Side, Side);
        Vector3 Wanted = Target.TransformPoint(new Vector3(Target.rect.xMin - Gap - Side / 2, Target.rect.center.y, 0));
        if (Rect.position != Wanted) Rect.position = Wanted;
    }
}
