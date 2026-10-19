using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class WeaponConfig
{
    public string itemName;
    public int price;
    public GameObject itemContainer;
}

public class LevelSelectActions : MonoBehaviour
{
    public StageProgress stageProgress;

    [Header("Weapons")]
    public WeaponConfig[] weapons;
    public Color weaponEquippedTint = new Color(0.75f, 1f, 0.75f, 1f);
    public AudioClip weaponEquipSound;
    [Range(0f, 1f)] public float weaponEquipVolume = 0.7f;
    public AudioClip weaponUnequipSound;   // the equip sound played backwards and lower
    [Range(0f, 1f)] public float weaponUnequipVolume = 0.7f;
    public AudioClip weaponUpgradeSound;
    [Range(0f, 1f)] public float weaponUpgradeVolume = 0.7f;
    private AudioSource sfxSource;
    private const float EquippedPressOffset = 6f;
    private static readonly Color EquippedPressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    public const string RayGunItemName = "Ray Gun";
    public const string SwordItemName = "Sword";
    public const string SatelliteItemName = "Satellite";
    public const string MachineDrillItemName = "Machine Drill";
    private const string WeaponOwnedKeyPrefix = "WeaponOwned_";
    private const string EquippedWeaponKey = "EquippedWeapon";
    private const string WeaponLevelKeyPrefix = "WeaponLevel_";

    public const int MaxWeaponLevel = 5;
    public const int WeaponUpgradeBaseCost = 5000;
    public const int DamagePerWeaponLevel = 2;

    public static string GetEquippedWeapon() => GameSave.GetString(EquippedWeaponKey, "");
    public static bool IsWeaponEquipped(string itemName) => GetEquippedWeapon() == itemName;
    public static bool IsAnyWeaponEquipped() => !string.IsNullOrEmpty(GetEquippedWeapon());
    public static int GetWeaponLevel(string itemName) => GameSave.GetInt(WeaponLevelKeyPrefix + itemName, 0);

    // Each upgrade level adds DamagePerWeaponLevel attack power to that weapon's fired damage.
    public static int GetWeaponBonusDamage(string itemName) => GetWeaponLevel(itemName) * DamagePerWeaponLevel;

    // 1st upgrade costs WeaponUpgradeBaseCost, each subsequent one costs one more base-cost step.
    public static int GetWeaponUpgradeCost(string itemName) => WeaponUpgradeBaseCost * (GetWeaponLevel(itemName) + 1);

    public GameObject shopDim;
    public GameObject inventoryPanel;
    public GameObject settingsPanel;
    public GameObject itemPanel;
    public GameObject missionPanel;
    public GameObject accountPanel;
    public GameObject[] mainUIToHideBehindModal;

    // Shop buttons pass an index into this list (weapons first, then heal items).
    private static readonly string[] ItemNames =
    {
        RayGunItemName, SwordItemName, SatelliteItemName, MachineDrillItemName,
        "Mushroom", "Fern", "Energy Vial", "Large Health"
    };
    private static readonly HashSet<string> HealItemNames = new HashSet<string> { "Mushroom", "Fern", "Energy Vial", "Large Health" };

    public GameObject inventorySelectedIcon;
    public TMP_Text inventoryObjectText;
    public TMP_Text inventoryDescriptionText;
    public TMP_Text inventoryStoryText;

    private GameObject[] ModalPanels => new[] { inventoryPanel, settingsPanel, itemPanel, missionPanel, accountPanel };

    private void OnEnable() => LocalizationManager.LanguageChanged += OnLanguageChanged;
    private void OnDisable() => LocalizationManager.LanguageChanged -= OnLanguageChanged;

    private void Start()
    {
        // Force a clean "no modal open" state every time this screen loads, regardless
        // of whatever panel-active flags happen to be serialized in the scene (an editor
        // save taken while a modal was open would otherwise make it appear on top of
        // Level_Select_UI as soon as the game boots).
        HideShop();
        UpdateWeaponsMaxed();

        // Runs after every Awake() in the scene, so LocalizationManager.Instance is
        // guaranteed to be set by now (RefreshWeaponUI reads it for the equip/unequip labels).
        RefreshWeaponUI();
    }

    private void OnLanguageChanged(int index) => RefreshWeaponUI();

    public void ShowInventory()
    {
        ShowPanel(inventoryPanel);
        ClearItemSelection();
    }

    public void ShowSettings() => ShowPanel(settingsPanel);
    public void ShowItemShop() => ShowPanel(itemPanel);
    public void ShowMissions() => ShowPanel(missionPanel);
    public void ShowAccount() => ShowPanel(accountPanel);

