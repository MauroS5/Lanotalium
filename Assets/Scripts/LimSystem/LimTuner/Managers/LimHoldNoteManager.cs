using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LimHoldNoteManager : MonoBehaviour
{
    private bool isInitialized = false;
    public float OnTouchWidthAdd = 0.1f;
    public List<Lanotalium.Chart.LanotaHoldNote> HoldNote;
    public LimTunerManager Tuner;
    public Material HoldUntouch, HoldTouch;
    public Color OnSelectColor, NormalColor;

    public GameObject cT5S0, cT5S1, cT5S2, cT5S3;
    public GameObject oT5S0, oT5S1, oT5S2, oT5S3;
    public GameObject oTJoint;

    /// <summary>
    /// The box that makes a joint clickable, in the joint's own units, half
    /// the width of a note's so that a joint sitting on a rail under a note
    /// does not steal the note's clicks. The joint prefab ships without a
    /// collider because the game never had to pick one up; the editor does.
    /// </summary>
    private static readonly Vector3 JointColliderSize = new Vector3(1.5f, 1f, 0f);

    void Update()
    {
        if (!isInitialized) return;
        LimTimeGroups.EvaluateFrame(Tuner.ChartTime, Tuner.CameraManager);
        UpdateAllNoteShouldUpdate();
        UpdateAllNoteTransforms();
        UpdateAllLineMaterial();
        UpdateAllLineRenderers();
        UpdateGroupLineLook();
        UpdateAllNoteActive();
        UpdateAllJointActive();
        UpdateAllNoteColor();
        UpdateAllJointColor();
        UpdateNoteAudioEffect();
    }

    public void Initialize(List<Lanotalium.Chart.LanotaHoldNote> HoldNoteData)
    {
        HoldNote = HoldNoteData;
        InstantiateNotes();
        isInitialized = true;
    }

    public GameObject GetPrefab(int Size, bool Combination)
    {
        if (Combination)
        {
            switch (Size)
            {
                case 0: return cT5S0;
                case 1: return cT5S1;
                case 2: return cT5S2;
                case 3: return cT5S3;
            }
        }
        else if (!Combination)
        {
            switch (Size)
            {
                case 0: return oT5S0;
                case 1: return oT5S1;
                case 2: return oT5S2;
                case 3: return oT5S3;
            }
        }
        return null;
    }
    public void InstantiateHeadNote(Lanotalium.Chart.LanotaHoldNote Note)
    {
        Note.HoldNoteGameObject = Instantiate(GetPrefab(Note.Size, Note.Combination), transform);
        Note.LineRenderer = Note.HoldNoteGameObject.GetComponentInChildren<LineRenderer>();
        Note.InstanceId = Note.HoldNoteGameObject.GetInstanceID();
        Note.Sprite = Note.HoldNoteGameObject.GetComponentInChildren<SpriteRenderer>();
        Note.OnTouch = false;
        Note.HoldNoteGameObject.SetActive(false);
    }
    public void InstantiateAllJointNote(Lanotalium.Chart.LanotaHoldNote Note)
    {
        if (Note.Joints != null)
        {
            for (int i = 0; i < Note.Joints.Count - 1; ++i)
            {
                Note.Joints[i].JointGameObject = Instantiate(oTJoint, transform);
                Note.Joints[i].InstanceId = Note.Joints[i].JointGameObject.GetInstanceID();
                Note.Joints[i].Sprite = Note.Joints[i].JointGameObject.GetComponentInChildren<SpriteRenderer>(true);
                EnsureJointCollider(Note.Joints[i].JointGameObject);
                Note.Joints[i].JointGameObject.SetActive(false);
            }
        }
    }
    public void InstantiateJointNote(Lanotalium.Chart.LanotaJoints Joint)
    {
        Joint.JointGameObject = Instantiate(oTJoint, transform);
        Joint.InstanceId = Joint.JointGameObject.GetInstanceID();
        Joint.Sprite = Joint.JointGameObject.GetComponentInChildren<SpriteRenderer>(true);
        EnsureJointCollider(Joint.JointGameObject);
        Joint.JointGameObject.SetActive(false);
    }

    /// <summary>
    /// On the root, where the instance id that identifies the joint lives:
    /// selection compares the id of the object the ray hit, so a collider on
    /// a child would be hit and never recognised.
    /// </summary>
    private static void EnsureJointCollider(GameObject Joint)
    {
        if (Joint == null) return;
        if (Joint.GetComponent<Collider>() != null) return;
        BoxCollider Box = Joint.AddComponent<BoxCollider>();
        Box.size = JointColliderSize;
        Box.center = Vector3.zero;
    }
    private void InstantiateNotes()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            InstantiateHeadNote(Note);
            InstantiateAllJointNote(Note);
        }
    }

    /// <summary>
    /// The scroll list of the note being worked on, set at the top of each
    /// note in the loops that place them and cleared after. Null is the
    /// chart's own, which is every note of the base group.
    /// </summary>
    private List<Lanotalium.Chart.LanotaScroll> NoteScroll;
    /// <summary>Degrees the note's group is turned by, set alongside NoteScroll.</summary>
    private float NoteGroupRotation;
    private void UseScrollOf(Lanotalium.Chart.LanotaHoldNote Note)
    {
        NoteScroll = LimTimeGroups.UsesOwnScroll(Note.Group) ? LimTimeGroups.ScrollFor(Note.Group, Tuner.ScrollManager) : null;
        NoteGroupRotation = LimTimeGroups.NoteRotation(Note.Group);
    }
    /// <summary>
    /// The turn a note is drawn at: the camera's, plus its group's. Stored
    /// degrees stay the chart's own, so whatever adds this to draw subtracts
    /// it again before writing a joint's aDegree back.
    /// </summary>
    private float ViewRotation
    {
        get { return Tuner.CameraManager.CurrentRotation + NoteGroupRotation; }
    }
    /// <summary>Whether the note's own scroll is running backwards right now.</summary>
    private bool IsBackwarding(Lanotalium.Chart.LanotaHoldNote Note)
    {
        if (!LimTimeGroups.UsesOwnScroll(Note.Group)) return Tuner.ScrollManager.IsBackwarding;
        return LimTimeGroups.SpeedAt(LimTimeGroups.ScrollFor(Note.Group, Tuner.ScrollManager), Tuner.ChartTime) < 0;
    }
    private float CalculateMovePercent(float JudgeTime)
    {
        int StartScroll = 0, EndScroll = 0;
        float Percent = 100;
        List<Lanotalium.Chart.LanotaScroll> Scroll = NoteScroll ?? Tuner.ScrollManager.Scroll;
        int count = Scroll.Count;
        for (int i = 0; i < count - 1; ++i)
        {
            if (Scroll[i + 1].Time < Tuner.ChartTime) continue;
            if (Tuner.ChartTime >= Scroll[i].Time && Tuner.ChartTime < Scroll[i + 1].Time) StartScroll = i;
            if (Scroll[i + 1].Time < JudgeTime) continue;
            if (JudgeTime >= Scroll[i].Time && JudgeTime < Scroll[i + 1].Time) EndScroll = i;
        }
        if (count != 0)
        {
            if (Tuner.ChartTime >= Scroll[count - 1].Time) StartScroll = count - 1;
            if (JudgeTime >= Scroll[count - 1].Time) EndScroll = count - 1;
        }
        for (int i = StartScroll; i <= EndScroll; ++i)
        {
            if (StartScroll == EndScroll) Percent -= (JudgeTime - Tuner.ChartTime) * Scroll[i].Speed * 10 * Tuner.ChartPlaySpeed;
            else if (StartScroll != EndScroll)
            {
                if (i == StartScroll) Percent -= (Scroll[i + 1].Time - Tuner.ChartTime) * Scroll[i].Speed * 10 * Tuner.ChartPlaySpeed;
                else if (i != EndScroll && i != StartScroll) Percent -= (Scroll[i + 1].Time - Scroll[i].Time) * Scroll[i].Speed * 10 * Tuner.ChartPlaySpeed;
                else if (i == EndScroll) Percent -= (JudgeTime - Scroll[i].Time) * Scroll[i].Speed * 10 * Tuner.ChartPlaySpeed;
            }
        }
        Percent = Mathf.Clamp(Percent, 0, 100);
        return Percent;
    }
    private float CalculateEasedPercent(float Percent)
    {
        return LimNoteEase.Instance.CalculateEasedPercent(Percent);
    }
    private float CalculateEasedCurve(float Percent, int Mode)
    {
        if (Percent >= 1.0) return 1.0f;
        else if (Percent <= 0.0) return 0.0f;
        switch (Mode)
        {
            case 0:
                return Percent;
            case 1:
                return Percent * Percent * Percent * Percent;
            case 2:
                return -(Percent - 1) * (Percent - 1) * (Percent - 1) * (Percent - 1) + 1;
            case 3:
                return (Percent < 0.5) ? (Percent * Percent * Percent * Percent * 8) : ((Percent - 1) * (Percent - 1) * (Percent - 1) * (Percent - 1) * -8 + 1);
            case 4:
                return Percent * Percent * Percent;
            case 5:
                return (Percent - 1) * (Percent - 1) * (Percent - 1) + 1;
            case 6:
                return (Percent < 0.5) ? (Percent * Percent * Percent * 4) : ((Percent - 1) * (Percent - 1) * (Percent - 1) * 4 + 1);
            case 7:
                return Mathf.Pow(2, 10 * (float)(Percent - 1));
            case 8:
                return -Mathf.Pow(2, -10 * (float)Percent) + 1;
            case 9:
                return (Percent < 0.5) ? (Mathf.Pow(2, 10 * (2 * (float)Percent - 1)) / 2) : ((-Mathf.Pow(2, -10 * (2 * (float)Percent - 1)) + 2) / 2);
            case 10:
                return -Mathf.Cos((float)Percent * Mathf.PI / 2) + 1;
            case 11:
                return Mathf.Sin((float)Percent * Mathf.PI / 2);
            case 12:
                return (Mathf.Cos((float)Percent * Mathf.PI) - 1) / -2;
        }
        return 1;
    }
    private float CalculateReverseEasedCurve(float Percent, int Mode)
    {
        if (Percent >= 1.0) return 1.0f;
        else if (Percent <= 0.0) return 0.0f;
        switch (Mode)
        {
            case 0:
                return Percent;
            case 1:
                return Mathf.Pow(Percent, 0.25f);
            case 2:
                return 1 - Mathf.Pow(1 - Percent, 0.25f);
            case 3:
                return (Percent < 0.5) ? Mathf.Pow(Percent * 0.125f, 0.25f) : 1 - Mathf.Pow((1 - Percent) * 0.125f, 0.25f);
            case 4:
                return Mathf.Pow(Percent, 0.3333333f);
            case 5:
                return 1 - Mathf.Pow(1 - Percent, 0.3333333f);
            case 6:
                return (Percent < 0.5) ? Mathf.Pow(Percent * 0.25f, 0.3333333f) : 1 - Mathf.Pow((1 - Percent) * 0.25f, 0.3333333f);
            case 7:
                return Mathf.Log(2 * Mathf.Pow(Percent, 0.1f), 2);
            case 8:
                return Mathf.Log(Mathf.Pow(1 / (1 - Percent), 0.1f), 2);
            case 9:
                return (Percent < 0.5) ? Mathf.Log(1.464086f * Mathf.Pow(Percent, 0.05f), 2) : Mathf.Log(1.366040f * Mathf.Pow(1 / (1 - Percent), 0.05f), 2);
            case 10:
                return Mathf.Acos(1 - Percent) * 0.6366198f;
            case 11:
                return Mathf.Asin(Percent) * 0.6366198f;
            case 12:
                return Mathf.Acos(1 - 2 * Percent) * 0.3183099f;
        }
        return 1;
    }
    private float CalculateEasedCurveDirevative(float Percent, int Mode)
    {
        if (Percent >= 1.0) return 1.0f;
        else if (Percent <= 0.0) return 0.0f;
        switch (Mode)
        {
            case 0:
                return 1;
            case 1:
                return 4 * Mathf.Pow(Percent, 3);
            case 2:
                return 4 * Mathf.Pow(1 - Percent, 3);
            case 3:
                return (Percent < 0.5) ? (Mathf.Pow(Percent, 3) * 32) : (Mathf.Pow(1 - Percent, 3) * 32);
            case 4:
                return 3 * Mathf.Pow(Percent, 2);
            case 5:
                return 3 * Mathf.Pow(1 - Percent, 2);
            case 6:
                return (Percent < 0.5) ? (Mathf.Pow(Percent, 2) * 12) : (Mathf.Pow(1 - Percent, 2) * 12);
            case 7:
                return 6.931471f * Mathf.Pow(2, 10 * (Percent - 1));
            case 8:
                return Mathf.Pow(2, -10 * Percent) * 6.931471f;
            case 9:
                return (Percent < 0.5) ? 13.862943f * Mathf.Pow(2, 20 * Percent - 11) : 13.862943f * Mathf.Pow(2, -20 * Percent + 9);
            case 10:
                return 1.570796f * Mathf.Sin(Percent * 1.570796f);
            case 11:
                return 1.570796f * Mathf.Cos(Percent * 1.570796f);
            case 12:
                return 1.570796f * Mathf.Sin(Percent * Mathf.PI);
        }
        return 1;
    }
    private float CalculateHeadRotation(Lanotalium.Chart.LanotaHoldNote Note)
    {
        float LastaTime = Note.Time, LastaDegree = Note.Degree;
        if (Note.Joints != null)
        {
            foreach (Lanotalium.Chart.LanotaJoints Joint in Note.Joints)
            {
                if (Tuner.ChartTime > Joint.aTime)
                {
                    LastaTime = Joint.aTime;
                    LastaDegree = Joint.aDegree;
                    continue;
                }
                float Percent = (Tuner.ChartTime - LastaTime) / Joint.dTime;
                return ViewRotation + LastaDegree + Joint.dDegree * CalculateEasedCurve(Percent, Joint.Cfmi);
            }
            if (Note.Joints.Count != 0)
                return ViewRotation + Note.Joints[Note.Joints.Count - 1].aDegree;
        }
        return ViewRotation + Note.Degree;
    }
    private Vector3 CalculateLineRendererPoint(float Percent, float Degree)
    {
        return new Vector3(-Percent / 10 * Mathf.Sin(Degree * Mathf.Deg2Rad), 0, -Percent / 10 * Mathf.Cos(Degree * Mathf.Deg2Rad));
    }

    private void UpdateJointTransform(Lanotalium.Chart.LanotaHoldNote Note)
    {
        float aTime = Note.Time;
        float aDegree = Note.Degree + ViewRotation;
        if (Note.Joints != null)
        {
            for (int i = 0; i < Note.Joints.Count - 1; ++i)
            {
                Lanotalium.Chart.LanotaJoints Joint = Note.Joints[i];
                aTime += Joint.dTime;
                aDegree += Joint.dDegree;
                float Percent = CalculateMovePercent(aTime);
                Percent = CalculateEasedPercent(Percent);
                Joint.Percent = Percent;
                if (Percent < 20) continue;
                Joint.JointGameObject.transform.rotation = Quaternion.Euler(new Vector3(90, aDegree, 0));
                Joint.JointGameObject.transform.position = new Vector3(-Percent / 10 * Mathf.Sin(aDegree * Mathf.Deg2Rad), 0, -Percent / 10 * Mathf.Cos(aDegree * Mathf.Deg2Rad));
                Joint.JointGameObject.transform.localScale = new Vector3(Percent / 100, Percent / 100, 0);
                Joint.aTime = aTime;
                Joint.aDegree = aDegree - ViewRotation;
            }
        }
    }
    private void AddLineRendererPosition(int PositionIndex, LineRenderer lineRenderer, Vector3 Position)
    {
        if (lineRenderer.positionCount <= PositionIndex) lineRenderer.positionCount = PositionIndex + 1;
        lineRenderer.SetPosition(PositionIndex, Position);
    }

    private void UpdateAllNoteShouldUpdate()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            // See the tap notes: a group moving by its own speed skips the
            // chart's culling and is placed every frame.
            if (LimTimeGroups.UsesOwnScroll(Note.Group))
            {
                Note.shouldUpdate = LimTimeGroups.IsVisible(Note.Group);
                continue;
            }
            Note.shouldUpdate = LimTimeGroups.IsVisible(Note.Group);
            if (!LimScanTime.Instance.IsHoldNoteinScanRange(Note))
            {
                Note.shouldUpdate = false;
            }
        }
    }
    private void UpdateAllNoteTransforms()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            if (!Note.shouldUpdate)
            {
                continue;
            }
            UseScrollOf(Note);
            float Percent = CalculateMovePercent(Note.Time);
            Percent = CalculateEasedPercent(Percent);
            Note.Percent = Percent;
            UpdateJointTransform(Note);
            if (Percent < 20) continue;
            float RotatedDegree = CalculateHeadRotation(Note);
            Note.HoldNoteGameObject.transform.rotation = Quaternion.Euler(new Vector3(90, RotatedDegree, 0));
            Note.HoldNoteGameObject.transform.position = new Vector3(-Percent / 10 * Mathf.Sin(RotatedDegree * Mathf.Deg2Rad), 0, -Percent / 10 * Mathf.Cos(RotatedDegree * Mathf.Deg2Rad));
            Note.HoldNoteGameObject.transform.localScale = new Vector3(Percent / 100, Percent / 100, 0);
            Note.FinalDegree = RotatedDegree;
        }
        NoteScroll = null;
        NoteGroupRotation = 0;
    }
    private void UpdateAllLineRenderers()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            if (!Note.shouldUpdate)
            {
                continue;
            }
            if (Note.Percent < 20) continue;
            UseScrollOf(Note);
            if (Note.Jcount == 0)
            {
                float Rotation = Note.Degree + ViewRotation;
                float EndPercent = CalculateMovePercent(Note.Time + Note.Duration);
                Note.LineRenderer.positionCount = 10;
                Note.LineRenderer.startWidth = Note.Percent / 100 + (Note.OnTouch ? OnTouchWidthAdd : 0);
                Note.LineRenderer.endWidth = CalculateEasedPercent(EndPercent) / 100 + (Note.OnTouch ? OnTouchWidthAdd : 0);
                Vector3 Start = CalculateLineRendererPoint(Note.Percent, Rotation);
                Vector3 End = CalculateLineRendererPoint(CalculateEasedPercent(EndPercent), Rotation);
                Vector3 Delta = (End - Start) / 9;
                for (int i = 0; i < 10; ++i) Note.LineRenderer.SetPosition(i, Start + i * Delta);
            }
            else
            {
                float lastPercent = 0;
                int positionIndex = 0;
                float currentaTime = Note.Time, currentaDegree = Note.Degree;
                bool headPointAdded = false;

                for (int i = 0; i < Note.Joints.Count; ++i)
                {
                    Lanotalium.Chart.LanotaJoints Joint = Note.Joints[i];
                    if (Joint.aTime < Tuner.ChartTime)
                    {
                        currentaTime = Joint.aTime;
                        currentaDegree = Joint.aDegree;
                        continue;
                    }

                    if (!headPointAdded)
                    {
                        if (Note.Time < Tuner.ChartTime)
                        {
                            float startDegreePercent = (Tuner.ChartTime - currentaTime) / Joint.dTime;
                            AddLineRendererPosition(positionIndex, Note.LineRenderer, CalculateLineRendererPoint(100, currentaDegree + Joint.dDegree * CalculateEasedCurve(startDegreePercent, Joint.Cfmi) + ViewRotation));
                            positionIndex++;
                        }
                        headPointAdded = true;
                    }

                    int jCount = Mathf.Max(Mathf.FloorToInt(Mathf.Abs(Joint.dDegree)), 50);
                    for (int j = 0; j < jCount; ++j)
                    {
                        float degreePercent = 1f * j / (jCount - 1);
                        float degree = currentaDegree + Joint.dDegree * degreePercent;
                        float timingPercent = CalculateReverseEasedCurve(degreePercent, Joint.Cfmi);
                        float timing = currentaTime + Joint.dTime * timingPercent;
                        float percent = CalculateEasedPercent(CalculateMovePercent(timing));
                        lastPercent = percent;
                        if (percent == 100 && timing <= Tuner.ChartTime) continue;
                        AddLineRendererPosition(positionIndex, Note.LineRenderer, CalculateLineRendererPoint(percent, degree + ViewRotation));
                        positionIndex++;
                        if (percent <= 15) goto end;
                    }

                    currentaTime = Joint.aTime;
                    currentaDegree = Joint.aDegree;
                }
                end:

                Note.LineRenderer.startWidth = Note.Percent / 100;
                Note.LineRenderer.endWidth = lastPercent / 100;
                Note.LineRenderer.positionCount = positionIndex;
            }
        }
        NoteScroll = null;
        NoteGroupRotation = 0;
    }
    private void UpdateAllNoteActive()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            if (!Note.shouldUpdate)
            {
                if (Note.HoldNoteGameObject.activeInHierarchy) Note.HoldNoteGameObject.SetActive(false);
                continue;
            }
            if (Tuner.ChartTime > Note.Time + Note.Duration && Note.HoldNoteGameObject.activeInHierarchy) Note.HoldNoteGameObject.SetActive(false);
            else if (Tuner.ChartTime < Note.Time)
            {
                if (Note.Percent <= 20 || Note.Percent >= 100)
                {
                    if (IsBackwarding(Note))
                    {
                        if (Note.Percent == 100 && !Note.HoldNoteGameObject.activeInHierarchy)
                        {
                            Note.HoldNoteGameObject.SetActive(true);
                            Note.SetSpritesActive(false);
                        }
                    }
                    else if (Note.HoldNoteGameObject.activeInHierarchy) Note.HoldNoteGameObject.SetActive(false);
                }
                else
                {
                    Note.SetSpritesActive(true);
                    if (!Note.HoldNoteGameObject.activeInHierarchy) Note.HoldNoteGameObject.SetActive(true);
                }
            }
            else if (Tuner.ChartTime >= Note.Time && Tuner.ChartTime <= Note.Time + Note.Duration)
            {
                if (Note.Percent == 100 && !Note.HoldNoteGameObject.activeInHierarchy) Note.HoldNoteGameObject.SetActive(true);
            }
        }
    }
    private void UpdateAllJointActive()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {

            if (!Note.shouldUpdate)
            {
                if (Note.Joints == null) continue;
                for (int i = 0; i < Note.Joints.Count - 1; ++i)
                {
                    Lanotalium.Chart.LanotaJoints Joint = Note.Joints[i];
                    if (Joint.JointGameObject.activeInHierarchy) Joint.JointGameObject.SetActive(false);
                }
                continue;
            }
            if (Note.Joints == null) continue;
            for (int i = 0; i < Note.Joints.Count - 1; ++i)
            {
                Lanotalium.Chart.LanotaJoints Joint = Note.Joints[i];
                if (Tuner.ChartTime > Joint.aTime && Joint.JointGameObject.activeInHierarchy) Joint.JointGameObject.SetActive(false);
                else if (Tuner.ChartTime < Joint.aTime)
                {
                    if (Joint.Percent <= 20 || Joint.Percent >= 100)
                    {
                        if (Joint.JointGameObject.activeInHierarchy) Joint.JointGameObject.SetActive(false);
                    }
                    else
                    {
                        if (!Joint.JointGameObject.activeInHierarchy) Joint.JointGameObject.SetActive(true);
                    }
                }
            }
        }
    }
    private void UpdateAllLineMaterial()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            if (!Note.shouldUpdate)
            {
                continue;
            }
            if (Note.Percent < 20) continue;
            if (Tuner.ChartTime >= Note.Time && Tuner.ChartTime < Note.Time + Note.Duration)
            {
                if (!Note.OnTouch)
                {
                    Note.LineRenderer.material = HoldTouch;
                    Note.OnTouch = true;
                }
            }
            else
            {
                if (Note.OnTouch)
                {
                    Note.LineRenderer.material = HoldUntouch;
                    Note.OnTouch = false;
                }
            }
        }
    }
    private void UpdateAllNoteColor()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            if (!Note.shouldUpdate)
            {
                continue;
            }
            // Compared against the colour it should be, not against the one
            // it should not: a rail whose sprite ended up any third colour
            // used to match neither test and stay looking picked up for the
            // rest of the session. The tap notes were already read this way.
            Color Wanted = Note.OnSelect ? OnSelectColor : NormalColor;
            if (LimTimeGroups.HasEffects(Note.Group))
            {
                Wanted = LimTimeGroups.Shade(Note.Group, Wanted, Note.Percent, Note.OnSelect, LimTimeGroups.IsHighlight(Note.Sprite));
                LimTimeGroups.FadeExtras(Note.HoldNoteGameObject, Note.Sprite, Note.Group, Note.OnSelect, Wanted.a);
            }
            else LimTimeGroups.RestoreExtras(Note.HoldNoteGameObject, Note.Sprite);
            if (Note.Sprite.color != Wanted) Note.Sprite.color = Wanted;
        }
    }
    /// <summary>
    /// The body of a rail in a group that is faded or tinted. The body's own
    /// materials are shared by every rail and their shader is not known to
    /// take a colour, so such a rail is drawn with a copy that wears the same
    /// texture on the sprite shader, which does, and handed its colour through
    /// the line's own start and end colours: the head end faded as the head
    /// is, the tail end as the tail is. A rail with nothing to show goes back
    /// to the shared material, so a group left plain looks exactly as before.
    /// </summary>
    private void UpdateGroupLineLook()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            if (!Note.shouldUpdate || Note.LineRenderer == null) continue;
            Material Shared = Note.OnTouch ? HoldTouch : HoldUntouch;
            if (!LimTimeGroups.HasEffects(Note.Group))
            {
                // A rail that has just left its group, or whose group was
                // removed, still wearing the copy: it gets the shared one back.
                if (FadeMaterials.ContainsValue(Note.LineRenderer.sharedMaterial) && Note.LineRenderer.sharedMaterial != null)
                {
                    Note.LineRenderer.sharedMaterial = Shared;
                    Note.LineRenderer.startColor = Color.white;
                    Note.LineRenderer.endColor = Color.white;
                }
                continue;
            }
            UseScrollOf(Note);
            float TailPercent = CalculateEasedPercent(CalculateMovePercent(Note.Time + Note.Duration));
            Color HeadShade = LimTimeGroups.Shade(Note.Group, Color.white, Note.Percent, Note.OnSelect);
            Color TailShade = LimTimeGroups.Shade(Note.Group, Color.white, TailPercent, Note.OnSelect);
            bool Plain = HeadShade == Color.white && TailShade == Color.white;
            Material Wanted = Plain ? Shared : FadeMaterialFor(Shared);
            if (Wanted == null) Wanted = Shared;
            if (Note.LineRenderer.sharedMaterial != Wanted) Note.LineRenderer.sharedMaterial = Wanted;
            if (Wanted == Shared)
            {
                if (Note.LineRenderer.startColor != Color.white) Note.LineRenderer.startColor = Color.white;
                if (Note.LineRenderer.endColor != Color.white) Note.LineRenderer.endColor = Color.white;
                continue;
            }
            Color Own = SharedColorOf(Shared);
            Note.LineRenderer.startColor = Own * HeadShade;
            Note.LineRenderer.endColor = Own * TailShade;
        }
        NoteScroll = null;
        NoteGroupRotation = 0;
    }
    private readonly Dictionary<Material, Material> FadeMaterials = new Dictionary<Material, Material>();
    private Material FadeMaterialFor(Material Shared)
    {
        if (Shared == null) return null;
        Material Made;
        if (FadeMaterials.TryGetValue(Shared, out Made)) return Made;
        Shader Sprite = Shader.Find("Sprites/Default");
        if (Sprite == null) { FadeMaterials[Shared] = null; return null; }
        Made = new Material(Sprite);
        if (Shared.HasProperty("_MainTex")) Made.mainTexture = Shared.GetTexture("_MainTex");
        Made.renderQueue = 3000;
        FadeMaterials[Shared] = Made;
        return Made;
    }
    private static Color SharedColorOf(Material Shared)
    {
        return Shared != null && Shared.HasProperty("_Color") ? Shared.GetColor("_Color") : Color.white;
    }
    /// <summary>
    /// A picked-up joint is coloured the way a picked-up note is. Painted
    /// here rather than when it is selected because a joint's object comes
    /// and goes as rails are cut and mended, and this way a new one is right
    /// on the frame it appears.
    /// </summary>
    private void UpdateAllJointColor()
    {
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            if (!Note.shouldUpdate) continue;
            if (Note.Joints == null) continue;
            foreach (Lanotalium.Chart.LanotaJoints Joint in Note.Joints)
            {
                if (Joint.Sprite == null) continue;
                Color Wanted = Joint.OnSelect ? OnSelectColor : NormalColor;
                if (LimTimeGroups.HasEffects(Note.Group)) Wanted = LimTimeGroups.Shade(Note.Group, Wanted, Joint.Percent, Joint.OnSelect);
                if (Joint.Sprite.color != Wanted) Joint.Sprite.color = Wanted;
            }
        }
    }
    private void UpdateNoteAudioEffect()
    {
        if (!LimSystem.Preferences.AudioEffect) return;
        bool ShouldPlayRail = false;
        foreach (Lanotalium.Chart.LanotaHoldNote Note in HoldNote)
        {
            if (!Note.shouldUpdate)
            {
                continue;
            }
            if (Note.Time + Note.Duration <= Tuner.ChartTime)
            {
                if (!Note.EndEffectPlayed)
                {
                    if (Tuner.MediaPlayerManager.IsPlaying) Tuner.AudioEffectManager.PlayRailEnd();
                    Note.EndEffectPlayed = true;
                }
            }
            else if (Note.Time <= Tuner.ChartTime && Note.Time + Note.Duration > Tuner.ChartTime)
            {
                if (!Note.StartEffectPlayed)
                {
                    if (Tuner.MediaPlayerManager.IsPlaying) Tuner.AudioEffectManager.PlayClick();
                    Note.StartEffectPlayed = true;
                }
                ShouldPlayRail = true;
                Note.EndEffectPlayed = false;
            }
            else if (Note.Time > Tuner.ChartTime)
            {
                Note.StartEffectPlayed = false;
                Note.EndEffectPlayed = false;
            }
        }
        if (Tuner.MediaPlayerManager.IsPlaying)
        {
            if (ShouldPlayRail) Tuner.AudioEffectManager.StartPlayRail();
            else Tuner.AudioEffectManager.StopPlayRail();
        }
        else Tuner.AudioEffectManager.StopPlayRail();
    }

    public void SortHoldNoteList()
    {
        HoldNote.Sort((Lanotalium.Chart.LanotaHoldNote A, Lanotalium.Chart.LanotaHoldNote B) =>
        {
            return A.Time.CompareTo(B.Time);
        });
    }
}
