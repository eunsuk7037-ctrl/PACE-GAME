using UnityEngine;
using TMPro;
using UnityEngine.UI;

[System.Serializable]
public class MissionRowUI
{
    public TMP_Text descriptionText;
    public TMP_Text progressText;
    public Button claimButton;
    public TMP_Text claimButtonText;
}

[System.Serializable]
public class MissionDefinition
{
    public string missionId;
    public string descriptionKey;
    public string progressKey;
    public int targetValue;
    public int goldReward;
}

[System.Serializable]
public class StageMissionSet
{
    public int stageIndex;
    public MissionDefinition[] missions;
}

public class MissionManager : MonoBehaviour
{
    public StageProgress stageProgress;
    public MissionRowUI[] rows;
    public MissionDefinition[] defaultMissions;
    public StageMissionSet[] stageMissionSets;

    [Header("Chapter Navigation")]
    public Button prevChapterButton;
    public Button nextChapterButton;
    public TMP_Text chapterText;

    private const string MonstersDefeatedKey = "TotalMonstersDefeated";
    private const string StagesClearedKey = "TotalStagesCleared";
    private const string ClaimedKeyPrefix = "MissionClaimed_";

    private MissionDefinition[] currentMissions;
    private int viewedStageIndex;

    // ---- Progress counters (read by missions and achievements via their progressKey) ----

    public static void AddMonsterDefeated() => GameSave.Increment(MonstersDefeatedKey);
    public static void AddStageCleared() => GameSave.Increment(StagesClearedKey);
    public static void AddBossDefeated(int stageIndex) => GameSave.Increment("BossDefeated_Stage" + stageIndex);
    public static void AddMonsterDefeatedForStage(int stageIndex) => GameSave.Increment("MonstersDefeated_Stage" + stageIndex);
    public static void AddAttackForStage(int stageIndex) => GameSave.Increment("AttackCount_Stage" + stageIndex);
    public static void AddWeaponUsed(string weaponName) => GameSave.Increment("WeaponUsed_" + weaponName);

    public static void AddHealItemUsedForStage(int stageIndex)
    {
        GameSave.Increment("HealItemUsed_Stage" + stageIndex);
        GameSave.Increment("TotalHealItemsUsed");
    }

    // The mid-boss of every chapter is the UFO (elite monster).
    public static void AddMidEliteDefeatedForStage(int stageIndex)
    {
        GameSave.Increment("MidEliteDefeated_Stage" + stageIndex);
        GameSave.Increment("TotalMidEliteDefeated");
    }

    public static void AddNoHitClear() => GameSave.Increment("NoHitClears");

    // Flags (0/1) - set, never counted up.
    public static void MarkWeaponEverEquipped() => SetFlag("EverEquippedWeapon");
    public static void MarkGameCompleted() => SetFlag("GameCompleted");

    public static void SetWeaponsMaxed(int count)
    {
        GameSave.SetInt("WeaponsMaxed", count);
        GameSave.Save();
    }

    private static void SetFlag(string key)
    {
        GameSave.SetInt(key, 1);
        GameSave.Save();
    }

    private int MaxUnlockedStageIndex
    {
        get
        {
            int unlockedCount = stageProgress != null ? stageProgress.UnlockedStageCount : StageProgress.StageCount;
            return Mathf.Clamp(unlockedCount, 1, StageProgress.StageCount) - 1;
        }
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        viewedStageIndex = Mathf.Min(StageProgress.CurrentStageIndex, MaxUnlockedStageIndex);
        RefreshMissionUI();
    }

    private void OnDisable() => LocalizationManager.LanguageChanged -= OnLanguageChanged;
    private void OnLanguageChanged(int index) => RefreshMissionUI();

    // Mixing Latin/digits (main font) with Korean (fallback font, e.g. "UFO 15마리 처치") makes
    // TMP spawn a child "TMP SubMeshUI" to render the fallback glyphs. That child sometimes gets
    // created with a stray non-zero anchoredPosition instead of stretching exactly over the parent,
    // which visually shifts just the Korean characters off the line. Re-stretching it after every
    // text change keeps the fallback glyphs aligned with the rest of the line.
    private static void FixFallbackFontOffset(TMP_Text text)
    {
        text.ForceMeshUpdate();
        foreach (Transform child in text.transform)
        {
            if (!child.name.StartsWith("TMP SubMeshUI")) continue;
            var rt = child as RectTransform;
            if (rt == null) continue;
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    private MissionDefinition[] GetMissionsForStage(int stageIndex)
    {
        var set = stageMissionSets == null ? null : System.Array.Find(stageMissionSets, s => s.stageIndex == stageIndex);
        return set != null ? set.missions : defaultMissions;
    }

    private static bool IsClaimed(string missionId)
    {
        return GameSave.GetInt(ClaimedKeyPrefix + missionId, 0) == 1;
    }

    private static void SetClaimed(string missionId)
    {
        GameSave.SetInt(ClaimedKeyPrefix + missionId, 1);
        GameSave.Save();
    }

    public void OnPrevChapterPressed()
    {
        viewedStageIndex = Mathf.Max(0, viewedStageIndex - 1);
        RefreshMissionUI();
    }

    public void OnNextChapterPressed()
    {
        viewedStageIndex = Mathf.Min(MaxUnlockedStageIndex, viewedStageIndex + 1);
        RefreshMissionUI();
    }

    public void RefreshMissionUI()
    {
        if (rows == null) return;

        if (chapterText != null) chapterText.text = (viewedStageIndex + 1) + ". " + StageProgress.GetPlanetName(viewedStageIndex);
        if (prevChapterButton != null) prevChapterButton.interactable = viewedStageIndex > 0;
        if (nextChapterButton != null) nextChapterButton.interactable = viewedStageIndex < MaxUnlockedStageIndex;

        currentMissions = GetMissionsForStage(viewedStageIndex) ?? new MissionDefinition[0];

        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            bool hasMission = i < currentMissions.Length;
            var mission = hasMission ? currentMissions[i] : null;

            if (row.descriptionText != null) row.descriptionText.gameObject.SetActive(hasMission);
            if (row.progressText != null) row.progressText.gameObject.SetActive(hasMission);
            if (row.claimButton != null) row.claimButton.gameObject.SetActive(hasMission);
            if (!hasMission) continue;

            int progress = Mathf.Min(GameSave.GetInt(mission.progressKey, 0), mission.targetValue);
            bool complete = progress >= mission.targetValue;
            bool claimed = IsClaimed(mission.missionId);

            if (row.descriptionText != null)
            {
                row.descriptionText.text = LocalizationManager.Get(mission.descriptionKey, mission.descriptionKey);
                FixFallbackFontOffset(row.descriptionText);
            }
            if (row.progressText != null) row.progressText.text = progress + " / " + mission.targetValue;

            if (row.claimButton != null) row.claimButton.interactable = complete && !claimed;
            if (row.claimButtonText != null) row.claimButtonText.text = claimed ? LocalizationManager.Get("done", "DONE") : LocalizationManager.Get("claim", "CLAIM");
        }
    }

    public void OnClaimPressed(int index)
    {
        if (currentMissions == null || index < 0 || index >= currentMissions.Length) return;

        var mission = currentMissions[index];
        if (IsClaimed(mission.missionId)) return;
        if (GameSave.GetInt(mission.progressKey, 0) < mission.targetValue) return;

        SetClaimed(mission.missionId);
        if (stageProgress != null) stageProgress.AddGold(mission.goldReward);

        RefreshMissionUI();
    }
}
