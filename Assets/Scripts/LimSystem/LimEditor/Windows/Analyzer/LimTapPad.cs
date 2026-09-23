using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Reports the moment the pointer goes down on it. A tap is timed on the way
/// down, not on release as a Button's click is: how long a finger rests on
/// the mouse varies from tap to tap, and that would read as an uneven beat.
/// </summary>
public class LimTapPad : MonoBehaviour, IPointerDownHandler
{
    public Action OnDown;

    public void OnPointerDown(PointerEventData EventData)
    {
        if (EventData.button != PointerEventData.InputButton.Left) return;
        if (OnDown != null) OnDown();
    }
}