    // Opens exactly one modal panel (closing the rest) over the dimmed screen.
    private void ShowPanel(GameObject panel)
    {
        SetMainUIVisible(false);
        shopDim.SetActive(true);
        foreach (var p in ModalPanels)
        {
            if (p != null) p.SetActive(p == panel);
        }
    }

    public void HideShop()
    {
        // Settings can be opened from the in-stage pause popup, which stops time while
        // it's up; closing settings via this generic path must always resume, or the
        // game would stay frozen with no popup left to press Continue on.
        Time.timeScale = 1f;

        shopDim.SetActive(false);
        foreach (var p in ModalPanels)
        {
            if (p != null) p.SetActive(false);
        }
        // Settings opened from the in-stage pause popup: the level select UI stays hidden behind the stage.
        SetMainUIVisible(stageProgress == null || !stageProgress.IsInStage);
    }

    private void SetMainUIVisible(bool visible)
    {
        foreach (var go in mainUIToHideBehindModal) if (go != null) go.SetActive(visible);
    }

    private void ClearItemSelection()
    {
        inventorySelectedIcon.SetActive(false);
        inventoryObjectText.text = LocalizationManager.Get("empty", "Empty");
        inventoryDescriptionText.text = LocalizationManager.Get("empty_desc", "You haven't found anything here yet.");
        if (inventoryStoryText != null) inventoryStoryText.gameObject.SetActive(false);   // don't keep the last pick's story
    }

    public void OnBuyItemPressed(int index)
    {
        string itemName = ItemNames[index];
        if (HealItemNames.Contains(itemName))
        {
            bool bought = stageProgress != null && stageProgress.BuyHealItem(itemName);
            Debug.Log(bought ? "Bought " + itemName : "Could not buy " + itemName + " (not enough gold)");
            return;
        }

        var weapon = FindWeapon(itemName);
        if (weapon == null)
        {
            Debug.Log("Buy item pressed: " + itemName);
            return;
        }

        // Owned weapons: the same button toggles equip/unequip.
        if (IsWeaponOwned(itemName))
        {
            if (IsWeaponEquipped(itemName)) UnequipWeapon();
            else EquipWeapon(itemName);
            return;
        }

        if (stageProgress == null || !stageProgress.SpendGold(weapon.price))
        {
            Debug.Log("Not enough gold to buy " + itemName);
            return;
        }

        GameSave.SetInt(WeaponOwnedKeyPrefix + itemName, 1);
        EquipWeapon(itemName);
    }

    public void OnUpgradeWeaponPressed(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length) return;
        var weapon = weapons[index];
        if (!IsWeaponOwned(weapon.itemName)) return;

        int level = GetWeaponLevel(weapon.itemName);
        if (level >= MaxWeaponLevel) return;

        if (stageProgress == null || !stageProgress.SpendGold(GetWeaponUpgradeCost(weapon.itemName))) return;

