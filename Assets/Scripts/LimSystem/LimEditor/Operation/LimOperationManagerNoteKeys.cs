using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The number row, 1 to 5, as a shortcut for the two things most often
/// changed while charting.
///
///   Click To Create on          : 1 Click, 2 Flick In, 3 Flick Out,
///                                 4 Catch, 5 Rail
///   Click To Create on  + Shift : 1, 2, 3, 4 set the size of the next note
///                                 to 0, 1, 2, 3
///   Click To Create off         : 1, 2, 3 set the size of the selected
///                                 notes, and 4 puts them back to the
///                                 default size
///   Click To Create off + Shift : 1 to 5 set the kind of the selected
///                                 notes, the same five the tool places
///
/// The three never overlap: with the tool on, the keys say what the next
/// note will be and Shift says how big it will be, and with it off they
/// change the notes already chosen. Nothing happens while a field is being
/// typed into, or with Ctrl or Alt held down, which belong to other
/// shortcuts.
///
/// The sizes read 1, 2, 3 and 4 rather than 1, 2, 3 and 0 because a keyboard
/// row runs that way: the fourth key is the fourth size, which happens to be
/// the one Lanota calls 0.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>Lanota's default note size, which the fourth key returns to.</summary>
    private const int DefaultNoteSize = 0;

    public void DetectNoteKeys()
    {
        if (TunerManager == null || !TunerManager.isInitialized) return;
        if (_PasteActive || IsNoteDragInProgress) return;
        if (IsTypingInTextField()) return;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return;
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) { DetectSizeKeys(); return; }

        int Number = PressedNumber();
        if (Number == 0) return;

        if (LimClickToCreateManager.IsCreating)
        {
            if (LimClickToCreateManager.Instance != null) LimClickToCreateManager.Instance.SetTypeByNumber(Number);
            return;
        }
        if (!HasNoteSelection) return;
        if (Number > 4) return;
        SetSelectionSize(Number == 4 ? DefaultNoteSize : Number);
    }

    /// <summary>
    /// Shift and a number choose how big the next note will be, while the
    /// tool that places it is on. Sizes run 0 to 3, so the key is one more
    /// than the size, the same way the four keys line up with the four
    /// entries of the Size list.
    ///
    /// With the tool off the same keys keep doing what they did before, so
    /// Shift is free there for the arrow shortcuts and nothing is taken away.
    /// </summary>
    private void DetectSizeKeys()
    {
        int Number = PressedNumber();
        if (Number == 0) return;
        if (LimClickToCreateManager.IsCreating)
        {
            if (Number > 4) return;
            if (LimClickToCreateManager.Instance != null) LimClickToCreateManager.Instance.SetSizeByNumber(Number);
            return;
        }
        if (!HasNoteSelection) return;
        SetSelectionType(NumberToNoteType(Number));
    }

    /// <summary>
    /// 1 Click, 2 Flick In, 3 Flick Out, 4 Catch, 5 Rail. Lanota writes those
    /// five as 0, 2, 3, 4, 5, which is the one place the leading zero it
    /// gives the click note still shows.
    /// </summary>
    private static int NumberToNoteType(int Number)
    {
        return Number == 1 ? 0 : Number;
    }

    /// <summary>
    /// Retypes everything selected. Turning a note into a rail, or a rail
    /// back into a note, replaces the object rather than editing it, which is
    /// why the selection is let go afterwards; those two are the conversions
    /// the Creator's own buttons make, and like them they are not undoable.
    /// A change between the four kinds of tap note is, one entry per note.
    /// </summary>
    private void SetSelectionType(int Type)
    {
        List<Lanotalium.Chart.LanotaTapNote> Taps = new List<Lanotalium.Chart.LanotaTapNote>(SelectedTapNote);
        List<Lanotalium.Chart.LanotaHoldNote> Holds = new List<Lanotalium.Chart.LanotaHoldNote>(SelectedHoldNote);
        bool Replaced = false;

        if (Type == 5)
        {
            if (Taps.Count == 0) return;
            foreach (Lanotalium.Chart.LanotaTapNote Tap in Taps) ConvertTapNoteToHoldNote(Tap);
            Replaced = true;
        }
        else
        {
            foreach (Lanotalium.Chart.LanotaTapNote Tap in Taps)
                if (Tap.Type != Type) SetTapNoteType(Tap, Type);
            foreach (Lanotalium.Chart.LanotaHoldNote Hold in Holds)
            {
                ConvertHoldNoteToTapNote(Hold, Type);
                Replaced = true;
            }
        }

        if (Replaced) SelectNothing();
        if (InspectorManager != null) InspectorManager.OnSelectChange();
    }

    /// <summary>The number row along the top, not the keypad.</summary>
    private static int PressedNumber()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) return 5;
        return 0;
    }

    /// <summary>
    /// Resizes everything selected as one undo entry, rather than one entry
    /// per note the way the inspector does it.
    /// </summary>
    private void SetSelectionSize(int Size)
    {
        List<Lanotalium.Chart.LanotaTapNote> Taps = new List<Lanotalium.Chart.LanotaTapNote>(SelectedTapNote);
        List<Lanotalium.Chart.LanotaHoldNote> Holds = new List<Lanotalium.Chart.LanotaHoldNote>(SelectedHoldNote);
        List<int> TapSizes = new List<int>();
        List<int> HoldSizes = new List<int>();
        bool Changed = false;
        foreach (Lanotalium.Chart.LanotaTapNote Tap in Taps)
        {
            TapSizes.Add(Tap.Size);
            if (Tap.Size != Size) Changed = true;
        }
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in Holds)
        {
            HoldSizes.Add(Hold.Size);
            if (Hold.Size != Size) Changed = true;
        }
        if (!Changed) return;

        LimInspectorManager Inspector = InspectorManager;
        WriteSelectionSize(Taps, Holds, Size);
        if (Inspector != null) Inspector.OnSelectChange();

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            WriteSelectionSize(Taps, Holds, Size);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            for (int i = 0; i < Taps.Count; ++i) SetTapNoteSize(Taps[i], TapSizes[i], false);
            for (int i = 0; i < Holds.Count; ++i) SetHoldNoteSize(Holds[i], HoldSizes[i], false);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        AddToOperationSaver(OpSave);
    }

    private void WriteSelectionSize(List<Lanotalium.Chart.LanotaTapNote> Taps,
                                    List<Lanotalium.Chart.LanotaHoldNote> Holds, int Size)
    {
        foreach (Lanotalium.Chart.LanotaTapNote Tap in Taps) SetTapNoteSize(Tap, Size, false);
        foreach (Lanotalium.Chart.LanotaHoldNote Hold in Holds) SetHoldNoteSize(Hold, Size, false);
    }
}
