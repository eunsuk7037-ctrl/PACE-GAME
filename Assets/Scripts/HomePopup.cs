using UnityEngine;
using TMPro;

public class HomePopup : MonoBehaviour
{
    public GameObject panelRoot;
    public TMP_Text titleText;
    public GameObject continueButton;
    public GameObject restartButton;
    public GameObject accountButton;
    public GameObject settingsButton;
    public GameObject quitButton;

    public StageProgress stageProgress;
    public LevelSelectActions levelSelectActions;
    public ScreenNavigator screenNavigator;

    // In-stage pause shows 4 slots (Continue, Restart, Settings, Quit); the level-select
    // menu swaps Restart out for Account and only shows 3, so the row is re-centered
    // instead of reusing the 4-slot x positions (which would leave it lopsided to the right).
    private static readonly float[] FourSlotX = { -330f, -110f, 110f, 330f };
    private static readonly float[] ThreeSlotX = { -220f, 0f, 220f };

    private bool InStage => stageProgress != null && stageProgress.IsInStage;

    public void Show()
    {
        bool inStage = InStage;

        if (titleText != null) titleText.text = inStage ? "PAUSE" : "MENU";
        if (continueButton != null) continueButton.SetActive(inStage);
        if (restartButton != null) restartButton.SetActive(inStage);
        if (accountButton != null) accountButton.SetActive(!inStage);

        if (inStage) LayOutRow(FourSlotX, continueButton, restartButton, settingsButton, quitButton);
        else LayOutRow(ThreeSlotX, accountButton, settingsButton, quitButton);

        panelRoot.SetActive(true);
        if (inStage) Time.timeScale = 0f;
    }

    private static void LayOutRow(float[] slotX, params GameObject[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            var rt = (RectTransform)buttons[i].transform;
            rt.anchoredPosition = new Vector2(slotX[i], rt.anchoredPosition.y);
        }
    }

    // Closes the popup; resumeTime unpauses the stage (Settings/Account keep it paused
    // behind their own panel).
    private void Close(bool resumeTime)
    {
        if (resumeTime) Time.timeScale = 1f;
        panelRoot.SetActive(false);
    }

    public void OnContinuePressed() => Close(true);

    public void OnRestartPressed()
    {
        Close(true);
        if (stageProgress != null) stageProgress.RestartCurrentStage();
    }

    public void OnSettingsPressed()
    {
        Close(false);
        if (levelSelectActions != null) levelSelectActions.ShowSettings();
    }

    public void OnAccountPressed()
    {
        Close(false);
        if (levelSelectActions != null) levelSelectActions.ShowAccount();
    }

    public void OnQuitPressed()
    {
        Close(true);
        if (InStage) stageProgress.ExitStage();
        else if (screenNavigator != null) screenNavigator.ShowTitle();
    }
}
