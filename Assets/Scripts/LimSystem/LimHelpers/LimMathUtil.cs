using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LimMathUtil
{
    public static bool InRange(float Value, float Min, float Max)
    {
        return (Value >= Min && Value <= Max && Min <= Max) ? true : false;
    }
    /// <summary>
    /// A degree brought back into the circle, 0 up to but not including 360.
    ///
    /// The camera's rotation adds up as a chart goes on: after a handful of
    /// rotation motions it can be several thousand degrees, and a note placed
    /// by pointing at the ring had that whole sum taken off it, so a note
    /// sitting at 90 was written down as -2430. It looked right, because only
    /// the position on the circle is ever drawn, but it read as nonsense.
    /// </summary>
    public static float NormalizeDegree(float Degree)
    {
        Degree = Degree % 360;
        if (Degree < 0) Degree += 360;
        return Degree;
    }
    public static bool InRect(Vector2 Point,RectTransform Rect)
    {
        if (InRange(Point.x, Rect.anchoredPosition.x, Rect.anchoredPosition.x + Rect.sizeDelta.x) && InRange(Point.y, Rect.anchoredPosition.y - Rect.sizeDelta.y, Rect.anchoredPosition.y)) return true;
        else return false;
    }

}
