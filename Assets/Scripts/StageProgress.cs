using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class HealItemConfig
{
    public string itemName;
    public int healAmount;
    public int price;
    public Sprite icon;
}

// Explicit values: these are serialized in the scene, so removing an entry must not
// renumber the rest (3 was the removed Swarm pattern).
public enum PatternType
{
    Wall = 0,   // straight line sweeping across the screen, with one escape gap
    Ring = 1,   // circle closing in from all sides
    Stream = 2, // single-file column charging through where the player stood
    Snake = 4,  // single-file column wiggling side to side as it crosses
    Spiral = 5, // ring that rotates while it closes in
}

[System.Serializable]
public class ChapterPatternSet
{
    public PatternType[] patterns;

    public ChapterPatternSet(params PatternType[] patterns)
    {
        this.patterns = patterns;
    }
}

public class StageProgress : MonoBehaviour
{
    public Transform levelSelectRoot;
    public Sprite normalContainerSprite;
    public Sprite lockedContainerSprite;

    public GameObject gameplayStage;
    public GameObject player;
    [Tooltip("Regular monster per stage, index = stage index (0=MERCURY .. 9=SUN). Each stage is invaded by its own planet.")]
    public Monster[] planetMonsters = new Monster[10];
    public float monsterSpawnDelay = 0.5f;

    [Header("Monster Spawning")]
    public int startMonsterCount = 1;
    public int maxMonsterCount = 6;
    [Tooltip("Extra monsters allowed on screen at once per stage index (0-based), on top of maxMonsterCount.")]
    public int maxMonsterCountPerStage = 2;
    public float spawnInterval = 2.5f;
    public float minSpawnInterval = 0.8f;
    public float spawnRampDuration = 60f;
    [Tooltip("Extra max health added to each spawned regular/elite monster per stage index (0-based).")]
    public int monsterHealthPerStage = 6;
    [Tooltip("Extra move speed added to each spawned regular/elite monster per stage index (0-based).")]
    public float monsterSpeedPerStage = 9f;
    [Tooltip("How many stage indices it takes for spawned monsters to gain +1 attack power.")]
    public int stagesPerAttackPowerIncrease = 2;
    [Tooltip("Attack interval reduction (faster attacks) per stage index, floored at minMonsterAttackInterval.")]
    public float monsterAttackIntervalReductionPerStage = 0.012f;
    public float minMonsterAttackInterval = 0.12f;

    [Header("Circle Wave (later chapters)")]
    [Tooltip("0-based stage index from which the surrounding circle wave can occur (e.g. 3 = chapter 4).")]
    public int circleWaveMinStageIndex = 3;
    [Tooltip("Seconds between circle waves while in a stage that has them.")]
    public float circleWaveInterval = 25f;
    public int circleWaveMonsterCount = 20;
    [Tooltip("Fixed spawn radius for the circle wave, in canvas units - kept well beyond the screen edge so the ring starts fully off-screen and visibly closes in, instead of appearing already at the edge.")]
    public float circleWaveRadius = 1500f;
    [Tooltip("Circle wave monsters close in slower than a normal spawn, to read as a deliberate surround-and-advance beat.")]
    public float circleWaveSpeedMultiplier = 0.45f;

    [Header("Pattern Waves (Survivors-style, after enough kills in a stage)")]
    [Tooltip("Kills needed in a stage before pattern waves start mixing in with the normal spawns.")]
    public int patternStartKills = 20;
    public float patternFirstDelay = 2f;
    [Tooltip("Seconds between pattern waves once they've started.")]
    public float patternInterval = 16f;
    [Tooltip("Max health of formation monsters (and the circle wave) relative to a regular monster of the same chapter.")]
    [Range(0.1f, 1f)] public float patternHealthMultiplier = 0.5f;
    [Tooltip("Wall/stream monsters move in a straight line at (their speed x this), capped by patternMaxSpeed so a player can always outrun them.")]
    public float patternSpeedMultiplier = 1.4f;
    public float patternMaxSpeed = 240f;
    public float patternSlotSpacing = 150f;
    [Tooltip("Half-width of the escape gap left in a wall, centered near the player.")]
    public float patternWallGapHalfWidth = 190f;
    public float patternEdgeMargin = 160f;
    public int patternRingBaseCount = 14;
    [Tooltip("Extra ring members per stage index (0-based).")]
    public int patternRingCountPerStage = 1;
    public float patternRingSpeedMultiplier = 0.8f;
    public int patternStreamLaneLength = 6;
    [Tooltip("Pattern monsters alive at once; a new wave is skipped while over this.")]
    public int patternMaxAlive = 70;
    [Tooltip("Which formations each chapter uses, index = stage index. Early chapters each introduce one signature pattern; later ones mix them.")]
    public ChapterPatternSet[] chapterPatterns =
    {
        new ChapterPatternSet(PatternType.Ring),                                         // Mercury
        new ChapterPatternSet(PatternType.Stream),                                       // Venus
        new ChapterPatternSet(PatternType.Ring, PatternType.Stream),                     // Earth
        new ChapterPatternSet(PatternType.Snake),                                        // Mars
        new ChapterPatternSet(PatternType.Wall),                                         // Jupiter
        new ChapterPatternSet(PatternType.Spiral),                                       // Saturn
        new ChapterPatternSet(PatternType.Ring, PatternType.Snake),                      // Uranus
        new ChapterPatternSet(PatternType.Wall, PatternType.Stream, PatternType.Spiral), // Neptune
        new ChapterPatternSet(PatternType.Snake, PatternType.Spiral, PatternType.Wall),  // Pluto
        new ChapterPatternSet(PatternType.Wall, PatternType.Ring, PatternType.Stream, PatternType.Snake, PatternType.Spiral), // Sun
    };
    [Tooltip("Gap between formation members along a column/snake, in canvas units. Keep above the monster's size (128) so they don't overlap.")]
    public float patternColumnSpacing = 160f;
    public int patternSnakeLength = 12;
    public float patternSnakeAmplitude = 170f;
    public float patternSnakeWavelength = 650f;
    public float patternSpiralDegreesPerSecond = 40f;
    public float patternSpiralSpeedMultiplier = 0.6f;

