using UnityEngine;

/// <summary>
/// Flowaria's UiTweak, built into the tuner: hit effects and the combo
/// counter (<see cref="LimNoteEffects"/>), the judge line's ornaments
/// (<see cref="LimJudgeLineOrnaments"/>), the flick arrows
/// (<see cref="LimFlickArrows"/>), the wave along the bottom
/// (<see cref="LimPlaySceneWave"/>), the HD rails (<see cref="LimHdRails"/>),
/// "Perfect Purified" (<see cref="LimPerfectPurified"/>), "Ready"
/// (<see cref="LimReadyIntro"/>), skins and the HD core from the TunerSkin
/// folder (<see cref="LimTunerSkins"/>) and the Lanota style header with its score
/// (<see cref="LimLanotaHeader"/>). Each is switched from the UiTweak menu
/// and reads its switch every frame, since the preferences arrive after this
/// Start.
/// </summary>
public partial class LimTunerManager
{
    private void SetUpVisualEffects()
    {
        if (GetComponent<LimNoteEffects>() == null) gameObject.AddComponent<LimNoteEffects>().Tuner = this;
        if (GetComponent<LimJudgeLineOrnaments>() == null) gameObject.AddComponent<LimJudgeLineOrnaments>().Tuner = this;
        if (GetComponent<LimFlickArrows>() == null) gameObject.AddComponent<LimFlickArrows>().Tuner = this;
        if (GetComponent<LimPlaySceneWave>() == null) gameObject.AddComponent<LimPlaySceneWave>().Tuner = this;
        if (GetComponent<LimHdRails>() == null) gameObject.AddComponent<LimHdRails>().Tuner = this;
        if (GetComponent<LimPerfectPurified>() == null) gameObject.AddComponent<LimPerfectPurified>().Tuner = this;
        if (GetComponent<LimReadyIntro>() == null) gameObject.AddComponent<LimReadyIntro>().Tuner = this;
        if (GetComponent<LimCompactHighlight>() == null) gameObject.AddComponent<LimCompactHighlight>().Tuner = this;
        LimTunerHeadManager Head = GetComponentInChildren<LimTunerHeadManager>(true);
        if (Head == null) Head = FindObjectOfType<LimTunerHeadManager>();
        if (Head != null && Head.GetComponent<LimLanotaHeader>() == null)
        {
            LimLanotaHeader Look = Head.gameObject.AddComponent<LimLanotaHeader>();
            Look.Head = Head;
            Look.Tuner = this;
        }
    }

    private void LateUpdate()
    {
        LimTunerSkins.Refresh(false);
    }
}
