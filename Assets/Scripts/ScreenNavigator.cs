using UnityEngine;

public class ScreenNavigator : MonoBehaviour
{
    public GameObject titleScreen;
    public GameObject levelSelectScreen;
    public BgmPlayer bgm;
    [Tooltip("Opening cutscene, played over the title screen once per app launch.")]
    public IntroCutscene intro;

    // Set before reloading the scene (e.g. after switching accounts) to land back on the
    // level select instead of the title screen.
    public static bool OpenLevelSelectOnLoad;
    private static bool introPlayed;   // static: a scene reload (account switch) doesn't replay it

    // Fresh for every launch, even with the editor's domain reload turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetIntroFlag() => introPlayed = false;

    private void Start()
    {
        if (!OpenLevelSelectOnLoad)
        {
            if (intro != null && !introPlayed && IntroToggle.IntroEnabled)
            {
                introPlayed = true;
                intro.Play(() => { if (bgm != null) bgm.PlayTitle(); });
            }
            else if (bgm != null) bgm.PlayTitle();
            return;
        }
        OpenLevelSelectOnLoad = false;
        ShowLevelSelect();
    }

    public void ShowLevelSelect()
    {
        titleScreen.SetActive(false);
        levelSelectScreen.SetActive(true);
        if (bgm != null) bgm.PlayLevelSelect();
    }

    public void ShowTitle()
    {
        levelSelectScreen.SetActive(false);
        titleScreen.SetActive(true);
        if (bgm != null) bgm.PlayTitle();
    }
}