    [Header("Boss / Stage Clear")]
    [Tooltip("Boss per stage, index = stage index. Each stage's boss previews next chapter's planet (index 9/SUN is unused - finalBoss takes over instead).")]
    public Monster[] nextPlanetBossTemplates = new Monster[10];
    public float bossSpawnDelay = 300f;
    [Tooltip("Extra max health added to every boss (including the final boss) per stage index, so later chapters' bosses are meaningfully tougher.")]
    public int bossHealthPerStage = 40;
    public CanvasGroup stageClearGroup;
    public TMP_Text stageClearText;
    public AudioClip stageClearSound;   // fanfare as the STAGE CLEAR text comes up (follows the Music setting)

    [Header("Boss Membrane (chapter bosses, not the final boss)")]
    public Sprite bossShieldSprite;
    public Color bossShieldColor = new Color(0.72f, 0.38f, 1f, 0.55f);
    [Tooltip("Electric spark animation frames, played on the membrane now and then.")]
    public Sprite[] bossSparkFrames;

    [Header("Final Boss (3 phases, bullet patterns)")]
    public Sprite bossBulletSprite;
    [Tooltip("Clear glassy barrier; one breaks after each of the first two phases.")]
    public Sprite bossBarrierSprite;
    public int bossBulletDamage = 2;
    [Tooltip("After a bullet hits, further bullets are ignored this long.")]
    public float bulletHitInvulnerability = 0.6f;
    private float lastBulletHitTime = -10f;
    [Range(0f, 1f)] public float stageClearVolume = 0.5f;

    [Header("Chapter Monster Density Boost")]
    [Tooltip("0-based stage indices that spawn extra monsters (e.g. 1 = chapter 2).")]
    public int[] extraMonsterStageIndices = new int[] { 1 };
    public int extraMaxMonsterCount = 3;
    public float extraSpawnIntervalReduction = 0.4f;

    [Header("Final Stage Boss Override")]
    [Tooltip("0-based index of the stage that spawns finalBoss instead of stageBoss (e.g. 9 = the last planet).")]
    public int finalBossStageIndex = 9;
    public Monster finalBoss;

    [Header("Game Over")]
    public CanvasGroup gameOverGroup;
    public TMP_Text gameOverText;
    public AudioClip gameOverSound;   // jingle as the GAME OVER text comes up (follows the Music setting)
    [Range(0f, 1f)] public float gameOverVolume = 0.5f;
    [Tooltip("Player health at level 1; each player level adds PlayerLevelManager.MaxHitsPerLevel on top.")]
    public int maxPlayerHits = 10;

    [Header("Ending")]
    [Tooltip("Plays after the final chapter is cleared.")]
    public EndingCredits endingCredits;
    public GameObject playerHealthBarRoot;
    public Image playerHealthFill;
    [Tooltip("Fill sprites from low to high health: [0]=red [1]=orange [2]=yellow [3]=green. Swapped in as the bar drains; falls back to tinting a plain sprite if left empty.")]
    public Sprite[] healthBarFillSprites;

    [Header("Rewards")]
    public TMP_Text goldText;
    public int minGoldDrop = 7;
    public int maxGoldDrop = 9;
    public int bossGoldDrop = 50;
    public int bossGoldPerStage = 25;   // later chapters' bosses pay more: bossGoldDrop + this x stage index
    [Range(0f, 1f)] public float goldDropChance = 0.8f;   // regular monsters only; bosses always drop gold
    public GameObject coinDropPrefab;

    [Header("Elite Monster")]
    public Monster stageEliteMonster;
    [Range(0f, 1f)] public float eliteSpawnChance = 0.15f;
    [Tooltip("Added to eliteSpawnChance per stage index (0-based).")]
    public float eliteSpawnChancePerStage = 0.03f;
    public GameObject mushroomDropPrefab;
    [Range(0f, 1f)] public float eliteMushroomDropChance = 0.3f;
    [Tooltip("Tint applied to the (shared) elite monster sprite per stage index, so it reads as that stage's planet color.")]
    public Color[] eliteColorPerStage = new Color[]
    {
        new Color(0.72f, 0.68f, 0.64f, 1f), // Mercury - grey
        new Color(0.94f, 0.85f, 0.64f, 1f), // Venus - pale cream
        new Color(0.42f, 0.68f, 0.86f, 1f), // Earth - blue
        new Color(0.85f, 0.45f, 0.35f, 1f), // Mars - red
        new Color(0.88f, 0.72f, 0.52f, 1f), // Jupiter - tan
        new Color(0.92f, 0.80f, 0.58f, 1f), // Saturn - gold
        new Color(0.68f, 0.90f, 0.90f, 1f), // Uranus - pale cyan
        new Color(0.38f, 0.48f, 0.85f, 1f), // Neptune - deep blue
        new Color(0.80f, 0.70f, 0.62f, 1f), // Pluto - light brown
        new Color(1.00f, 0.80f, 0.30f, 1f), // Sun - yellow
    };

    [Header("Item Box")]
    [Tooltip("One slot per healItems[] entry, same index - lets the player hold several heal item types at once instead of just one.")]
    public Button[] itemBoxButtons;
    public Image[] itemBoxIcons;
    public TMP_Text[] itemBoxCountTexts;
    public HealItemConfig[] healItems;

    public CanvasGroup stageLabelGroup;
    public TMP_Text stageLabelText;
    public CanvasGroup stageNameGroup;
    public TMP_Text stageNameText;
    public GameObject[] levelSelectUIToHide;
    public BgmPlayer bgm;

    [Tooltip("Testing/preview only: opens every stage without touching save progress. Turn off before release.")]
    public bool unlockAllForTesting;

    private static readonly string[] PlanetNames =
    {
        "MERCURY", "VENUS", "EARTH", "MARS", "JUPITER",
        "SATURN", "URANUS", "NEPTUNE", "PLUTO", "SUN"
    };

