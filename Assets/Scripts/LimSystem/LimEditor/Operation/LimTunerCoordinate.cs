using UnityEngine;

/// <summary>
/// Converts a point on the tuner viewport into chart coordinates
/// (timing + degree) and back.
///
/// This is the single source of truth for that math. It was originally
/// written inside LimClickToCreateManager as private helpers; it now
/// lives here so that click-to-create and drag-to-move cannot drift
/// apart. LimClickToCreateManager delegates to these methods.
/// </summary>
public static class LimTunerCoordinate
{
    /// <summary>
    /// Mouse position translated into the tuner camera's screen space.
    /// </summary>
    public static Vector3 MouseToTunerScreen(RectTransform TunerWindowRect)
    {
        Vector3 MousePosition = LimMousePosition.MousePosition;
        return new Vector3
        {
            x = MousePosition.x - TunerWindowRect.anchoredPosition.x,
            y = TunerWindowRect.sizeDelta.y + (MousePosition.y - TunerWindowRect.anchoredPosition.y)
        };
    }

    /// <summary>
    /// Tuner screen space projected onto the chart plane (y = 0).
    /// </summary>
    public static Vector3 TunerScreenToWorld(Vector3 TunerPosition, Camera TunerCamera)
    {
        return TunerCamera.ScreenToWorldPoint(new Vector3(TunerPosition.x, TunerPosition.y, -TunerCamera.transform.position.y));
    }

    public static float UnEasedPercent(float Percent)
    {
        return 1f + 0.1f * (Mathf.Log(Percent) / Mathf.Log(2));
    }

    public static float EasedPercent(float Percent)
    {
        return Mathf.Pow(2, 10 * (Percent / 100 - 1)) * 100;
    }

