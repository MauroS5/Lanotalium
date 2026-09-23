using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lanotalium.Editor
{
    /// <summary>
    /// The three looks offered in Preferences. Written to the preferences file
    /// by value, so entries are only ever added at the end.
    /// </summary>
    public enum UiTheme
    {
        Default = 0,
        Dark = 1,
        Light = 2
    }
}

/// <summary>
/// Repaints the editor darker or lighter without redesigning it.
///
/// A whole restyle was tried once on this project and had to be reverted, so
/// this one deliberately touches as little as possible. What it changes is
/// the chrome: the plain grey surfaces the windows are made of, the handles,
/// the toolbars and the panels. Those are the large areas that tire the eyes,
/// and they are the only Images in the scene that carry a flat grey of their
/// own. Everything else, meaning the input fields, the checkmarks, the
/// coloured buttons and every piece of text, is left exactly as it was, so
/// nothing can end up unreadable.
///
/// The light theme has to go one step further: white text and white icons sit
/// on that chrome, and pale chrome would swallow them, so in that theme, and
/// only in that theme, they are darkened to match.
///
/// Colours are recorded the first time they are seen and every repaint starts
/// from the recorded value, never from what is on screen, so switching
/// between themes cannot pile one tint on top of another. The scene is walked
/// once at startup and again only when the theme is changed, rather than
/// every frame: the editor, including the rows added to Preferences, has
/// built its whole interface before the first frame ends. Repainting also
/// writes over colours that are set as the editor is used, a pressed tool
/// button or a selected motion, which is another reason not to do it often.
/// </summary>
public class LimThemeManager : MonoBehaviour
{
    public static LimThemeManager Instance;

    /// <summary>Greys darker than this are already dark; leave them be.</summary>
    private const float ChromeFloor = 0.02f;
    /// <summary>White is the widgets' own colour, not the chrome's.</summary>
    private const float ChromeCeiling = 0.95f;
    /// <summary>How far apart the channels may be and still count as grey.</summary>
    private const float NeutralTolerance = 0.06f;

    private const float DarkFactor = 0.5f;
    /// <summary>Silver rather than white: 0.39 becomes 0.76, 0.59 becomes 0.83.</summary>
    private const float LightBase = 0.62f;
    private const float LightRange = 0.35f;
    /// <summary>What white text and white icons become once the chrome is pale.</summary>
    private static readonly Color LightInk = new Color(0.13f, 0.13f, 0.13f);
    private static readonly Color LightIcon = new Color(0.25f, 0.25f, 0.25f);

    private readonly Dictionary<Graphic, Color> Recorded = new Dictionary<Graphic, Color>();
    private readonly List<Graphic> Forgotten = new List<Graphic>();
    private bool Started, EverPainted;

    /// <summary>
    /// Names uGUI gives the parts of its own controls. An Image called one of
    /// these is a piece of a widget rather than an icon, and keeps its colour
    /// even in the light theme.
    /// </summary>
    private static readonly string[] WidgetParts =
    {
        "Background", "Viewport", "Template", "Fill", "Fill Area", "Handle", "Handle Slide Area",
        "Sliding Area", "Scrollbar", "Scrollbar Horizontal", "Scrollbar Vertical", "Item Background",
        "Checkmark", "Item Checkmark", "Arrow", "Mask"
    };

    public static void Ensure()
    {
        if (Instance != null) return;
        GameObject Holder = new GameObject("LimTheme");
        Instance = Holder.AddComponent<LimThemeManager>();
    }

    /// <summary>Paints everything again, picking up anything built since.</summary>
    public static void Refresh()
    {
        Ensure();
        if (Instance != null) Instance.Apply();
    }

    /// <summary>
    /// Gives a control a colour as if it were the one it was built with, and
    /// paints it for the theme in use. For colours code sets while the editor
    /// is being used, such as a pressed tab or a chosen button: set directly,
    /// they are the default theme's colour whatever theme is on, and the next
    /// change of theme would record them as the control's own.
    /// </summary>
    public static void Paint(Graphic One, Color Original)
    {
        if (One == null) return;
        Ensure();
        if (Instance != null) Instance.Recorded[One] = Original;
        One.color = Repaint(One, Original);
    }

    /// <summary>The colour a control was built with, as recorded; its colour now if it never was.</summary>
    public static Color OriginalOf(Graphic One)
    {
        Color Found;
        if (One != null && Instance != null && Instance.Recorded.TryGetValue(One, out Found)) return Found;
        return One != null ? One.color : Color.white;
    }

