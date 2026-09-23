using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saving a selection as a pattern, and bringing one back.
///
/// Ctrl+F keeps whatever is selected: notes or motions, never a mix, since a
/// pattern is pasted into one place or the other and a mixed one would have
/// nowhere to go. Patterns are stored relative to their earliest element, so
/// the same one can be dropped anywhere in any chart.
///
/// Bringing one back works the way each kind is normally placed: notes go
/// into the paste preview, the ghost that follows the pointer until a click
/// drops them, and motions are laid down at the playhead, since the timeline
/// has no such preview.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>Enough for a phrase or two; a cap keeps the preferences file sane.</summary>
    public const int MaximumFavouriteNotes = 100;

    private static List<Lanotalium.Editor.FavouritePattern> Patterns
    {
        get
        {
            if (LimSystem.Preferences.FavouritePatterns == null)
                LimSystem.Preferences.FavouritePatterns = new List<Lanotalium.Editor.FavouritePattern>();
            return LimSystem.Preferences.FavouritePatterns;
        }
    }

    public void DetectFavouriteShortcut()
    {
        if (LimSystem.ChartContainer == null) return;
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
        if (!Input.GetKeyDown(KeyCode.F)) return;
        if (IsTypingInTextField()) return;
        // Nothing selected means the key belongs to Create Scroll Speed,
        // which it did before this: stay quiet and let it through.
        if (SelectedTapNote.Count == 0 && SelectedHoldNote.Count == 0 && SelectedMotions.Count == 0) return;
        SaveSelectionAsFavourite();
    }

    /// <summary>
    /// Keeps the selection as a new pattern. Answers with what was saved, or
    /// null when there was nothing to save, in which case the reason has
    /// already been shown.
    /// </summary>
    public Lanotalium.Editor.FavouritePattern SaveSelectionAsFavourite()
    {
        bool HasNotes = SelectedTapNote.Count > 0 || SelectedHoldNote.Count > 0;
        bool HasMotions = SelectedMotions.Count > 0;

        if (HasNotes && HasMotions)
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_Mixed"]);
            return null;
        }
        if (!HasNotes && !HasMotions)
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_Nothing"]);
            return null;
        }
        if (HasNotes && SelectedTapNote.Count + SelectedHoldNote.Count > MaximumFavouriteNotes)
        {
            LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_TooMany"]);
            return null;
        }

        Lanotalium.Editor.FavouritePattern Pattern = HasNotes ? CaptureNotes() : CaptureMotions();
        Pattern.Name = NextFavouriteName(HasMotions);
        Patterns.Add(Pattern);
        LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Favourite_Saved"]);
        return Pattern;
    }

    private string NextFavouriteName(bool Motions)
    {
        string Stem = LimLanguageManager.TextDict[Motions ? "Window_Creator_Favourites_Motions" : "Window_Creator_Favourites_Notes"];
        int Count = 0;
        foreach (Lanotalium.Editor.FavouritePattern Pattern in Patterns)
            if (Pattern.HoldsMotions == Motions) ++Count;
        return Stem + " " + (Count + 1);
    }

    private Lanotalium.Editor.FavouritePattern CaptureNotes()
    {
        Lanotalium.Editor.FavouritePattern Pattern = new Lanotalium.Editor.FavouritePattern();

        float Anchor = float.MaxValue;
        foreach (Lanotalium.Chart.LanotaTapNote Tap in SelectedTapNote) if (Tap.Time < Anchor) Anchor = Tap.Time;
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in SelectedHoldNote) if (Hold.Time < Anchor) Anchor = Hold.Time;

        foreach (Lanotalium.Chart.LanotaTapNote Tap in SelectedTapNote)
        {
            Pattern.Notes.Add(new Lanotalium.Editor.FavouriteNote
            {
                Type = Tap.Type,
                Time = Tap.Time - Anchor,
                Duration = Tap.Duration,
                Degree = Tap.Degree,
                Size = Tap.Size,
                Critical = Tap.Critical,
                Combination = Tap.Combination,
                Bpm = Tap.Bpm
            });
        }
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in SelectedHoldNote)
        {
            Lanotalium.Editor.FavouriteNote Saved = new Lanotalium.Editor.FavouriteNote
            {
                Type = Hold.Type,
                Time = Hold.Time - Anchor,
                Duration = Hold.Duration,
                Degree = Hold.Degree,
                Size = Hold.Size,
                Critical = Hold.Critical,
                Combination = Hold.Combination,
                Bpm = Hold.Bpm
            };
            if (Hold.Joints != null)
            {
                foreach (Lanotalium.Chart.LanotaJoints Joint in Hold.Joints)
                    Saved.Joints.Add(new Lanotalium.Editor.FavouriteJoint { Cfmi = Joint.Cfmi, dDegree = Joint.dDegree, dTime = Joint.dTime });
            }
            Pattern.Notes.Add(Saved);
        }
        return Pattern;
    }

    private Lanotalium.Editor.FavouritePattern CaptureMotions()
    {
        Lanotalium.Editor.FavouritePattern Pattern = new Lanotalium.Editor.FavouritePattern();

        float Anchor = float.MaxValue;
        foreach (Lanotalium.Chart.LanotaCameraBase Motion in SelectedMotions) if (Motion.Time < Anchor) Anchor = Motion.Time;

        foreach (Lanotalium.Chart.LanotaCameraBase Motion in SelectedMotions)
        {
            Pattern.Motions.Add(new Lanotalium.Editor.FavouriteMotion
            {
                Type = Motion.Type,
                Time = Motion.Time - Anchor,
                Duration = Motion.Duration,
                ctp = Motion.ctp,
                ctp1 = Motion.ctp1,
                ctp2 = Motion.ctp2,
                cfmi = Motion.cfmi,
                cflg = Motion.cflg
            });
        }
        return Pattern;
    }

    /// <summary>
    /// Brings a pattern back. Notes are handed to the paste preview, so they
    /// follow the pointer until a click drops them; motions are laid down at
    /// the playhead, as one undo step.
    /// </summary>
    public void UseFavourite(Lanotalium.Editor.FavouritePattern Pattern)
    {
        if (Pattern == null) return;
        if (TunerManager == null || !TunerManager.isInitialized) return;
        if (Pattern.HoldsMotions) PasteFavouriteMotions(Pattern);
        else LoadFavouriteNotesIntoClipboard(Pattern);
    }

    private void LoadFavouriteNotesIntoClipboard(Lanotalium.Editor.FavouritePattern Pattern)
    {
        if (Pattern.Notes == null || Pattern.Notes.Count == 0) return;
        CancelPaste();
        _Clipboard.Clear();

        // The first note of the pattern is its handle, the same way a copied
        // selection uses its earliest note.
        float AnchorDegree = 0;
        float Anchor = float.MaxValue;
        foreach (Lanotalium.Editor.FavouriteNote Note in Pattern.Notes)
            if (Note.Time < Anchor) { Anchor = Note.Time; AnchorDegree = Note.Degree; }

        foreach (Lanotalium.Editor.FavouriteNote Note in Pattern.Notes)
        {
            ClipboardItem Item = new ClipboardItem
            {
                OffsetTime = Note.Time - Anchor,
                OffsetDegree = Note.Degree - AnchorDegree
            };
            if (Note.Type == 5) Item.Hold = ToHoldNote(Note);
            else Item.Tap = ToTapNote(Note);
            _Clipboard.Add(Item);
        }

        MeasureClipboardExtent();
        // Notes, so that the next Ctrl+V offers these and not the motions
        // that may also be sitting in the other clipboard.
        _ClipboardHoldsMotions = false;
        BeginPaste();
    }

    private static Lanotalium.Chart.LanotaTapNote ToTapNote(Lanotalium.Editor.FavouriteNote Note)
    {
        return new Lanotalium.Chart.LanotaTapNote
        {
            Type = Note.Type,
            Time = Note.Time,
            Duration = Note.Duration,
            Degree = Note.Degree,
            Size = Note.Size,
            Critical = Note.Critical,
            Combination = Note.Combination,
            Bpm = Note.Bpm
        };
    }

    private static Lanotalium.Chart.LanotaHoldNote ToHoldNote(Lanotalium.Editor.FavouriteNote Note)
    {
        Lanotalium.Chart.LanotaHoldNote Hold = new Lanotalium.Chart.LanotaHoldNote
        {
            Type = Note.Type,
            Time = Note.Time,
            Duration = Note.Duration,
            Degree = Note.Degree,
            Size = Note.Size,
            Critical = Note.Critical,
            Combination = Note.Combination,
            Bpm = Note.Bpm,
            Joints = new List<Lanotalium.Chart.LanotaJoints>()
        };
        if (Note.Joints != null)
        {
            foreach (Lanotalium.Editor.FavouriteJoint Joint in Note.Joints)
                Hold.Joints.Add(new Lanotalium.Chart.LanotaJoints { Cfmi = Joint.Cfmi, dDegree = Joint.dDegree, dTime = Joint.dTime });
        }
        Hold.Jcount = Hold.Joints.Count;
        return Hold;
    }

    /// <summary>
    /// Hands a kept motion pattern to the timeline's paste preview, so it
    /// follows the pointer and is dropped with a click. It used to land at
    /// the playhead the instant the button was pressed, which is the one way
    /// a saved group of motions did not behave like a saved group of notes.
    /// </summary>
    private void PasteFavouriteMotions(Lanotalium.Editor.FavouritePattern Pattern)
    {
        if (Pattern.Motions == null || Pattern.Motions.Count == 0) return;
        if (TimeLineManager == null) return;

        List<Lanotalium.Chart.LanotaCameraBase> Shapes = new List<Lanotalium.Chart.LanotaCameraBase>();
        foreach (Lanotalium.Editor.FavouriteMotion Motion in Pattern.Motions)
        {
            Lanotalium.Chart.LanotaCameraBase New = NewMotionOfType(Motion.Type);
            if (New == null) continue;
            // Kept at the pattern's own timings; the preview measures its own
            // offsets from the earliest of them.
            Fill(New, Motion, 0);
            Shapes.Add(New);
        }
        if (Shapes.Count == 0) return;

        // Whatever was in the note clipboard is not what is being placed now.
        CancelPaste();
        _ClipboardHoldsMotions = true;
        TimeLineManager.LoadMotionsIntoClipboard(Shapes);
    }

    private static Lanotalium.Chart.LanotaCameraBase NewMotionOfType(int Type)
    {
        if (Type == 8 || Type == 11) return new Lanotalium.Chart.LanotaCameraXZ();
        if (Type == 10) return new Lanotalium.Chart.LanotaCameraY();
        if (Type == 13) return new Lanotalium.Chart.LanotaCameraRot();
        if (Type == 14) return new Lanotalium.Chart.LanotaCameraTrs();
        return null;
    }

    private static void Fill(Lanotalium.Chart.LanotaCameraBase Target, Lanotalium.Editor.FavouriteMotion Source, float Start)
    {
        Target.Type = Source.Type;
        Target.Time = Start + Source.Time;
        Target.Duration = Source.Duration;
        Target.ctp = Source.ctp;
        Target.ctp1 = Source.ctp1;
        Target.ctp2 = Source.ctp2;
        Target.cfmi = Source.cfmi;
        Target.cflg = Source.cflg;
    }

    /// <summary>
    /// Adds one motion, letting the editor's own checks refuse it: a motion
    /// landing on top of another is turned away with a message rather than
    /// quietly corrupting the row.
    /// </summary>
    private bool OperationAdd(Lanotalium.Chart.LanotaCameraXZ Motion) { return AddHorizontal(Motion, false, false, false); }
    private bool OperationAdd(Lanotalium.Chart.LanotaCameraY Motion) { return AddVertical(Motion, false, false, false); }
    private bool OperationAdd(Lanotalium.Chart.LanotaCameraRot Motion) { return AddRotation(Motion, false, false, false); }
}