    private const string ProgressKey = "UnlockedStageCount";
    private const string GoldKey = "PlayerGold";
    private const string HeldItemCountKeyPrefix = "HeldItemCount_";
    private int unlockedCount;
    private Transform gridContent;
    private Coroutine introRoutine;
    private Coroutine spawnRoutine;
    private Coroutine circleWaveRoutine;
    private Coroutine patternRoutine;
    private int killsThisStage;
    private bool patternsStarted;
    private readonly List<Monster> patternMonsters = new List<Monster>();
    private Coroutine bossSpawnRoutine;
    private Coroutine stageEndRoutine;
    private readonly List<Monster> activeMonsters = new List<Monster>();
    private readonly List<Monster> activeBosses = new List<Monster>();
    private int playerHitCount;
    private bool tookDamageThisStage;
    private bool isStageEnding;
    private AudioSource sfxSource;

    private void Awake()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        gridContent = levelSelectRoot.Find("Level_Select_UI/Scrollable_Area/Grid_Content");
        // With everything open, the clear sequence never reaches its "unlock next stage" branch,
        // so the saved progress stays untouched.
        unlockedCount = unlockAllForTesting
            ? PlanetNames.Length
            : Mathf.Clamp(GameSave.GetInt(ProgressKey, 1), 1, PlanetNames.Length);
        MigrateLegacyHeldItem();
        RefreshAll();
        RefreshGoldText();
        RefreshItemBox();