        GameSave.SetInt(WeaponLevelKeyPrefix + weapon.itemName, level + 1);
        GameSave.Save();
        UpdateWeaponsMaxed();
        RefreshWeaponUI();
        PlaySfx(weaponUpgradeSound, weaponUpgradeVolume);
    }

    // Feeds the "max upgrade" achievements.
    private void UpdateWeaponsMaxed()
    {
        if (weapons == null) return;
        MissionManager.SetWeaponsMaxed(System.Array.FindAll(weapons, w => GetWeaponLevel(w.itemName) >= MaxWeaponLevel).Length);
    }

    private WeaponConfig FindWeapon(string itemName)
    {
        return weapons == null ? null : System.Array.Find(weapons, w => w.itemName == itemName);
    }

    private static bool IsWeaponOwned(string itemName) => GameSave.GetInt(WeaponOwnedKeyPrefix + itemName, 0) == 1;

    private void EquipWeapon(string itemName)
    {
        GameSave.SetString(EquippedWeaponKey, itemName);
        GameSave.Save();
        MissionManager.MarkWeaponEverEquipped();
        RefreshWeaponUI();
        PlaySfx(weaponEquipSound, weaponEquipVolume);
    }

    private void UnequipWeapon()
    {
        GameSave.SetString(EquippedWeaponKey, "");
        GameSave.Save();
        RefreshWeaponUI();
        PlaySfx(weaponUnequipSound, weaponUnequipVolume);
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null) return;
        ButtonClickSound.SkipThisClick();
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        sfxSource.PlayOneShot(clip, volume * VolumeSettings.SfxGain);
    }

    private void RefreshWeaponUI()
    {
        if (weapons == null) return;
        string equipped = GetEquippedWeapon();
        bool korean = LocalizationManager.IsKorean;

        foreach (var w in weapons)
        {
            if (w.itemContainer == null) continue;

            var buyButtonT = w.itemContainer.transform.Find("Purple_Large_Button");
            if (buyButtonT == null) continue;

            var buttonRt = (RectTransform)buyButtonT;
            var button = buyButtonT.GetComponent<Button>();
            if (button != null) button.interactable = true;
            var buttonImage = buyButtonT.GetComponent<Image>();
            var priceText = buyButtonT.Find("Text (TMP)")?.GetComponent<TMP_Text>();
            var coinIconT = buyButtonT.Find("Coin_Icon") as RectTransform;
            var background = w.itemContainer.transform.Find("Item_Background")?.GetComponent<Image>();

            bool owned = IsWeaponOwned(w.itemName);
            bool isEquipped = owned && equipped == w.itemName;

            // Owned weapons show a half-width Equip/Unequip button so an Upgrade button can
            // sit beside it; unowned weapons need the full-width buy button so the price
            // doesn't collide with the coin icon. The equipped one looks pressed in.
            buttonRt.sizeDelta = owned ? new Vector2(150f, 96f) : new Vector2(322f, 96f);
            buttonRt.anchoredPosition = new Vector2(owned ? -82f : 0f, -180f - (isEquipped ? EquippedPressOffset : 0f));
            if (buttonImage != null) buttonImage.color = isEquipped ? EquippedPressedColor : Color.white;
            if (background != null) background.color = isEquipped ? weaponEquippedTint : Color.white;

            if (coinIconT != null)
            {
                coinIconT.gameObject.SetActive(!owned);
                coinIconT.anchoredPosition = new Vector2(-104f, coinIconT.anchoredPosition.y);
            }

            if (priceText != null)
            {
                priceText.text = owned
                    ? LocalizationManager.Get(isEquipped ? "unequip" : "equip", isEquipped ? "UNEQUIP" : "EQUIP")
                    : w.price.ToString("N0");
                priceText.rectTransform.sizeDelta = new Vector2(175f, priceText.rectTransform.sizeDelta.y);
                priceText.fontStyle = owned && korean ? FontStyles.Bold : FontStyles.Normal;
            }

            CenterButtonText(buttonRt, priceText, owned ? null : coinIconT);
            RefreshUpgradeButton(w, owned, korean);
        }
    }

    private static void RefreshUpgradeButton(WeaponConfig w, bool owned, bool korean)
    {
        var upgradeButtonT = w.itemContainer.transform.Find("Upgrade_Button");
        if (upgradeButtonT == null) return;

        upgradeButtonT.gameObject.SetActive(owned);
        if (!owned) return;

        bool maxed = GetWeaponLevel(w.itemName) >= MaxWeaponLevel;
        var upgradeButton = upgradeButtonT.GetComponent<Button>();
        if (upgradeButton != null) upgradeButton.interactable = !maxed;

        var upgradeText = upgradeButtonT.Find("Text (TMP)")?.GetComponent<TMP_Text>();
        if (upgradeText == null) return;

        string label = LocalizationManager.Get("upgrade", "UPGRADE");
        if (korean) label = "<b>" + label + "</b>";
        upgradeText.text = maxed ? "MAX" : label + "\n" + GetWeaponUpgradeCost(w.itemName).ToString("N0");
    }

    // Centers priceText within the button, or within the space to the right of
    // sharesSpaceWith (e.g. a coin icon) when that's provided, so the text never
    // overlaps a leading icon regardless of how many digits the price has.
    private static void CenterButtonText(RectTransform buttonRt, TMP_Text priceText, RectTransform sharesSpaceWith)
    {
        if (priceText == null) return;

        float halfWidth = buttonRt.sizeDelta.x / 2f;
        float halfHeight = buttonRt.sizeDelta.y / 2f;

        float centerX = 0f;
        if (sharesSpaceWith != null)
        {
            float iconRightEdge = sharesSpaceWith.anchoredPosition.x + sharesSpaceWith.sizeDelta.x / 2f;
            centerX = (iconRightEdge + halfWidth) / 2f;
        }

        priceText.rectTransform.anchoredPosition = new Vector2(centerX - halfWidth, halfHeight);
    }

}
