using System.Collections.Generic;
using Lanotalium.Chart;
using UnityEngine;

/// <summary>
/// Setting the ease of every joint of every selected rail in one go.
///
/// The inspector edits one joint of one rail at a time, which is the right
/// thing when a rail is being shaped by hand and the wrong thing when a run
/// of rails all want the same curve: with more than one rail picked up the
/// inspector has nothing to show at all, so the ease had to be set rail by
/// rail and joint by joint.
///
/// The numbers are the same 0 to 12 a motion takes, read by the same table,
/// and 0 is the straight one. Only the ease is written: where each joint
/// sits does not move, so a rail keeps its shape and only the way it travels
/// between its joints changes.
/// </summary>
public partial class LimOperationManager
{
    /// <summary>
    /// Returns false when there is nothing to write to: no rail selected, or
    /// none of the selected rails has a joint to carry an ease.
    /// </summary>
    public bool SetSelectedRailsEase(int Ease)
    {
        if (!IsGroupEaseInRange(Ease)) return false;

        List<LanotaJoints> Joints = new List<LanotaJoints>();
        List<int> Before = new List<int>();
        foreach (LanotaHoldNote Hold in SelectedHoldNote)
        {
            if (Hold.Joints == null) continue;
            foreach (LanotaJoints Joint in Hold.Joints)
            {
                Joints.Add(Joint);
                Before.Add(Joint.Cfmi);
            }
        }
        if (Joints.Count == 0) return false;

        bool Changed = false;
        for (int i = 0; i < Before.Count; ++i) if (Before[i] != Ease) { Changed = true; break; }
        if (!Changed) return true;

        LimInspectorManager Inspector = InspectorManager;
        WriteRailEase(Joints, Ease);
        RefreshJointInspector();

        Lanotalium.Editor.OperationSave OpSave = new Lanotalium.Editor.OperationSave();
        OpSave.Forward = new Lanotalium.Editor.OperationForward(() =>
        {
            WriteRailEase(Joints, Ease);
            if (Inspector != null) Inspector.OnSelectChange();
        });
        OpSave.Reverse = new Lanotalium.Editor.OperationReverse(() =>
        {
            for (int i = 0; i < Joints.Count; ++i) Joints[i].Cfmi = Before[i];
            if (Inspector != null) Inspector.OnSelectChange();
        });
        AddToOperationSaver(OpSave);
        return true;
    }

    private static void WriteRailEase(List<LanotaJoints> Joints, int Ease)
    {
        for (int i = 0; i < Joints.Count; ++i) Joints[i].Cfmi = Ease;
    }
}
