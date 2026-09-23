using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reset Layout asks first. The scene's button reset every window straight
/// away, and one stray click in the Setting menu threw away a layout the user
/// had spent a while arranging; it now closes the menu and asks in the usual
/// message box, and only OK resets.
/// </summary>
public partial class LimTopMenuManager
{
    private void SetUpResetLayoutConfirm()
    {
        if (ResetLayoutText == null) return;
        // Walked up by hand: GetComponentInParent finds nothing while the
        // Setting menu is hidden, which it is at start.
        Button Reset = null;
        for (Transform At = ResetLayoutText.transform; At != null && Reset == null; At = At.parent) Reset = At.GetComponent<Button>();
        if (Reset == null) return;
        Reset.onClick = new Button.ButtonClickedEvent();
        Reset.onClick.AddListener(AskResetLayout);
    }

    private void AskResetLayout()
    {
        if (SettingPanel != null) SettingPanel.SetActive(false);
        LimEditorManager Editor = LimEditorManager.Instance != null ? LimEditorManager.Instance : FindObjectOfType<LimEditorManager>();
        if (Editor == null) return;
        if (MessageBoxManager.Instance == null) return;
        MessageBoxManager.Instance.ShowMessage(LimLanguageManager.TextDict["TopMenu_Setting_ResetLayout_Confirm"], () => { Editor.ResetEditorLayout(); });
    }
}
