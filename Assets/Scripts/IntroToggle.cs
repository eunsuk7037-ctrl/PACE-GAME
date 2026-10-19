using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Settings button that turns the opening cutscene on/off. A device setting like the language,
// so it lives on plain PlayerPrefs rather than the per-account GameSave.
public class IntroToggle : MonoBehaviour
{
    private const string Key = "Settings_IntroEnabled";

    public static bool IntroEnabled => PlayerPrefs.GetInt(Key, 1) == 1;

    public TMP_Text titleLabel;   // "Intro" next to the button
    public TMP_Text stateLabel;   // ON / OFF on the button
    public Color onColor = Color.white;
    public Color offColor = new Color(1f, 1f, 1f, 0.45f);

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        Refresh();
    }

    private void OnDisable() => LocalizationManager.LanguageChanged -= OnLanguageChanged;
    private void OnLanguageChanged(int _) => Refresh();

    [Tooltip("Played right away when the toggle is switched ON.")]
    public IntroCutscene intro;
    public BgmPlayer bgm;
    public ScreenNavigator screens;

    private void Toggle()
    {
        bool on = !IntroEnabled;
        PlayerPrefs.SetInt(Key, on ? 1 : 0);
        PlayerPrefs.Save();
        Refresh();

        // Switching it on shows it now; afterwards the music of whichever screen is open comes back.
        if (on && intro != null && !intro.IsPlaying)
        {
            intro.Play(() =>
            {
                if (bgm == null) return;
                if (screens != null && screens.titleScreen.activeInHierarchy) bgm.PlayTitle();
                else bgm.PlayLevelSelect();
            });
        }
    }

    private void Refresh()
    {
        bool on = IntroEnabled;
        if (titleLabel != null) titleLabel.text = LocalizationManager.Get("intro", "Intro");
        if (stateLabel != null)
        {
            stateLabel.text = on ? LocalizationManager.Get("on", "ON") : LocalizationManager.Get("off", "OFF");
            stateLabel.color = on ? onColor : offColor;
        }
    }
}
