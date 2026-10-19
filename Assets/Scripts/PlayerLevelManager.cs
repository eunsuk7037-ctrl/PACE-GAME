using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Clearing a chapter grants experience (more for later chapters); each level gained
// raises the player's max health a little. Tuned so clearing chapters 1-9 once each
// lands at about level 10 - the health the chapter 10 balance was built around.
public class PlayerLevelManager : MonoBehaviour
{
    public const int MaxLevel = 50;
    public const int ExpPerChapter = 100;    // clearing chapter N grants N x this
    public const int ExpPerLevelStep = 100;  // reaching level L+1 needs L x this
    public const int MaxHitsPerLevel = 2;

    private const string LevelKey = "PlayerLevel";
    private const string ExpKey = "PlayerExp";

    public static PlayerLevelManager Instance { get; private set; }

    public TMP_Text levelText;
    public Slider expSlider;
    public TMP_Text expText;

    [Header("Level Up Motion")]
    [Tooltip("Popped each time the EXP bar fills and the level ticks up.")]
    public RectTransform levelIcon;
    [Tooltip("Floats in after the bar finishes filling, if any level was gained. Starts hidden.")]
    public TMP_Text levelUpText;
    public AudioClip levelUpSound;
    [Range(0f, 1f)] public float levelUpVolume = 0.7f;
    [Tooltip("Seconds for the EXP bar to fill from empty to full.")]
    public float fillSecondsPerLevel = 0.6f;
    public float levelUpTextHoldSeconds = 1.5f;

    // What the bar currently shows: stage clears save the new values right away, but the bar
    // keeps showing these until PlayGainMotion animates it up to them on the chapter select screen.
    private int shownLevel, shownExp;
    private AudioSource sfxSource;
    private Coroutine gainRoutine, levelUpTextRoutine;

    private void Awake()
    {
        Instance = this;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        if (levelUpText != null) levelUpText.gameObject.SetActive(false);
        RefreshUI();
    }

    public static int GetLevel() => Mathf.Clamp(GameSave.GetInt(LevelKey, 1), 1, MaxLevel);
    public static int GetExp() => GameSave.GetInt(ExpKey, 0);
    public static int ExpToNextLevel(int level) => ExpPerLevelStep * level;
    public static int ExpForStageClear(int stageIndex) => ExpPerChapter * (stageIndex + 1);

    // Extra player hits (max health) from levels above 1.
    public static int MaxHitsBonus => (GetLevel() - 1) * MaxHitsPerLevel;

    // Returns how many levels were gained.
    public static int AddStageClearExperience(int stageIndex)
    {
        int startLevel = GetLevel();
        int level = startLevel;
        if (level >= MaxLevel) return 0;

        int exp = GetExp() + ExpForStageClear(stageIndex);
        while (level < MaxLevel && exp >= ExpToNextLevel(level))
        {
            exp -= ExpToNextLevel(level);
            level++;
        }
        if (level >= MaxLevel) exp = 0;

        GameSave.SetInt(LevelKey, level);
        GameSave.SetInt(ExpKey, exp);
        GameSave.Save();

        // No RefreshUI here: the bar catches up with PlayGainMotion once the player is back on chapter select.
        return level - startLevel;
    }

    public void RefreshUI()
    {
        shownLevel = GetLevel();
        shownExp = GetExp();
        Show(shownLevel, shownExp);
    }

    private void Show(int level, float exp)
    {
        int needed = ExpToNextLevel(level);
        bool maxed = level >= MaxLevel;
        exp = Mathf.Min(exp, needed);

        if (levelText != null) levelText.text = level.ToString();
        if (expSlider != null)
        {
            expSlider.maxValue = needed;
            expSlider.value = maxed ? needed : exp;
        }
        if (expText != null) expText.text = maxed ? "MAX" : Mathf.FloorToInt(exp) + " / " + needed;
    }

    // Fills the EXP bar from what it last showed up to the saved values, ticking the level up each
    // time the bar fills; once it's done, plays the level up sound and floats the LEVEL UP text.
    public void PlayGainMotion()
    {
        if (gainRoutine != null) StopCoroutine(gainRoutine);
        gainRoutine = StartCoroutine(GainMotion());
    }

    private IEnumerator GainMotion()
    {
        int startLevel = shownLevel;
        int level = shownLevel;
        float exp = shownExp;
        int targetLevel = GetLevel();
        int targetExp = GetExp();

        yield return new WaitForSeconds(0.3f);   // let the chapter select screen settle first

        while (true)
        {
            bool levelsLeft = level < targetLevel;
            int needed = ExpToNextLevel(level);
            float to = levelsLeft ? needed : targetExp;

            if (to > exp)
            {
                float from = exp;
                float seconds = Mathf.Max(0.15f, fillSecondsPerLevel * (to - from) / needed);
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    float p = t / seconds;
                    Show(level, Mathf.Lerp(from, to, 1f - (1f - p) * (1f - p)));   // ease out
                    yield return null;
                }
                exp = to;
                Show(level, exp);
            }

            if (!levelsLeft) break;

            level++;
            exp = 0f;
            Show(level, exp);
            yield return PopLevelIcon();
        }

        RefreshUI();
        gainRoutine = null;

        int gained = level - startLevel;
        if (gained <= 0) yield break;

        if (levelUpSound != null) sfxSource.PlayOneShot(levelUpSound, levelUpVolume * VolumeSettings.SfxGain);
        if (levelUpText != null)
        {
            if (levelUpTextRoutine != null) StopCoroutine(levelUpTextRoutine);
            levelUpTextRoutine = StartCoroutine(FloatLevelUpText(level, gained));
        }
    }

    private IEnumerator PopLevelIcon()
    {
        if (levelIcon == null) yield break;

        const float seconds = 0.25f;
        Vector3 baseScale = levelIcon.localScale;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            levelIcon.localScale = baseScale * (1f + 0.3f * Mathf.Sin(t / seconds * Mathf.PI));
            yield return null;
        }
        levelIcon.localScale = baseScale;
    }

    private IEnumerator FloatLevelUpText(int level, int gained)
    {
        var rt = levelUpText.rectTransform;
        Vector2 basePos = rt.anchoredPosition;
        levelUpText.text = string.Format(LocalizationManager.Get("level_up", "LEVEL UP! Lv.{0} (Max HP +{1})"),
            level, gained * MaxHitsPerLevel);
        levelUpText.gameObject.SetActive(true);

        // Pop in...
        const float popSeconds = 0.3f;
        for (float t = 0f; t < popSeconds; t += Time.deltaTime)
        {
            float p = t / popSeconds;
            levelUpText.alpha = p;
            rt.localScale = Vector3.one * (0.6f + 0.4f * p + 0.15f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        levelUpText.alpha = 1f;
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(levelUpTextHoldSeconds);

        // ...then drift up and fade away.
        const float fadeSeconds = 0.5f;
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            float p = t / fadeSeconds;
            levelUpText.alpha = 1f - p;
            rt.anchoredPosition = basePos + Vector2.up * (30f * p);
            yield return null;
        }

        levelUpText.gameObject.SetActive(false);
        levelUpText.alpha = 1f;
        rt.anchoredPosition = basePos;
        levelUpTextRoutine = null;
    }
}