    /// <summary>
    /// Absolute on-screen degree of a point on the chart plane. This is
    /// what the player sees; it still includes the camera rotation.
    /// </summary>
    public static float ScreenDegree(Vector3 Position)
    {
        return 180f - Mathf.Atan2(-Position.x, Position.z) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Walks the scroll-speed segments to turn a distance-from-centre
    /// percentage into a chart timing. Returns NaN when the point falls
    /// outside the song.
    /// </summary>
    public static float PercentToTime(float Percent, LimTunerManager TunerManager)
    {
        float EndPercent = Percent;
        int StartScroll = 0, EndScroll = 0;
        float StartPercent = 100.0f;
        for (int i = 0; i < TunerManager.ScrollManager.Scroll.Count - 1; ++i)
        {
            if (TunerManager.ChartTime >= TunerManager.ScrollManager.Scroll[i].Time && TunerManager.ChartTime < TunerManager.ScrollManager.Scroll[i + 1].Time) StartScroll = i;
        }
        if (TunerManager.ChartTime >= TunerManager.ScrollManager.Scroll[TunerManager.ScrollManager.Scroll.Count - 1].Time) StartScroll = TunerManager.ScrollManager.Scroll.Count - 1;
        EndScroll = TunerManager.ScrollManager.Scroll.Count - 1;
        int BreakLocation = -1;
        float Delta = 0, EndTime = 0;
        for (int i = StartScroll; i <= EndScroll; ++i)
        {
            if (StartScroll != EndScroll)
            {
                if (i == StartScroll)
                {
                    Delta = (TunerManager.ScrollManager.Scroll[i + 1].Time - TunerManager.ChartTime) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
                    if (StartPercent - Delta < EndPercent) { BreakLocation = i; break; }
                    StartPercent -= Delta;
                }
                else if (i != EndScroll && i != StartScroll)
                {
                    Delta = (TunerManager.ScrollManager.Scroll[i + 1].Time - TunerManager.ScrollManager.Scroll[i].Time) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
                    if (StartPercent - Delta < EndPercent) { BreakLocation = i; break; }
                    StartPercent -= Delta;
                }
                else if (i == EndScroll)
                {
                    Delta = (TunerManager.MediaPlayerManager.Length - TunerManager.ScrollManager.Scroll[i].Time) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
                    if (StartPercent - Delta < EndPercent) { BreakLocation = i; break; }
                    StartPercent -= Delta;
                }
            }
        }
        if (StartScroll == EndScroll)
        {
            Delta = (StartPercent - EndPercent);
            EndTime = Delta / (TunerManager.ScrollManager.Scroll[EndScroll].Speed * 10 * TunerManager.ChartPlaySpeed) + TunerManager.ChartTime;
            if (EndTime > TunerManager.MediaPlayerManager.Length) return float.NaN;
            return EndTime;
        }
        if (BreakLocation == StartScroll)
        {
            Delta = (StartPercent - EndPercent);
            EndTime = Delta / (TunerManager.ScrollManager.Scroll[BreakLocation].Speed * 10 * TunerManager.ChartPlaySpeed) + TunerManager.ChartTime;
        }
        else if (BreakLocation != -1)
        {
            Delta = (StartPercent - EndPercent);
            EndTime = Delta / (TunerManager.ScrollManager.Scroll[BreakLocation].Speed * 10 * TunerManager.ChartPlaySpeed) + TunerManager.ScrollManager.Scroll[BreakLocation].Time;
        }
        else if (BreakLocation == -1) return float.NaN;
        return EndTime;
    }

    /// <summary>
    /// Full mouse -> chart conversion. Returns false when the pointer is
    /// outside the usable ring or past the end of the song, in which case
    /// the out values are meaningless.
    ///
    /// Degree is the ABSOLUTE on-screen degree: pass it to
    /// SetTapNoteDegree / SetHoldNoteDegree with isAbsolute = true so the
    /// camera rotation at the note's own timing gets removed.
    /// </summary>
    public static bool TryGetChartPointAtMouse(RectTransform TunerWindowRect, Camera TunerCamera, LimTunerManager TunerManager,
                                               out float Time, out float Degree)
    {
        Time = 0; Degree = 0;
        if (TunerWindowRect == null || TunerCamera == null || TunerManager == null) return false;
        if (TunerManager.ScrollManager == null || TunerManager.ScrollManager.Scroll == null || TunerManager.ScrollManager.Scroll.Count == 0) return false;

        Vector3 TunerPosition = MouseToTunerScreen(TunerWindowRect);
        Vector3 Position = TunerScreenToWorld(TunerPosition, TunerCamera);
        float Distance = Vector3.Distance(Position, new Vector3());
        if (Distance > 10 || Distance < 2) return false;

        float Percent = UnEasedPercent(Distance / 10) * 100;
        float CalculatedTime = PercentToTime(Percent, TunerManager);
        if (float.IsNaN(CalculatedTime)) return false;

        Time = CalculatedTime;
        Degree = ScreenDegree(Position);
        return true;
    }

    /// <summary>
    /// How far along the approach a note sitting at the given timing is,
    /// as a 0-100 percentage of the ring radius. 100 is the outer edge.
    /// </summary>
    public static float TimeToMovePercent(float Time, LimTunerManager TunerManager)
    {
        int StartScroll = 0, EndScroll = 0;
        float Percent = 100;
        for (int i = 0; i < TunerManager.ScrollManager.Scroll.Count - 1; ++i)
        {
            if (TunerManager.ChartTime >= TunerManager.ScrollManager.Scroll[i].Time && TunerManager.ChartTime < TunerManager.ScrollManager.Scroll[i + 1].Time) StartScroll = i;
            if (Time >= TunerManager.ScrollManager.Scroll[i].Time && Time < TunerManager.ScrollManager.Scroll[i + 1].Time) EndScroll = i;
        }
        if (TunerManager.ScrollManager.Scroll.Count != 0)
        {
            if (TunerManager.ChartTime >= TunerManager.ScrollManager.Scroll[TunerManager.ScrollManager.Scroll.Count - 1].Time) StartScroll = TunerManager.ScrollManager.Scroll.Count - 1;
            if (Time >= TunerManager.ScrollManager.Scroll[TunerManager.ScrollManager.Scroll.Count - 1].Time) EndScroll = TunerManager.ScrollManager.Scroll.Count - 1;
        }
        for (int i = StartScroll; i <= EndScroll; ++i)
        {
            if (StartScroll == EndScroll) Percent -= (Time - TunerManager.ChartTime) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
            else if (StartScroll != EndScroll)
            {
                if (i == StartScroll) Percent -= (TunerManager.ScrollManager.Scroll[i + 1].Time - TunerManager.ChartTime) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
                else if (i != EndScroll && i != StartScroll) Percent -= (TunerManager.ScrollManager.Scroll[i + 1].Time - TunerManager.ScrollManager.Scroll[i].Time) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
                else if (i == EndScroll) Percent -= (Time - TunerManager.ScrollManager.Scroll[i].Time) * TunerManager.ScrollManager.Scroll[i].Speed * 10 * TunerManager.ChartPlaySpeed;
            }
        }
        Percent = Mathf.Clamp(Percent, 0, 100);
        return Percent;
    }

    /// <summary>
    /// Lays a preview object on the ring exactly the way click-to-create
    /// places its note cursor. Degree is the absolute on-screen degree.
    /// Returns false when the timing falls outside the visible ring, in
    /// which case the caller should hide the object.
    /// </summary>
    public static bool PlacePreview(Transform Preview, float Time, float Degree, LimTunerManager TunerManager)
    {
        if (Preview == null || TunerManager == null) return false;
        float Percent = EasedPercent(TimeToMovePercent(Time, TunerManager));
        if (Percent <= 0.01f) return false;
        Preview.rotation = Quaternion.Euler(new Vector3(90, Degree, 0));
        Preview.position = new Vector3(-Percent / 10 * Mathf.Sin(Degree * Mathf.Deg2Rad), 0, -Percent / 10 * Mathf.Cos(Degree * Mathf.Deg2Rad));
        Preview.localScale = new Vector3(Percent / 100, Percent / 100, 0);
        return true;
    }

}