    /// <summary>
    /// To be called right after a piece of the interface is cloned, before the
    /// copy is changed: every graphic of the copy takes the colour its
    /// counterpart in the source was recorded with. A copy made after a theme
    /// was painted carries the painted colour, and without this the next
    /// change of theme would record that as its own colour and then paint it
    /// a second time, which is how cloned Creator rows ended up darker than
    /// their neighbours. Nothing happens when the source was never recorded,
    /// which is the case before the first pass, when the copy still has the
    /// colour it was born with and is recorded with it like everything else.
    /// </summary>
    public static void Adopt(GameObject Source, GameObject Clone)
    {
        if (Instance == null || Source == null || Clone == null) return;
        Graphic[] From = Source.GetComponentsInChildren<Graphic>(true);
        Graphic[] To = Clone.GetComponentsInChildren<Graphic>(true);
        if (From.Length != To.Length) return;
        for (int i = 0; i < From.Length; ++i)
        {
            Color Original;
            if (From[i] != null && To[i] != null && Instance.Recorded.TryGetValue(From[i], out Original)) Instance.Recorded[To[i]] = Original;
        }
    }

    private void Update()
    {
        // The first frame is over by now, which is when nearly all of this
        // editor's windows and tools have finished building themselves.
        if (Started) return;
        Started = true;
        Apply();
    }

    private void Apply()
    {
        Forget();
        // A control that turns up later can only be recorded when nothing
        // has been painted yet, or while the default theme is on: in both
        // cases the colour it has now is the colour it was born with. Under
        // a theme, a control cloned from one already painted would be
        // recorded as if its tint were its own colour, and painting it again
        // would tint the tint.
        Collect(!EverPainted || Chosen == Lanotalium.Editor.UiTheme.Default);
        foreach (KeyValuePair<Graphic, Color> Entry in Recorded)
        {
            if (Entry.Key == null) continue;
            Entry.Key.color = Repaint(Entry.Key, Entry.Value);
        }
        EverPainted = true;
    }

    /// <summary>Drops entries whose control has been destroyed since.</summary>
    private void Forget()
    {
        Forgotten.Clear();
        foreach (KeyValuePair<Graphic, Color> Entry in Recorded)
        {
            if (Entry.Key == null) Forgotten.Add(Entry.Key);
        }
        foreach (Graphic Dead in Forgotten) Recorded.Remove(Dead);
    }

    /// <summary>
    /// Records the colour of anything not seen before. What is recorded is
    /// the colour the control has right now, which for a control built since
    /// the last pass is the colour it was born with.
    /// </summary>
    private void Collect(bool Record)
    {
        if (!Record) return;
        GameObject[] Roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject Root in Roots)
        {
            Graphic[] Found = Root.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic One in Found)
            {
                if (One == null || Recorded.ContainsKey(One)) continue;
                Recorded.Add(One, One.color);
            }
        }
    }

    private static Lanotalium.Editor.UiTheme Chosen
    {
        get { return LimSystem.Preferences == null ? Lanotalium.Editor.UiTheme.Default : LimSystem.Preferences.Theme; }
    }

    private static bool IsNeutral(Color Shade)
    {
        float High = Mathf.Max(Shade.r, Mathf.Max(Shade.g, Shade.b));
        float Low = Mathf.Min(Shade.r, Mathf.Min(Shade.g, Shade.b));
        return High - Low <= NeutralTolerance;
    }
    private static bool IsChrome(Color Shade)
    {
        if (Shade.a < 0.05f) return false;
        if (!IsNeutral(Shade)) return false;
        float Value = (Shade.r + Shade.g + Shade.b) / 3f;
        return Value > ChromeFloor && Value < ChromeCeiling;
    }
    private static bool IsWidgetPart(Graphic One)
    {
        string Name = One.gameObject.name;
        for (int i = 0; i < WidgetParts.Length; ++i) if (WidgetParts[i] == Name) return true;
        // A control's own background is whatever it highlights when pressed.
        Selectable Owner = One.GetComponent<Selectable>();
        if (Owner != null && Owner.targetGraphic == One) return true;
        return One.GetComponent<Mask>() != null;
    }

    private static Color Repaint(Graphic One, Color Origin)
    {
        Lanotalium.Editor.UiTheme Theme = Chosen;
        if (Theme == Lanotalium.Editor.UiTheme.Default) return Origin;

        bool Writing = One is Text;
        float Value = (Origin.r + Origin.g + Origin.b) / 3f;

        if (Theme == Lanotalium.Editor.UiTheme.Dark)
        {
            // Text is untouched: it is nearly all white, and white on darker
            // grey only reads better.
            if (Writing || !IsChrome(Origin)) return Origin;
            return new Color(Origin.r * DarkFactor, Origin.g * DarkFactor, Origin.b * DarkFactor, Origin.a);
        }

        if (Writing)
        {
            if (Value <= 0.7f) return Origin;
            return new Color(LightInk.r, LightInk.g, LightInk.b, Origin.a);
        }
        if (IsChrome(Origin))
        {
            return new Color(LightBase + Origin.r * LightRange, LightBase + Origin.g * LightRange,
                             LightBase + Origin.b * LightRange, Origin.a);
        }
        // A white shape that is not part of a control is an icon, and would
        // vanish into pale chrome.
        if (Value > ChromeCeiling && Origin.a > 0.05f && IsNeutral(Origin) && !IsWidgetPart(One))
        {
            return new Color(LightIcon.r, LightIcon.g, LightIcon.b, Origin.a);
        }
        return Origin;
    }
}
