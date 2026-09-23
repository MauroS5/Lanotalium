using UnityEngine;

/// <summary>
/// Adding, removing, checking and editing transparency motions: the same set
/// of operations Rotation has, one for one, so the fourth row answers to
/// everything the other three do. The value is the one difference: it is
/// kept between 0 and 100, the ring's range from gone to opaque.
/// </summary>
public partial class LimOperationManager
{
    public int FindTransparencyIndexByInstanceId(int InstanceId)
    {
        int Index = 0;
        if (TunerManager.CameraManager.Transparency == null) return -1;
        foreach (Lanotalium.Chart.LanotaCameraTrs Trs in TunerManager.CameraManager.Transparency)
        {
            if (Trs.InstanceId == InstanceId) return Index;
            Index++;
        }
        return -1;
    }
    public bool AddTransparency(Lanotalium.Chart.LanotaCameraTrs Trs, bool AutoDuration = true, bool SaveOperation = true, bool CallSelectNothing = true)
    {
        if (TunerManager.CameraManager.Transparency == null) return false;
        if (CheckNewTransparencyTimeExisted(Trs))
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Motion_TimeExisted"]);
            return false;
        }
        Trs.ctp = Mathf.Clamp(Trs.ctp, 0, LimCameraManager.OpaqueTransparency);
        TunerManager.CameraManager.Transparency.Add(Trs);
        TunerManager.CameraManager.SortTransparencyList();
        TimeLineManager.InstantiateSingleTransparency(Trs);
        if (CheckTransparencyInPreviousDuration(Trs))
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Motion_InPrevious"]);
            DeleteTransparency(Trs);
            return false;
        }
        if (AutoDuration) Trs.Duration = GetNewTransparencyDuration(Trs);
        else if (!AutoDuration)
        {
            if (!CheckTransparencyDurationValid(Trs, Trs.Duration))
            {
                LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Motion_DurationOverflow"]);
                DeleteTransparency(Trs);
                return false;
            }
        }
        TimeLineManager.InstantiateSingleTransparency(Trs);
        if (CallSelectNothing) SelectNothing();
        InspectorManager.ComponentMotion.SetMode(Lanotalium.Editor.ComponentMotionMode.Transparency, FindTransparencyIndexByInstanceId(Trs.InstanceId));
        return true;
    }
    public void DeleteTransparency(Lanotalium.Chart.LanotaCameraTrs Trs, bool SaveOperation = true)
    {
        Destroy(Trs.TimeLineGameObject);
        if (TunerManager.CameraManager.Transparency != null) TunerManager.CameraManager.Transparency.Remove(Trs);
    }
    public void SetTransparencyTime(Lanotalium.Chart.LanotaCameraTrs Trs, float Time, bool SaveOperation = true)
    {
        Trs.Time = Time;
        TimeLineManager.InstantiateSingleTransparency(Trs);
    }
    public void SetTransparencyDuration(Lanotalium.Chart.LanotaCameraTrs Trs, float Duration, bool SaveOperation = true)
    {
        Trs.Duration = Duration;
        TimeLineManager.InstantiateSingleTransparency(Trs);
    }
    public void SetTransparencyEase(Lanotalium.Chart.LanotaCameraTrs Trs, int Ease, bool SaveOperation = true)
    {
        Trs.cfmi = Ease;
    }
    public void SetTransparencyValue(Lanotalium.Chart.LanotaCameraTrs Trs, float Value, bool SaveOperation = true)
    {
        Trs.ctp = Mathf.Clamp(Value, 0, LimCameraManager.OpaqueTransparency);
    }
    public float GetNewTransparencyDuration(Lanotalium.Chart.LanotaCameraTrs Trs)
    {
        int Index = FindTransparencyIndexByInstanceId(Trs.InstanceId);
        if (Index == TunerManager.CameraManager.Transparency.Count - 1) return LimSystem.ChartContainer.ChartMusic.Length - Trs.Time > 1 ? 1 : LimSystem.ChartContainer.ChartMusic.Length - Trs.Time;
        else return TunerManager.CameraManager.Transparency[Index + 1].Time - TunerManager.CameraManager.Transparency[Index].Time > 1 ? 1 : TunerManager.CameraManager.Transparency[Index + 1].Time - TunerManager.CameraManager.Transparency[Index].Time;
    }
    public bool CheckTransparencyInPreviousDuration(Lanotalium.Chart.LanotaCameraTrs Trs)
    {
        if (LimSystem.Preferences.Unsafe) return false;
        int Index = FindTransparencyIndexByInstanceId(Trs.InstanceId);
        if (Index <= 0) return false;
        return TunerManager.CameraManager.Transparency[Index - 1].Time + TunerManager.CameraManager.Transparency[Index - 1].Duration > Trs.Time;
    }
    public bool CheckNewTransparencyTimeExisted(Lanotalium.Chart.LanotaCameraTrs Trs)
    {
        if (LimSystem.Preferences.Unsafe) return false;
        foreach (Lanotalium.Chart.LanotaCameraTrs Other in TunerManager.CameraManager.Transparency)
        {
            if (Other.Time == Trs.Time) return true;
        }
        return false;
    }
    public bool CheckTransparencyTimeValid(Lanotalium.Chart.LanotaCameraTrs Trs, float Time)
    {
        if (LimSystem.Preferences.Unsafe) return true;
        int Index = FindTransparencyIndexByInstanceId(Trs.InstanceId);
        if (Index == -1) return false;
        if (Index + 1 <= TunerManager.CameraManager.Transparency.Count - 1)
        {
            if (Time + Trs.Duration > TunerManager.CameraManager.Transparency[Index + 1].Time) return false;
        }
        else
        {
            if (Time > LimSystem.ChartContainer.ChartMusic.Length) return false;
        }
        if (Index - 1 >= 0)
        {
            if (TunerManager.CameraManager.Transparency[Index - 1].Time + TunerManager.CameraManager.Transparency[Index - 1].Duration > Time) return false;
        }
        else
        {
            if (Time < 0) return false;
        }
        return true;
    }
    public bool CheckTransparencyDurationValid(Lanotalium.Chart.LanotaCameraTrs Trs, float Duration)
    {
        if (LimSystem.Preferences.Unsafe) return true;
        if (Duration < 0.0001f) return false;
        int Index = FindTransparencyIndexByInstanceId(Trs.InstanceId);
        if (Index == -1) return false;
        if (Index + 1 <= TunerManager.CameraManager.Transparency.Count - 1)
        {
            if (Trs.Time + Duration > TunerManager.CameraManager.Transparency[Index + 1].Time) return false;
        }
        else
        {
            if (Trs.Time > LimSystem.ChartContainer.ChartMusic.Length) return false;
        }
        if (Index - 1 >= 0)
        {
            if (TunerManager.CameraManager.Transparency[Index - 1].Time + TunerManager.CameraManager.Transparency[Index - 1].Duration > Trs.Time) return false;
        }
        else
        {
            if (Trs.Time < 0) return false;
        }
        return true;
    }
}
