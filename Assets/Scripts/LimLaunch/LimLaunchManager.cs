using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LimLaunchManager : MonoBehaviour
{
    public Text VersionText;
    public Slider EnterLanotaliumSlider;
    public Text EnterLanotaliumText, TutorialText, SupportMeText, QuitText, ChartZoneText;
    public GameObject SupportMePanel;
    //public Dropdown PaypalQuantityDropdown;

    private Dictionary<string, string> _LaunchLanguageDict = new Dictionary<string, string>();

    private void Start()
    {
        VersionText.text = LimSystem.Version;
        SetLanguageDict();
        SetTexts();
        LimSystem.LanotaliumServer = "https://lanotalium.schwarzer.wang";
    }
    private void SetLanguageDict()
    {
        _LaunchLanguageDict.Add("EnterLanotalium_En", "<size=30>Enter</size>  <size=45>Lanotalium</size>");
        _LaunchLanguageDict.Add("Tutorial_En", "Tutorial");
        _LaunchLanguageDict.Add("SupportMe_En", "Donate");
        _LaunchLanguageDict.Add("About_En", "About");
        _LaunchLanguageDict.Add("Quit_En", "Quit");
        _LaunchLanguageDict.Add("ChartZone_En", "ChartZone");

        _LaunchLanguageDict.Add("EnterLanotalium_Es", "<size=30>Entrar en</size>  <size=45>Lanotalium</size>");
        _LaunchLanguageDict.Add("Tutorial_Es", "Tutorial");
        _LaunchLanguageDict.Add("SupportMe_Es", "Donar");
        _LaunchLanguageDict.Add("About_Es", "Acerca de");
        _LaunchLanguageDict.Add("Quit_Es", "Salir");
        _LaunchLanguageDict.Add("ChartZone_Es", "ChartZone");
    }
    private void SetTexts()
    {
        if (LimSystem.Preferences.LanguageName == "Español")
        {
            ChartZoneText.text = _LaunchLanguageDict["ChartZone_Es"];
            EnterLanotaliumText.text = _LaunchLanguageDict["EnterLanotalium_Es"];
            TutorialText.text = _LaunchLanguageDict["Tutorial_Es"];
            SupportMeText.text = _LaunchLanguageDict["SupportMe_Es"];
            QuitText.text = _LaunchLanguageDict["Quit_Es"];
        }
        else
        {
            ChartZoneText.text = _LaunchLanguageDict["ChartZone_En"];
            EnterLanotaliumText.text = _LaunchLanguageDict["EnterLanotalium_En"];
            TutorialText.text = _LaunchLanguageDict["Tutorial_En"];
            SupportMeText.text = _LaunchLanguageDict["SupportMe_En"];
            QuitText.text = _LaunchLanguageDict["Quit_En"];
        }
    }
    public void OpenSupportMePanel()
    {
        if (SupportMePanel.activeInHierarchy) SupportMePanel.SetActive(false);
        else SupportMePanel.SetActive(true);
    }
    public void QuitFromMenu()
    {
        UnityEngine.Application.Quit();
    }
    public void EnterLanotalium()
    {
        StartCoroutine(EnterLanotaliumCoroutine());
    }
    IEnumerator EnterLanotaliumCoroutine()
    {
        EnterLanotaliumText.text = (LimSystem.Preferences.LanguageName == "Español" ? "Cargando" : "Now Loading");
        AsyncOperation AsyncLoading = SceneManager.LoadSceneAsync("LimTuner");
        while (!AsyncLoading.isDone)
        {
            EnterLanotaliumSlider.value = AsyncLoading.progress;
            yield return null;
        }
    }
    public void OpenOfficialWebsite()
    {
        UnityEngine.Application.OpenURL(LimSystem.LanotaliumServer);
    }
    public void OpenTutorialWebsite()
    {
        UnityEngine.Application.OpenURL(LimSystem.LanotaliumServer + "/lanotalium/docs/Tutorial_En.pdf");
    }
    public void ToChartZone()
    {
        LimChartZoneManager.FromScene = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene("LimChartZone");
    }
    public void RaisePaypalRequest()
    {
        Application.OpenURL("https://lanotalium.schwarzer.wang/PaypalSupport.html");
    }
}
