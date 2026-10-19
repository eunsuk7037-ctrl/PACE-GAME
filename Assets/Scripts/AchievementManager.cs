using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class AchievementDefinition
{
    public string id;

    // Curated achievements resolve display text through LocalizationManager via these keys.
    public string nameKey;
    public string descriptionKey;

    // Generated/bulk achievements skip the localization dictionary and carry their own
    // [English, Korean] text directly, since there are too many of them to hand-author keys for.
    public string[] nameText;
    public string[] descriptionText;

    public string progressKey;
    public int targetValue;
    public Sprite icon;

    // Which tab this achievement shows under: "kill", "weapon", or "stage" (gold/stage progress).
    public string category;

    // Square size of the unlocked icon in its grid slot, tuned so every icon's visible
    // artwork ends up the same size as the planet icons (sprites carry different padding).
    public float iconSize = 32f;

    // Optional LocalizationManager key for a story blurb shown under this achievement's description.
    public string storyKey;
}

public class AchievementManager : MonoBehaviour
{
    public AchievementDefinition[] achievements;
    public Image[] slotIcons;
    public Sprite lockedIcon;
    [Tooltip("Testing/preview only: shows every achievement as unlocked without touching save progress. Turn off before release.")]
    public bool unlockAllForTesting;

    public GameObject inventorySelectedIcon;
    public TMP_Text inventoryObjectText;
    public TMP_Text inventoryDescriptionText;
    public TMP_Text inventoryStoryText;

    private const float LockedIconSize = 32f;
    private const string CategoryAll = "all";
    private string currentCategory = CategoryAll;

    // The grid has no layout group (slots sit at hand-placed positions), so filtering
    // has to re-pack the visible slots itself instead of just hiding gaps in place.
    private const int GridColumns = 6;
    private const float GridStartX = 62.5f;
    private const float GridStartY = -62.5f;
    private const float GridColSpacing = 135f;
    private const float GridRowSpacing = 130f;
    private RectTransform[] slotBlocks;

    private void CacheSlotBlocks()
    {
        if (slotBlocks != null) return;
        slotBlocks = new RectTransform[slotIcons.Length];
        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] == null) continue;
            slotBlocks[i] = slotIcons[i].transform.parent.parent as RectTransform;
        }
    }

    private void OnEnable()
    {
        RefreshSlots();
    }

    public void OnShowAllPressed() { SetCategory(CategoryAll); }
    public void OnShowKillPressed() { SetCategory("kill"); }
    public void OnShowWeaponPressed() { SetCategory("weapon"); }
    public void OnShowStagePressed() { SetCategory("stage"); }

    private void SetCategory(string category)
    {
        currentCategory = category;
        RefreshSlots();
    }

    // Curated achievements use a localization key; generated ones fall back to their own
    // raw [English, Korean] pair, picked by the localization manager's active language.
    private static string Resolve(string key, string[] rawText, string fallback)
    {
        if (!string.IsNullOrEmpty(key)) return LocalizationManager.Get(key, fallback);

        int langIndex = LocalizationManager.LanguageIndex;
        if (rawText != null && langIndex < rawText.Length) return rawText[langIndex];
        return fallback;
    }

    private bool IsUnlocked(AchievementDefinition a)
    {
        return unlockAllForTesting || GameSave.GetInt(a.progressKey, 0) >= a.targetValue;
    }

    public void RefreshSlots()
    {
        CacheSlotBlocks();

        int visibleIndex = 0;
        for (int i = 0; i < achievements.Length && i < slotIcons.Length; i++)
        {
            if (slotIcons[i] == null) continue;

            var a = achievements[i];
            bool unlocked = IsUnlocked(a);
            slotIcons[i].sprite = unlocked ? a.icon : lockedIcon;
            float size = unlocked ? a.iconSize : LockedIconSize;
            slotIcons[i].rectTransform.sizeDelta = new Vector2(size, size);

            bool matchesCategory = currentCategory == CategoryAll || a.category == currentCategory;
            var block = slotBlocks[i];
            if (block == null) continue;

            block.gameObject.SetActive(matchesCategory);
            if (!matchesCategory) continue;

            int col = visibleIndex % GridColumns;
            int row = visibleIndex / GridColumns;
            block.anchoredPosition = new Vector2(GridStartX + col * GridColSpacing, GridStartY - row * GridRowSpacing);
            visibleIndex++;
        }
    }

    public void OnAchievementSlotPressed(int index)
    {
        if (index < 0 || index >= achievements.Length) return;

        var a = achievements[index];
        bool unlocked = IsUnlocked(a);

        if (inventorySelectedIcon != null)
        {
            inventorySelectedIcon.SetActive(true);
            var img = inventorySelectedIcon.GetComponent<Image>();
            if (img != null) img.sprite = unlocked ? a.icon : lockedIcon;
        }
        if (inventoryObjectText != null)
        {
            inventoryObjectText.text = unlocked ? Resolve(a.nameKey, a.nameText, a.id) : LocalizationManager.Get("locked", "LOCKED");
        }
        if (inventoryDescriptionText != null)
        {
            inventoryDescriptionText.text = unlocked ? Resolve(a.descriptionKey, a.descriptionText, "") : LocalizationManager.Get("achv_locked_desc", "Unlock by completing the challenge.");
        }
        if (inventoryStoryText != null)
        {
            // The story is part of the reward: kept hidden until the achievement unlocks.
            bool showStory = unlocked && !string.IsNullOrEmpty(a.storyKey);
            inventoryStoryText.gameObject.SetActive(showStory);
            if (showStory) inventoryStoryText.text = LocalizationManager.Get(a.storyKey, "");
        }
    }
}
