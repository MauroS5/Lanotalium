using UnityEngine;

/// <summary>
/// Shift and the wheel over the Tuner window walk the chart beatline by
/// beatline: up goes forward, down goes back.
///
/// It is the same step Ctrl with the arrows takes in the Media Player, put on
/// the hand that is already on the mouse: while placing notes on the ring the
/// other hand is rarely on the arrow keys, and reaching for them to advance
/// one beat is what breaks the rhythm of charting.
///
/// The raw wheel delta is read rather than the smoothed axis every other
/// wheel gesture here uses, because this is a step and not a speed: the
/// smoothed axis keeps reporting movement for a frame or two after a notch
/// and would walk two or three beatlines for one turn of the wheel.
/// </summary>
public partial class LimOperationManager
{
    public void DetectBeatlineWheel()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;
        if (TunerManager.MediaPlayerManager == null) return;
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;

        float Wheel = Input.mouseScrollDelta.y;
        if (Wheel == 0) return;
        if (TunerWindowRect == null) return;
        if (!LimMousePosition.IsMouseOverWindow(TunerWindowRect)) return;

        ComponentBpmManager Bpm = InspectorManager != null ? InspectorManager.ComponentBpm : null;
        if (Bpm == null) return;

        float Target = Bpm.FindPrevOrNextBeatline(TunerManager.ChartTime, Wheel > 0);
        TunerManager.MediaPlayerManager.Time = Mathf.Clamp(Target, 0, TunerManager.MediaPlayerManager.Length);
    }
}
