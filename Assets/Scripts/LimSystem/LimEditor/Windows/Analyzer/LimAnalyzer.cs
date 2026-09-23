using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Analyzer menu: Automatic finds the song's bpm and first beat and
/// writes them as the chart's bpm list; Manual opens a window to tap the
/// tempo out by hand (LimAnalyzerManual.cs).
///
/// Added at runtime by the top menu, which gave up its Chart Convert entry
/// for it.
/// </summary>
public partial class LimAnalyzer : MonoBehaviour
{
    private bool Busy;
    private float Progress;

    private static string Format(float Value, string Pattern)
    {
        return Value.ToString(Pattern, CultureInfo.InvariantCulture);
    }

    /// <summary>A tempo as it reads best: 150, 150.5, 191.25.</summary>
    private static string BpmText(double Bpm)
    {
        return Bpm.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static MessageBoxManager Messages
    {
        get { return MessageBoxManager.Instance != null ? MessageBoxManager.Instance : FindObjectOfType<MessageBoxManager>(); }
    }

    // ---- Automatic ---------------------------------------------------------

    public void RunAutomatic()
    {
        if (Busy) return;
        if (LimSystem.ChartContainer == null) return;
        AudioClip Clip = LimSystem.ChartContainer.ChartMusic != null ? LimSystem.ChartContainer.ChartMusic.Music : null;
        if (Clip == null || Clip.samples <= 0 || Clip.frequency <= 0)
        {
            Messages.ShowMessage(LimLanguageManager.TextDict["Analyzer_NoMusic"]);
            return;
        }
        StartCoroutine(AutomaticCoroutine(Clip));
    }

    private IEnumerator AutomaticCoroutine(AudioClip Clip)
    {
        Busy = true;
        Progress = 0;
        ProgressBarManager Bar = ProgressBarManager.Instance != null ? ProgressBarManager.Instance : FindObjectOfType<ProgressBarManager>();
        if (Bar != null) Bar.ShowProgress(() => !Busy, () => Progress);

        // The song is read on the main thread, which is the only one allowed
        // to touch a clip, a slice at a time so the window keeps drawing;
        // the channels are mixed down and the rate halved on the way to keep
        // the copy small.
        int Channels = Mathf.Max(1, Clip.channels);
        int Frequency = Clip.frequency;
        int Frames = Clip.samples;
        int Factor = Mathf.Max(1, Mathf.RoundToInt(Frequency / 22050f));
        float[] Mono = new float[Frames / Factor];
        const int BlockFrames = 1 << 16;
        float[] Block = new float[BlockFrames * Channels];
        float Scale = 1f / (Channels * Factor);
        int Frame = 0;
        System.Diagnostics.Stopwatch Slice = System.Diagnostics.Stopwatch.StartNew();
        while (Frame < Frames)
        {
            int Wanted = Mathf.Min(BlockFrames, Frames - Frame);
            // The last block is shorter: asking for more than is left wraps
            // round to the beginning of the song.
            if (Wanted != BlockFrames) Block = new float[Wanted * Channels];
            if (!Clip.GetData(Block, Frame)) break;
            for (int i = 0; i < Wanted; ++i)
            {
                int Target = (Frame + i) / Factor;
                if (Target >= Mono.Length) break;
                float Sum = 0;
                int Base = i * Channels;
                for (int c = 0; c < Channels; ++c) Sum += Block[Base + c];
                Mono[Target] += Sum * Scale;
            }
            Frame += Wanted;
            Progress = 0.25f * Frame / Frames;
            if (Slice.ElapsedMilliseconds > 30)
            {
                yield return null;
                Slice.Reset();
                Slice.Start();
            }
        }
        Block = null;

        LimBpmAnalysis.Result Result = null;
        Exception Failure = null;
        Thread Worker = new Thread(() =>
        {
            try { Result = LimBpmAnalysis.Analyze(Mono, Frequency / Factor, Value => { Progress = 0.25f + 0.75f * Value; }); }
            catch (Exception E) { Failure = E; }
        });
        Worker.IsBackground = true;
        Worker.Start();
        while (Worker.IsAlive) yield return null;
        Mono = null;
        Busy = false;

        if (Failure != null) Debug.LogException(Failure);
        if (Result == null || Result.Bpm <= 0 || LimSystem.ChartContainer == null)
        {
            Messages.ShowMessage(LimLanguageManager.TextDict["Analyzer_Failed"]);
            yield break;
        }
        if (!Result.HasChanges)
        {
            ApplyOriginal(Result);
            yield break;
        }
        AskAboutChanges(Result);
    }

    private void ApplyOriginal(LimBpmAnalysis.Result Result)
    {
        ApplySections(new List<KeyValuePair<float, float>> { Pair(Result.FirstBeat, Result.Bpm) });
        Messages.ShowMessage(string.Format(LimLanguageManager.TextDict["Analyzer_Applied"],
            BpmText(Result.Bpm), Format((float)Result.FirstBeat, "0.000")));
    }

    private void ApplyAll(LimBpmAnalysis.Result Result)
    {
        List<KeyValuePair<float, float>> Starts = new List<KeyValuePair<float, float>>();
        foreach (LimBpmAnalysis.Section Section in Result.Sections) Starts.Add(Pair(Section.Time, Section.Bpm));
        ApplySections(Starts);
        Messages.ShowMessage(string.Format(LimLanguageManager.TextDict["Analyzer_AppliedAll"],
            Result.Sections.Count, Format((float)Result.Sections[0].Time, "0.000")));
    }

    private static KeyValuePair<float, float> Pair(double Time, double Bpm)
    {
        return new KeyValuePair<float, float>((float)Math.Round(Time, 4), (float)Bpm);
    }

    private void ApplySections(List<KeyValuePair<float, float>> Starts)
    {
        LimOperationManager Operations = LimOperationManager.Instance != null ? LimOperationManager.Instance : FindObjectOfType<LimOperationManager>();
        if (Operations == null) return;
        Operations.ReplaceBpmList(Operations.BpmListOf(Starts));
    }

    // ---- The question about tempo changes ----------------------------------

    private GameObject ChoiceBox;
    private Text ChoiceMessage;
    private Button ChoiceApplyAll, ChoiceKeep;

    private void AskAboutChanges(LimBpmAnalysis.Result Result)
    {
        if (!EnsureChoiceBox())
        {
            // Without a box to ask in, the song's own tempo is the safe answer.
            ApplyOriginal(Result);
            return;
        }
        List<string> Changes = new List<string>();
        for (int i = 1; i < Result.Sections.Count && i <= 5; ++i)
            Changes.Add(string.Format(LimLanguageManager.TextDict["Analyzer_ChangeAt"],
                BpmText(Result.Sections[i - 1].Bpm), BpmText(Result.Sections[i].Bpm), Format((float)Result.Sections[i].Time, "0.00")));
        if (Result.Sections.Count > 6) Changes.Add("…");
        ChoiceMessage.text = LimLanguageManager.TextDict["Analyzer_ChangesFound"] + "\n\n" + string.Join("\n", Changes.ToArray());
        SetLabel(ChoiceApplyAll, LimLanguageManager.TextDict["Analyzer_ApplyAll"]);
        SetLabel(ChoiceKeep, LimLanguageManager.TextDict["Analyzer_KeepOriginal"]);

        ChoiceApplyAll.onClick = new Button.ButtonClickedEvent();
        ChoiceApplyAll.onClick.AddListener(() => { ChoiceBox.SetActive(false); ApplyAll(Result); });
        ChoiceKeep.onClick = new Button.ButtonClickedEvent();
        ChoiceKeep.onClick.AddListener(() => { ChoiceBox.SetActive(false); ApplyOriginal(Result); });
        ChoiceBox.transform.SetAsLastSibling();
        ChoiceBox.SetActive(true);
    }

    private static void SetLabel(Button Target, string Label)
    {
        Text Caption = Target.GetComponentInChildren<Text>(true);
        if (Caption != null) Caption.text = Label;
    }

    /// <summary>
    /// A copy of the message box with room for longer answers than OK and
    /// Cancel. The box itself is left alone: it is shared by the whole
    /// editor, and its buttons are wired to answer whatever it was last asked.
    /// </summary>
    private bool EnsureChoiceBox()
    {
        if (ChoiceBox != null) return true;
        MessageBoxManager Source = Messages;
        if (Source == null || Source.Canvas == null) return false;

        ChoiceBox = Instantiate(Source.Canvas, Source.Canvas.transform.parent);
        LimThemeManager.Adopt(Source.Canvas, ChoiceBox);
        ChoiceBox.name = "AnalyzerChoice";
        ChoiceBox.SetActive(false);
        foreach (LimMouseOverHint Hint in ChoiceBox.GetComponentsInChildren<LimMouseOverHint>(true)) Destroy(Hint);
        foreach (EventTrigger Trigger in ChoiceBox.GetComponentsInChildren<EventTrigger>(true)) Destroy(Trigger);

        // The buttons are told apart by what they were wired to.
        foreach (Button Candidate in ChoiceBox.GetComponentsInChildren<Button>(true))
        {
            string Method = Candidate.onClick.GetPersistentEventCount() > 0 ? Candidate.onClick.GetPersistentMethodName(0) : "";
            if (Method == "OK") ChoiceApplyAll = Candidate;
            else if (Method == "Cancel") ChoiceKeep = Candidate;
        }
        if (ChoiceApplyAll == null || ChoiceKeep == null)
        {
            Destroy(ChoiceBox);
            ChoiceBox = null;
            return false;
        }
        Widen(ChoiceApplyAll, -1);
        Widen(ChoiceKeep, 1);

        foreach (Text Candidate in ChoiceBox.GetComponentsInChildren<Text>(true))
        {
            if (Candidate.GetComponentInParent<Button>() != null) continue;
            ChoiceMessage = Candidate;
            // The list of changes goes under the question.
            Candidate.fontSize = Mathf.Min(Candidate.fontSize, 20);
            Candidate.resizeTextForBestFit = true;
            Candidate.resizeTextMinSize = 12;
            Candidate.resizeTextMaxSize = Candidate.fontSize;
            break;
        }
        return ChoiceMessage != null;
    }

    private static void Widen(Button Target, int Side)
    {
        RectTransform Rect = Target.GetComponent<RectTransform>();
        Rect.sizeDelta = new Vector2(230, 34);
        Rect.anchoredPosition = new Vector2(Side * 125, Rect.anchoredPosition.y);
    }
}