        if (itemBoxButtons != null)
        {
            for (int i = 0; i < itemBoxButtons.Length; i++)
            {
                int index = i;
                if (itemBoxButtons[index] != null) itemBoxButtons[index].onClick.AddListener(() => UseHealItemAt(index));
            }
        }
    }

    // Older builds stored a single held item type/count; fold it into the new
    // per-item-type storage once so nobody loses whatever they were holding.
    private void MigrateLegacyHeldItem()
    {
        const string legacyTypeKey = "HeldItemType";
        const string legacyCountKey = "HeldItemCount";

        string legacyType = GameSave.GetString(legacyTypeKey, "");
        if (string.IsNullOrEmpty(legacyType)) return;

        int legacyCount = GameSave.GetInt(legacyCountKey, 0);
        if (legacyCount > 0) SetHeldItemCount(legacyType, GetHeldItemCount(legacyType) + legacyCount);

        GameSave.DeleteKey(legacyTypeKey);
        GameSave.DeleteKey(legacyCountKey);
    }

    private void RefreshGoldText()
    {
        if (goldText != null) goldText.text = GameSave.GetInt(GoldKey, 0).ToString();
    }

    public void AddGold(int amount)
    {
        int total = GameSave.GetInt(GoldKey, 0) + amount;
        GameSave.SetInt(GoldKey, total);
        GameSave.Save();
        RefreshGoldText();
    }

    private int EffectiveMaxPlayerHits => maxPlayerHits + PlayerLevelManager.MaxHitsBonus;

    // Health bar tints smoothly green -> yellow -> orange -> red as it drains, so the
    // color itself telegraphs danger instead of relying only on the bar's length.
    private static readonly Color HealthColorFull = new Color(0.35f, 0.90f, 0.40f, 1f);
    private static readonly Color HealthColorHigh = new Color(0.95f, 0.85f, 0.25f, 1f);
    private static readonly Color HealthColorMid = new Color(0.95f, 0.55f, 0.15f, 1f);
    private static readonly Color HealthColorLow = new Color(0.85f, 0.25f, 0.25f, 1f);

    private static Color GetHealthColor(float ratio)
    {
        if (ratio > 0.66f) return Color.Lerp(HealthColorHigh, HealthColorFull, (ratio - 0.66f) / 0.34f);
        if (ratio > 0.33f) return Color.Lerp(HealthColorMid, HealthColorHigh, (ratio - 0.33f) / 0.33f);
        return Color.Lerp(HealthColorLow, HealthColorMid, ratio / 0.33f);
    }

    // healthBarFillSprites is [red, orange, yellow, green]; picks the quartile band for ratio.
    private Sprite GetHealthFillSprite(float ratio)
    {
        if (healthBarFillSprites == null || healthBarFillSprites.Length < 4) return null;
        if (ratio > 0.75f) return healthBarFillSprites[3];
        if (ratio > 0.5f) return healthBarFillSprites[2];
        if (ratio > 0.25f) return healthBarFillSprites[1];
        return healthBarFillSprites[0];
    }

    private void RefreshHealthBar()
    {
        if (playerHealthFill == null) return;
        float ratio = Mathf.Clamp01(1f - (float)playerHitCount / EffectiveMaxPlayerHits);
        playerHealthFill.fillAmount = ratio;

        var sprite = GetHealthFillSprite(ratio);
        if (sprite != null)
        {
            playerHealthFill.sprite = sprite;
            playerHealthFill.color = Color.white;
        }
        else
        {
            playerHealthFill.color = GetHealthColor(ratio);
        }
    }

    private HealItemConfig FindHealItem(string itemName)
    {
        return healItems == null ? null : System.Array.Find(healItems, item => item.itemName == itemName);
    }

    private int GetHeldItemCount(string itemName) => GameSave.GetInt(HeldItemCountKeyPrefix + itemName, 0);

    private void SetHeldItemCount(string itemName, int count)
    {
        GameSave.SetInt(HeldItemCountKeyPrefix + itemName, count);
        GameSave.Save();
    }

    private void SetItemBoxSlotsActive(bool active)
    {
        if (itemBoxButtons == null) return;
        foreach (var button in itemBoxButtons)
        {
            if (button != null) button.gameObject.SetActive(active);
        }
    }

    private void RefreshItemBox()
    {
        if (healItems == null || itemBoxIcons == null) return;

        for (int i = 0; i < healItems.Length && i < itemBoxIcons.Length; i++)
        {
            int count = GetHeldItemCount(healItems[i].itemName);
            bool hasItem = count > 0;

            if (itemBoxIcons[i] != null)
            {
                itemBoxIcons[i].enabled = hasItem;
                if (hasItem) itemBoxIcons[i].sprite = healItems[i].icon;
            }
            if (itemBoxCountTexts != null && i < itemBoxCountTexts.Length && itemBoxCountTexts[i] != null)
            {
                itemBoxCountTexts[i].text = hasItem ? "x" + count : "";
            }
        }
    }

    private void AddHealItem(string itemName)
    {
        SetHeldItemCount(itemName, GetHeldItemCount(itemName) + 1);
        RefreshItemBox();
    }

    // Each heal item type stacks in its own slot, so buying one kind never blocks buying another.
    public bool BuyHealItem(string itemName)
    {
        var config = FindHealItem(itemName);
        if (config == null) return false;

        if (!SpendGold(config.price)) return false;

        AddHealItem(itemName);
        return true;
    }

    public bool SpendGold(int amount)
    {
        int gold = GameSave.GetInt(GoldKey, 0);
        if (gold < amount) return false;

        GameSave.SetInt(GoldKey, gold - amount);
        GameSave.Save();
        RefreshGoldText();
        return true;
    }

    public void UseHealItemAt(int index)
    {
        if (healItems == null || index < 0 || index >= healItems.Length) return;
        UseHealItem(healItems[index].itemName);
    }

    private void UseHealItem(string itemName)
    {
        int count = GetHeldItemCount(itemName);
        var config = FindHealItem(itemName);
        if (config == null || count <= 0) return;

        SetHeldItemCount(itemName, count - 1);

        playerHitCount = Mathf.Max(0, playerHitCount - config.healAmount);
        RefreshHealthBar();
        RefreshItemBox();

        MissionManager.AddHealItemUsedForStage(CurrentStageIndex);
    }

    private void RefreshAll()
    {
        for (int i = 0; i < PlanetNames.Length; i++)
        {
            SetBlockState(i, i < unlockedCount);
        }
    }

    private void SetBlockState(int index, bool unlocked)
    {
        var block = gridContent.GetChild(index);
        var container = block.Find("Container");
        var img = container.GetComponent<Image>();
        var btn = container.GetComponent<Button>();
        var icon = container.Find("Icon");
        var text = block.Find("Text (TMP)").GetComponent<TMP_Text>();

        img.sprite = unlocked ? normalContainerSprite : lockedContainerSprite;
        btn.interactable = unlocked;
        if (icon != null) icon.gameObject.SetActive(unlocked);
        text.text = unlocked ? PlanetNames[index] : "LOCKED";
    }

    public void OnStagePressed(int index)
    {
        Debug.Log("Stage pressed: " + PlanetNames[index]);
        EnterStage(index);
    }

    public static int CurrentStageIndex { get; private set; }

    public static int StageCount => PlanetNames.Length;

    public int UnlockedStageCount => unlockedCount;

    public static string GetPlanetName(int index)
    {
        return PlanetNames[Mathf.Clamp(index, 0, PlanetNames.Length - 1)];
    }

    public void EnterStage(int index)
    {
        // Restarting mid-stage (Pause > Restart) would otherwise leave the previous
        // run's spawn loop/boss timers and monsters alive, doubling up with the fresh
        // ones PlayIntro is about to start - stop and clear everything first so a
        // restart spawns monsters exactly like a first-time entry does.
        StopAllStageRoutines();
        ClearActiveMonsters();
        // A boss membrane still melting when the last stage was left (it isn't swept with the clones).
        foreach (var shield in gameplayStage.GetComponentsInChildren<BossShield>(true)) Destroy(shield.gameObject);

        CurrentStageIndex = index;
        playerHitCount = 0;
        tookDamageThisStage = false;
        killsThisStage = 0;
        patternsStarted = false;
        isStageEnding = false;
        RefreshHealthBar();

        foreach (var go in levelSelectUIToHide) if (go != null) go.SetActive(false);
        gameplayStage.SetActive(true);
        player.SetActive(false);
        DeactivateTemplateArray(planetMonsters);
        DeactivateTemplateArray(nextPlanetBossTemplates);
        if (finalBoss != null) finalBoss.gameObject.SetActive(false);
        if (playerHealthBarRoot != null) playerHealthBarRoot.SetActive(true);
        SetItemBoxSlotsActive(true);

        stageLabelText.text = string.Format(LocalizationManager.Get("stage_label", "STAGE {0}"), index + 1);
        stageNameText.text = PlanetNames[index];
        if (bgm != null) bgm.PlayStage(index);   // a restart keeps the same track playing

        introRoutine = StartCoroutine(PlayIntro());
    }

    public bool IsInStage => gameplayStage != null && gameplayStage.activeSelf;

    public void RestartCurrentStage()
    {
        EnterStage(CurrentStageIndex);
    }

    private Monster GetRegularMonsterTemplate() => ElementOrNull(planetMonsters, CurrentStageIndex);

    private Monster GetBossTemplate()
    {
        if (finalBoss != null && CurrentStageIndex == finalBossStageIndex) return finalBoss;
        return ElementOrNull(nextPlanetBossTemplates, CurrentStageIndex);
    }

    private static T ElementOrNull<T>(T[] array, int index) where T : class
    {
        return array != null && index >= 0 && index < array.Length ? array[index] : null;
    }

    private static bool ContainsStage(int[] stageIndices, int stageIndex)
    {
        return stageIndices != null && System.Array.IndexOf(stageIndices, stageIndex) >= 0;
    }

    private bool IsExtraMonsterStage() => ContainsStage(extraMonsterStageIndices, CurrentStageIndex);

    private static void DeactivateTemplateArray(Monster[] templates)
    {
        if (templates == null) return;
        foreach (var t in templates)
        {
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    // ---- Spawning ----

    // Every runtime monster is a clone of an inactive template under the gameplay stage,
    // wired into the stage's hit/kill handling.
    private Monster CloneMonster(Monster template)
    {
        var monster = Instantiate(template.gameObject, template.transform.parent).GetComponent<Monster>();
        monster.OnReachedPlayer += HandleMonsterReachedPlayer;
        monster.OnDefeated += HandleMonsterDefeated;
        return monster;
    }

    // Ramps a freshly-cloned monster's stats up with the current chapter, so later
    // planets feel meaningfully tougher than earlier ones instead of being a reskin.
    private void ApplyChapterDifficulty(Monster monster)
    {
        int stage = CurrentStageIndex;
        monster.maxHealth += monsterHealthPerStage * stage;
        monster.moveSpeed += monsterSpeedPerStage * stage;
        monster.attackPower += stage / Mathf.Max(1, stagesPerAttackPowerIncrease);
        monster.attackInterval = Mathf.Max(minMonsterAttackInterval, monster.attackInterval - monsterAttackIntervalReductionPerStage * stage);
    }

    private void SpawnMonster()
    {
        bool spawnElite = stageEliteMonster != null && Random.value < eliteSpawnChance + eliteSpawnChancePerStage * CurrentStageIndex;
        var template = spawnElite ? stageEliteMonster : GetRegularMonsterTemplate();
        if (template == null) return;

        var monster = CloneMonster(template);
        ApplyChapterDifficulty(monster);
        if (spawnElite) ApplyEliteStageColor(monster);
        monster.Spawn();
        activeMonsters.Add(monster);
    }

    // The elite monster is a single shared prefab across all stages, so it's tinted
    // per-stage here instead of needing a separate colored prefab per planet.
    private void ApplyEliteStageColor(Monster elite)
    {
        if (eliteColorPerStage == null || CurrentStageIndex >= eliteColorPerStage.Length) return;

        var img = elite.GetComponent<Image>();
        if (img != null) img.color = eliteColorPerStage[CurrentStageIndex];
    }

    // Boss spawning returns false when this stage has no such boss.
    private bool SpawnBoss(Monster template)
    {
        if (template == null) return false;

        var boss = CloneMonster(template);
        boss.maxHealth += bossHealthPerStage * CurrentStageIndex;
        boss.OnDefeated += HandleBossDefeated;
        boss.Spawn();
        if (template == finalBoss) FinalBoss.Attach(boss, this, (RectTransform)player.transform, bossBulletSprite, bossBarrierSprite);
        else if (bossShieldSprite != null) BossShield.Attach(boss, bossShieldSprite, bossShieldColor, bossSparkFrames);
        activeBosses.Add(boss);
        if (bgm != null) bgm.PlayBoss(CurrentStageIndex);

        // Regular spawns pause for the boss; pattern waves carry the fight, even if not enough kills unlocked them yet.
        if (!patternsStarted && GetRegularMonsterTemplate() != null)
        {
            patternsStarted = true;
            patternRoutine = StartCoroutine(PatternLoop());
        }
        return true;
    }

    // ---- Formations (circle wave + pattern waves) ----

    // Fires exactly once per stage - after this beat, monsters go back to spawning
    // normally instead of the wave repeating on a timer. Its monsters count toward the
    // regular spawn cap (activeMonsters), unlike pattern waves.
    private IEnumerator CircleWaveLoop()
    {
        yield return new WaitForSeconds(circleWaveInterval);
        SpawnRing(circleWaveMonsterCount, circleWaveSpeedMultiplier, 0f, activeMonsters);
        circleWaveRoutine = null;
    }

    // Once a stage has enough kills, one of the chapter's formations rolls in every few
    // seconds on top of the normal spawns, which keep going exactly as before.
    private IEnumerator PatternLoop()
    {
        yield return new WaitForSeconds(patternFirstDelay);

        int lastPattern = -1;
        while (true)
        {
            PruneInactive(patternMonsters);

            if (patternMonsters.Count < patternMaxAlive)
            {
                var patterns = GetPatternsForCurrentStage();
                int pick = Random.Range(0, patterns.Length);
                if (patterns.Length > 1 && pick == lastPattern) pick = (pick + 1) % patterns.Length;
                lastPattern = pick;

                SpawnPattern(patterns[pick]);
            }

            yield return new WaitForSeconds(patternInterval);
        }
    }

    private static readonly PatternType[] AllPatterns = (PatternType[])System.Enum.GetValues(typeof(PatternType));

    private PatternType[] GetPatternsForCurrentStage()
    {
        var set = ElementOrNull(chapterPatterns, CurrentStageIndex);
        return set != null && set.patterns != null && set.patterns.Length > 0 ? set.patterns : AllPatterns;
    }

    private void SpawnPattern(PatternType pattern)
    {
        var template = GetRegularMonsterTemplate();
        if (template == null) return;

        int stage = CurrentStageIndex;
        switch (pattern)
        {
            case PatternType.Wall: SpawnWallPattern(template); break;
            case PatternType.Ring: SpawnRing(patternRingBaseCount + stage * patternRingCountPerStage, patternRingSpeedMultiplier, 0f, patternMonsters); break;
            case PatternType.Stream: SpawnStreamPattern(template); break;
            case PatternType.Snake: SpawnSnakePattern(template); break;
            case PatternType.Spiral:
                float spin = Random.value < 0.5f ? 1f : -1f;
                SpawnRing(patternRingBaseCount + stage, patternSpiralSpeedMultiplier, patternSpiralDegreesPerSecond * spin, patternMonsters);
                break;
        }
    }

    // Formation monsters come in big batches, so each one is fodder to be mowed down
    // rather than a full-strength regular.
    private Monster CreateFormationMonster(Monster template, List<Monster> trackIn)
    {
        var monster = CloneMonster(template);
        ApplyChapterDifficulty(monster);
        monster.maxHealth = Mathf.Max(1, Mathf.RoundToInt(monster.maxHealth * patternHealthMultiplier));
        trackIn.Add(monster);
        return monster;
    }

    // A circle of monsters well off-screen that closes in - first toward the spot the
    // player stood when it spawned (keeping its circle), then onto the player once it's
    // halfway in. A non-zero orbit makes it swirl while it tightens (spiral).
    private void SpawnRing(int count, float speedMultiplier, float orbitDegreesPerSecond, List<Monster> trackIn)
    {
        var template = GetRegularMonsterTemplate();
        if (template == null || count <= 0) return;

        Vector2 center = PlayerAnchoredPosition();
        for (int i = 0; i < count; i++)
        {
            var monster = CreateFormationMonster(template, trackIn);
            monster.moveSpeed *= speedMultiplier;
            monster.orbitDegreesPerSecond = orbitDegreesPerSecond;
            monster.SpawnAtAngle(i * (Mathf.PI * 2f / count), circleWaveRadius);
            monster.ApproachPointFirst(center);
        }
    }

    // A non-homing formation member that moves in a straight line and vanishes after travelLimit.
    private Monster LaunchFixedMonster(Monster template, Vector2 position, Vector2 direction, float travelLimit)
    {
        var monster = CreateFormationMonster(template, patternMonsters);
        monster.moveSpeed = Mathf.Min(monster.moveSpeed * patternSpeedMultiplier, patternMaxSpeed);
        monster.movesInFixedDirection = true;
        monster.fixedDirection = direction.normalized;
        monster.fixedTravelLimit = travelLimit;
        monster.SpawnAtPosition(position);
        return monster;
    }

    // A straight line of monsters sweeping across the whole screen from one side, with one
    // escape gap kept near the player so it can always be dodged.
    private void SpawnWallPattern(Monster template)
    {
        GetStageBounds(template, out Vector2 boundsMin, out Vector2 boundsMax);
        Vector2 viewCenter = (boundsMin + boundsMax) * 0.5f;
        Vector2 halfSize = (boundsMax - boundsMin) * 0.5f;

        Vector2 travel = WallDirections[Random.Range(0, WallDirections.Length)];
        bool horizontal = Mathf.Abs(travel.x) > 0.5f;

        float travelHalf = (horizontal ? halfSize.x : halfSize.y) + patternEdgeMargin;
        float crossHalf = (horizontal ? halfSize.y : halfSize.x) + patternEdgeMargin;
        Vector2 perp = new Vector2(-travel.y, travel.x);
        Vector2 lineCenter = viewCenter - travel * travelHalf;

        int slots = Mathf.Max(3, Mathf.CeilToInt(crossHalf * 2f / patternSlotSpacing));
        float slotStep = crossHalf * 2f / slots;

        float playerCross = Vector2.Dot(PlayerAnchoredPosition() - viewCenter, perp);
        float gapCenter = Mathf.Clamp(playerCross + Random.Range(-250f, 250f), -crossHalf + 200f, crossHalf - 200f);

        float travelLimit = travelHalf * 2f + patternEdgeMargin;
        for (int s = 0; s < slots; s++)
        {
            float cross = -crossHalf + (s + 0.5f) * slotStep;
            if (Mathf.Abs(cross - gapCenter) < patternWallGapHalfWidth) continue;
            LaunchFixedMonster(template, lineCenter + perp * cross, travel, travelLimit);
        }
    }

    private static readonly Vector2[] WallDirections = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

    // A single-file column charging straight at the spot the player was standing when it
    // spawned - it doesn't home in, so sidestepping works.
    private void SpawnStreamPattern(Monster template)
    {
        Vector2 origin = PlayerAnchoredPosition();
        Vector2 outward = RandomDirection();
        float startDistance = OffscreenDistance(template, origin, outward);

        float travelLimit = startDistance * 2f + patternStreamLaneLength * patternColumnSpacing + patternEdgeMargin;
        for (int row = 0; row < patternStreamLaneLength; row++)
        {
            LaunchFixedMonster(template, origin + outward * (startDistance + row * patternColumnSpacing), -outward, travelLimit);
        }
    }

    // Like the stream, but slithering side to side. Each member trails the head's wiggle,
    // so it reads as one snake.
    private void SpawnSnakePattern(Monster template)
    {
        Vector2 origin = PlayerAnchoredPosition();
        Vector2 outward = RandomDirection();
        float startDistance = OffscreenDistance(template, origin, outward) + patternSnakeAmplitude;

        float frequency = Mathf.PI * 2f / patternSnakeWavelength;
        float travelLimit = startDistance * 2f + patternSnakeLength * patternColumnSpacing + patternEdgeMargin;
        for (int row = 0; row < patternSnakeLength; row++)
        {
            float behind = row * patternColumnSpacing;
            var monster = LaunchFixedMonster(template, origin + outward * (startDistance + behind), -outward, travelLimit);
            monster.zigzagAmplitude = patternSnakeAmplitude;
            monster.zigzagFrequency = frequency;
            monster.zigzagPhase = -behind * frequency;
        }
    }

    private Vector2 PlayerAnchoredPosition()
    {
        return player != null ? ((RectTransform)player.transform).anchoredPosition : Vector2.zero;
    }

    private static Vector2 RandomDirection()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // How far from origin along outward until just past the screen edge, so a formation
    // placed there starts fully off-screen.
    private float OffscreenDistance(Monster template, Vector2 origin, Vector2 outward)
    {
        GetStageBounds(template, out Vector2 min, out Vector2 max);

        float tx = float.PositiveInfinity, ty = float.PositiveInfinity;
        if (outward.x > 0.001f) tx = (max.x - origin.x) / outward.x;
        else if (outward.x < -0.001f) tx = (min.x - origin.x) / outward.x;
        if (outward.y > 0.001f) ty = (max.y - origin.y) / outward.y;
        else if (outward.y < -0.001f) ty = (min.y - origin.y) / outward.y;
        return Mathf.Max(0f, Mathf.Min(tx, ty)) + patternEdgeMargin;
    }

    // Visible screen area in the monsters' anchoredPosition space; falls back to the
    // parent rect only if there's no camera to measure from.
    private static void GetStageBounds(Monster template, out Vector2 min, out Vector2 max)
    {
        var space = (RectTransform)template.transform.parent;
        if (UIUtil.TryGetVisibleBounds(space, out min, out max)) return;
        min = space.rect.min;
        max = space.rect.max;
    }

    // Monsters that died or swept off-screen just deactivate; this drops them from a list
    // and destroys the leftover clones.
    private static void PruneInactive(List<Monster> monsters)
    {
        monsters.RemoveAll(m =>
        {
            if (m == null) return true;
            if (m.gameObject.activeSelf) return false;
            Destroy(m.gameObject);
            return true;
        });
    }

    private static void DestroyAll(List<Monster> monsters)
    {
        foreach (var monster in monsters)
        {
            if (monster != null) Destroy(monster.gameObject);
        }
        monsters.Clear();
    }

    private void HandleMonsterDefeated(Monster monster)
    {
        Vector2 anchoredPosition = monster.RectTransform.anchoredPosition;

        if (!monster.isBoss)
        {
            killsThisStage++;
            if (!patternsStarted && !isStageEnding && killsThisStage >= patternStartKills && GetRegularMonsterTemplate() != null)
            {
                patternsStarted = true;
                patternRoutine = StartCoroutine(PatternLoop());
            }
        }

        MissionManager.AddMonsterDefeated();
        if (!monster.isBoss) MissionManager.AddMonsterDefeatedForStage(CurrentStageIndex);
        if (monster.isBoss || Random.value < goldDropChance)
        {
            AddGold(monster.isBoss ? bossGoldDrop + bossGoldPerStage * CurrentStageIndex : Random.Range(minGoldDrop, maxGoldDrop + 1));
            SpawnItemDrop(coinDropPrefab, anchoredPosition);
        }

        if (monster.isElite) MissionManager.AddMidEliteDefeatedForStage(CurrentStageIndex);

        if (monster.isElite && Random.value < eliteMushroomDropChance)
        {
            SpawnItemDrop(mushroomDropPrefab, anchoredPosition);
            AddHealItem("Mushroom");
        }
    }

    private void SpawnItemDrop(GameObject prefab, Vector2 anchoredPosition)
    {
        if (prefab == null) return;

        var clone = Instantiate(prefab, prefab.transform.parent);
        clone.GetComponent<ItemDrop>().Play(anchoredPosition);
    }

    private IEnumerator BossSpawnTimer()
    {
        yield return new WaitForSeconds(bossSpawnDelay);
        SpawnBoss(GetBossTemplate());
        bossSpawnRoutine = null;
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(monsterSpawnDelay);

        bool boosted = IsExtraMonsterStage();
        int capEnd = maxMonsterCount + maxMonsterCountPerStage * CurrentStageIndex + (boosted ? extraMaxMonsterCount : 0);
        float intervalStart = boosted ? Mathf.Max(0.2f, spawnInterval - extraSpawnIntervalReduction) : spawnInterval;
        float intervalEnd = boosted ? Mathf.Max(0.15f, minSpawnInterval - extraSpawnIntervalReduction) : minSpawnInterval;

        float stageStartTime = Time.time;
        while (true)
        {
            PruneInactive(activeMonsters);

            float t = Mathf.Clamp01((Time.time - stageStartTime) / spawnRampDuration);
            int currentCap = Mathf.RoundToInt(Mathf.Lerp(startMonsterCount, capEnd, t));
            float currentInterval = Mathf.Lerp(intervalStart, intervalEnd, t);

            // During a boss fight only pattern waves come in (see SpawnBoss).
            if (activeBosses.Count == 0 && activeMonsters.Count < currentCap) SpawnMonster();

            yield return new WaitForSeconds(currentInterval);
        }
    }

    private void HandleMonsterReachedPlayer(Monster monster)
    {
        if (isStageEnding) return;
        DamagePlayer(monster.attackPower);
    }

    // Final boss bullets. A short grace period after each hit keeps a dense stream from
    // emptying the health bar in one pass.
    public void HitPlayerWithBullet()
    {
        if (isStageEnding || Time.time - lastBulletHitTime < bulletHitInvulnerability) return;
        lastBulletHitTime = Time.time;
        DamagePlayer(bossBulletDamage);
    }

    // Big "PHASE 2/3" flash where the stage label normally shows.
    public void ShowBossPhase(int phase)
    {
        if (stageLabelGroup == null || stageLabelText == null) return;
        stageLabelText.text = string.Format(LocalizationManager.Get("boss_phase", "PHASE {0}"), phase);
        StartCoroutine(FlashGroup(stageLabelGroup, 0.3f, 0.8f, 0.4f));
    }

    private void DamagePlayer(int amount)
    {
        var playerController = player.GetComponent<PlayerController>();
        if (playerController != null) playerController.FlashHit();

        playerHitCount += amount;
        tookDamageThisStage = true;
        RefreshHealthBar();
        if (playerHitCount >= EffectiveMaxPlayerHits) StartStageEnd(PlayGameOverSequence());
    }

    private void HandleBossDefeated(Monster boss)
    {
        MissionManager.AddBossDefeated(CurrentStageIndex);

        if (isStageEnding) return;

        activeBosses.Remove(boss);
        if (activeBosses.Count > 0) return;

        if (bgm != null) bgm.ReturnToStage(CurrentStageIndex);

        // A dominated planet's membrane melts away first; STAGE CLEAR waits for it.
        var shield = boss.GetComponentInChildren<BossShield>(true);
        float meltWait = shield != null ? shield.Melt(boss) : 0f;
        StartStageEnd(PlayStageClearSequence(meltWait));
    }

    private void StartStageEnd(IEnumerator sequence)
    {
        isStageEnding = true;
        StopAllStageRoutines();
        stageEndRoutine = StartCoroutine(sequence);
    }

    private void StopAllStageRoutines()
    {
        Stop(ref introRoutine);
        Stop(ref spawnRoutine);
        Stop(ref circleWaveRoutine);
        Stop(ref patternRoutine);
        Stop(ref bossSpawnRoutine);
        Stop(ref stageEndRoutine);
    }

    private void Stop(ref Coroutine routine)
    {
        if (routine == null) return;
        StopCoroutine(routine);
        routine = null;
    }

    private void ClearActiveMonsters()
    {
        DestroyAll(activeMonsters);
        DestroyAll(patternMonsters);
        DestroyAll(activeBosses);
        ClearLeftoverEffects();
    }

    // Projectiles/effects (laser bolts, sword slashes, drill bolts, satellite pulses,
    // item drops) are instantiated as clones under gameplayStage and keep animating
    // via coroutines even after the stage ends if they were still mid-flight; since
    // gameplayStage just gets deactivated (not cleared), those clones used to sit there
    // paused and pop back in - still mid-animation - the next time a stage starts,
    // showing up as leftover effects from the previous run. Runtime clones are always
    // named "<template>(Clone)", so sweeping those out (and leaving the inactive
    // template objects alone) clears them for good.
    private void ClearLeftoverEffects()
    {
        if (gameplayStage == null) return;

        var stageTransform = gameplayStage.transform;
        for (int i = stageTransform.childCount - 1; i >= 0; i--)
        {
            var child = stageTransform.GetChild(i);
            if (child.name.EndsWith("(Clone)")) Destroy(child.gameObject);
        }
    }

    private IEnumerator PlayStageClearSequence(float delay = 0f)
    {
        ClearActiveMonsters();
        if (delay > 0f) yield return new WaitForSeconds(delay);
        MissionManager.AddStageCleared();
        if (!tookDamageThisStage) MissionManager.AddNoHitClear();
        bool finalChapter = CurrentStageIndex == PlanetNames.Length - 1;
        if (finalChapter) MissionManager.MarkGameCompleted();
        // The EXP bar fills (and LEVEL UP shows) on the chapter select screen, after ExitStage.
        PlayerLevelManager.AddStageClearExperience(CurrentStageIndex);

        string message = LocalizationManager.Get("stage_clear", "STAGE CLEAR");

        // The fanfare takes over from the stage music while the text comes up; the player leaves the field.
        player.SetActive(false);
        if (bgm != null) bgm.FadeOut(0.4f);
        if (stageClearSound != null) sfxSource.PlayOneShot(stageClearSound, stageClearVolume * VolumeSettings.MusicGain);
        yield return ShowMessage(stageClearGroup, stageClearText, message);

        int unlockedIndex = -1;
        if (CurrentStageIndex == unlockedCount - 1 && unlockedCount < PlanetNames.Length)
        {
            unlockedCount++;
            GameSave.SetInt(ProgressKey, unlockedCount);
            GameSave.Save();
            unlockedIndex = unlockedCount - 1;
            SetBlockState(unlockedIndex, true);
        }

        ExitStage();
        // Runs on PlayerLevelManager so it isn't tied to this routine (ExitStage stops stageEndRoutine).
        if (PlayerLevelManager.Instance != null) PlayerLevelManager.Instance.PlayGainMotion();

        if (finalChapter && endingCredits != null) endingCredits.Play();
        else if (unlockedIndex >= 0) yield return PlayUnlockMotion(unlockedIndex);

        stageEndRoutine = null;
    }

    private IEnumerator PlayGameOverSequence()
    {
        ClearActiveMonsters();

        if (bgm != null) bgm.FadeOut(0.4f);
        if (gameOverSound != null) sfxSource.PlayOneShot(gameOverSound, gameOverVolume * VolumeSettings.MusicGain);
        yield return ShowMessage(gameOverGroup, gameOverText, LocalizationManager.Get("game_over", "GAME OVER"));

        ExitStage();
        stageEndRoutine = null;
    }

    private IEnumerator ShowMessage(CanvasGroup group, TMP_Text text, string message)
    {
        if (group == null || text == null) yield break;

        text.text = message;
        yield return FlashGroup(group, 0.4f, 1.2f, 0.4f);
    }

    private IEnumerator PlayUnlockMotion(int index)
    {
        var block = gridContent.GetChild(index);
        Vector3 baseScale = block.localScale;
        block.localScale = Vector3.zero;

        float t = 0f;
        const float duration = 0.5f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float eased = p + Mathf.Sin(p * Mathf.PI) * 0.2f;
            block.localScale = baseScale * eased;
            yield return null;
        }
        block.localScale = baseScale;
    }

    private IEnumerator PlayIntro()
    {
        stageNameGroup.gameObject.SetActive(false);
        yield return FlashGroup(stageLabelGroup, 0.6f, 0.9f, 0.4f);
        yield return FlashGroup(stageNameGroup, 0.6f, 0.9f, 0.4f);

        player.SetActive(true);

        if (GetRegularMonsterTemplate() != null)
        {
            Restart(ref spawnRoutine, SpawnLoop());
            if (CurrentStageIndex >= circleWaveMinStageIndex) Restart(ref circleWaveRoutine, CircleWaveLoop());
        }
        if (GetBossTemplate() != null) Restart(ref bossSpawnRoutine, BossSpawnTimer());

        introRoutine = null;
    }

    private void Restart(ref Coroutine routine, IEnumerator body)
    {
        Stop(ref routine);
        routine = StartCoroutine(body);
    }

    // Fades a label group in, holds it, fades it out, then hides it.
    private IEnumerator FlashGroup(CanvasGroup group, float fadeIn, float hold, float fadeOut)
    {
        group.alpha = 0f;
        group.gameObject.SetActive(true);
        yield return Fade(group, 0f, 1f, fadeIn);
        yield return new WaitForSeconds(hold);
        yield return Fade(group, 1f, 0f, fadeOut);
        group.gameObject.SetActive(false);
    }

    private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    public void ExitStage()
    {
        StopAllStageRoutines();
        ClearActiveMonsters();

        gameplayStage.SetActive(false);
        DeactivateTemplateArray(planetMonsters);
        if (playerHealthBarRoot != null) playerHealthBarRoot.SetActive(false);
        SetItemBoxSlotsActive(false);
        foreach (var go in levelSelectUIToHide) if (go != null) go.SetActive(true);
        if (bgm != null) bgm.PlayLevelSelect();
    }
}
