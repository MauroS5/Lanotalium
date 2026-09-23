using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LimMouseOverHint : MonoBehaviour
{
    public string HintTextDictKey;
    public Font Font;
    private EventTrigger Trigger;
    /// <summary>Room left under the pointer when the balloon has to open below it.</summary>
    private const float BalloonPointerGap = 20f;
    private bool isMouseOver = false;
    private bool isGUIInitialized = false;
    private GUIStyle Style;
    private Vector2 Size;
    void Start()
    {
        Trigger = gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry PointerEnter = new EventTrigger.Entry();
        UnityAction<BaseEventData> PointerEnterAction = new UnityAction<BaseEventData>(OnPointerEnter);
        PointerEnter.callback.AddListener(PointerEnterAction);
        PointerEnter.eventID = EventTriggerType.PointerEnter;
        Trigger.triggers.Add(PointerEnter);

        EventTrigger.Entry PointerExit = new EventTrigger.Entry();
        UnityAction<BaseEventData> PointerExitAction = new UnityAction<BaseEventData>(OnPointerExit);
        PointerExit.callback.AddListener(PointerExitAction);
        PointerExit.eventID = EventTriggerType.PointerExit;
        Trigger.triggers.Add(PointerExit);

    }

    private void OnGUI()
    {
        if (!isMouseOver) return;
        if (!isGUIInitialized)
        {
            Style = new GUIStyle(GUI.skin.box);
            Style.font = Font;
            Style.fontSize = 18;
            Style.normal.textColor = new Color(255, 255, 255, 255);
            Style.stretchHeight = true;
            Style.stretchWidth = true;
            Style.alignment = TextAnchor.LowerLeft;
            isGUIInitialized = true;
        }
        Size = Style.CalcSize(new GUIContent(LimLanguageManager.HintDict[HintTextDictKey]));
        GUI.Box(new Rect(BalloonCorner(Input.mousePosition, Size), Size), LimLanguageManager.HintDict[HintTextDictKey], Style);
    }

    /// <summary>
    /// Where the balloon's top left corner goes, in GUI space (y down). It
    /// opens up and to the right of the pointer, as it always has, unless
    /// that would run off the screen: then it opens to the left of the
    /// pointer, and below it at the top of the screen. A balloon wider than
    /// the screen starts at the left edge rather than off it.
    /// </summary>
    private static Vector2 BalloonCorner(Vector2 Mouse, Vector2 Size)
    {
        float X = Mouse.x;
        if (X + Size.x > Screen.width) X = Mouse.x - Size.x;
        if (X < 0) X = 0;
        float Y = Screen.height - Mouse.y - Size.y;
        if (Y < 0) Y = Screen.height - Mouse.y + BalloonPointerGap;
        if (Y + Size.y > Screen.height) Y = Mathf.Max(0, Screen.height - Size.y);
        return new Vector2(X, Y);
    }

    public void OnPointerEnter(BaseEventData data)
    {
        isMouseOver = true;
    }
    public void OnPointerExit(BaseEventData data)
    {
        isMouseOver = false;
    }
    private void OnDisable()
    {
        isMouseOver = false;
    }
}
