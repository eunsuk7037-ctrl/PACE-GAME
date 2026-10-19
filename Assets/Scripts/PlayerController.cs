using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    public float speed = 300f;
    public int hitFlashCount = 4;
    public float hitFlashInterval = 0.08f;

    [Header("Unarmed (no weapon equipped)")]
    public float unarmedStrikeInterval = 0.5f;
    public int unarmedStrikeDamage = 1;

    [Header("Satellite Pulse")]
    public GameObject satellitePulsePrefab;
    public float satellitePulseInterval = 3.5f;
    [Tooltip("Seconds taken off the pulse interval per upgrade level (level 5: 3.5 -> 2.0).")]
    public float satellitePulseIntervalPerLevel = 0.3f;
    public int satelliteDamage = 7;
    public AudioClip satellitePulseSound;
    [Range(0f, 1f)] public float satellitePulseVolume = 0.48f;

    [Header("Ray Gun")]
    public GameObject laserBoltPrefab;
    public float laserFireInterval = 0.5f;
    public int laserDamage = 6;
    public float laserFollowUpDelay = 0.12f;
    public AudioClip laserShotSound;
    [Range(0f, 1f)] public float laserShotVolume = 0.35f;

    [Header("Sword")]
    public GameObject swordSlashPrefab;
    public float swordFireInterval = 0.35f;
    public int swordDamage = 9;
    public AudioClip[] swordThrowSounds;   // played in turn, one per throw
    [Range(0f, 1f)] public float swordThrowVolume = 0.58f;

    [Header("Machine Drill")]
    public GameObject drillBoltPrefab;
    public float drillFireInterval = 0.8f;
    public int drillDamage = 5;
    public AudioClip drillFireSound;   // once per 3-drill volley
    [Range(0f, 1f)] public float drillFireVolume = 0.52f;
    [Tooltip("Forward drills per volley at upgrade level 0; each level adds one, up to drillMaxForwardCount.")]
    public int drillBaseCount = 3;
    public int drillMaxForwardCount = 7;
    [Tooltip("Drills also fired straight behind the player at the final upgrade level.")]
    public int drillRearCountAtMax = 2;
    private const float DrillSpreadStep = 20f;      // degrees between neighbouring drills...
    private const float DrillMaxHalfSpread = 45f;   // ...squeezed so the forward fan stays within +-45
    private const float DrillRearSpread = 20f;

    private static readonly Vector3[] CornerBuffer = new Vector3[4];

    private RectTransform rt;
    private Image image;
    private AudioSource sfxSource;
    private Coroutine flashRoutine;
    private Vector2 facingDirection = Vector2.up;
    private float weaponTimer;
    private int nextSwordSound;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume * VolumeSettings.SfxGain);
    }

    private void OnEnable()
    {
        weaponTimer = 0f;
    }

    private void Update()
    {
        Move();
        UpdateWeapon();
    }

    private void Move()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 dir = Vector2.zero;
        if (kb.wKey.isPressed) dir.y += 1f;
        if (kb.sKey.isPressed) dir.y -= 1f;
        if (kb.aKey.isPressed) dir.x -= 1f;
        if (kb.dKey.isPressed) dir.x += 1f;
        if (dir.sqrMagnitude <= 0f) return;

        dir.Normalize();
        rt.anchoredPosition += dir * speed * Time.deltaTime;
        facingDirection = dir;
        ClampToScreen();
    }

    // Keeps the whole ship on screen: the root canvas covers exactly the visible area, so its
    // corners (in the player's parent space) are the screen edges.
    private void ClampToScreen()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(CornerBuffer);
        var parent = rt.parent;
        Vector2 min = parent.InverseTransformPoint(CornerBuffer[0]);
        Vector2 max = parent.InverseTransformPoint(CornerBuffer[2]);
        Vector2 half = Vector2.Scale(rt.rect.size, rt.localScale) * 0.5f;

        Vector3 pos = rt.localPosition;
        pos.x = Mathf.Clamp(pos.x, min.x + half.x, max.x - half.x);
        pos.y = Mathf.Clamp(pos.y, min.y + half.y, max.y - half.y);
        rt.localPosition = pos;
    }

    // Only one weapon can be equipped at a time, so a single timer drives whichever it is
    // (or the bare-handed strike when nothing is equipped).
    private void UpdateWeapon()
    {
        string weapon = LevelSelectActions.GetEquippedWeapon();
        if (!TryGetWeaponStats(weapon, out float interval, out int baseDamage))
        {
            weaponTimer = 0f;
            return;
        }

        weaponTimer += Time.deltaTime;
        if (weaponTimer < interval) return;
        weaponTimer = 0f;

        if (string.IsNullOrEmpty(weapon))
        {
            FireAtNearest(UnarmedStrike);
            return;
        }

        int damage = baseDamage + LevelSelectActions.GetWeaponBonusDamage(weapon);
        switch (weapon)
        {
            case LevelSelectActions.SatelliteItemName: FireSatellitePulse(damage); break;
            case LevelSelectActions.RayGunItemName: FireAtNearest(target => FireLaser(target, damage)); break;
            case LevelSelectActions.SwordItemName: FireSwordVolley(damage); break;
            case LevelSelectActions.MachineDrillItemName: FireDrill(damage); break;
        }
    }

    private bool TryGetWeaponStats(string weapon, out float interval, out int baseDamage)
    {
        if (string.IsNullOrEmpty(weapon))
        {
            interval = unarmedStrikeInterval;
            baseDamage = unarmedStrikeDamage;
            return true;
        }

        switch (weapon)
        {
            case LevelSelectActions.SatelliteItemName:
                interval = Mathf.Max(0.5f, satellitePulseInterval - satellitePulseIntervalPerLevel * LevelSelectActions.GetWeaponLevel(weapon));
                baseDamage = satelliteDamage;
                return true;
            case LevelSelectActions.RayGunItemName: interval = laserFireInterval; baseDamage = laserDamage; return true;
            case LevelSelectActions.SwordItemName: interval = swordFireInterval; baseDamage = swordDamage; return true;
            case LevelSelectActions.MachineDrillItemName: interval = drillFireInterval; baseDamage = drillDamage; return true;
            default: interval = 0f; baseDamage = 0; return false;
        }
    }

    // Hits the target directly; TakeDamage spawns the monster's hit effect.
    private void UnarmedStrike(Monster target)
    {
        UnarmedHitSound.Play();
        target.TakeDamage(unarmedStrikeDamage, rt.anchoredPosition);
    }

    private void FireAtNearest(System.Action<Monster> fire)
    {
        var target = FindTargetMonster();
        if (target != null) fire(target);
    }

    private static T SpawnEffect<T>(GameObject prefab) where T : Component
    {
        return Instantiate(prefab, prefab.transform.parent).GetComponent<T>();
    }

    private void FireSatellitePulse(int damage)
    {
        if (satellitePulsePrefab == null) return;

        MissionManager.AddWeaponUsed(LevelSelectActions.SatelliteItemName);
        SpawnEffect<SatellitePulse>(satellitePulsePrefab).Play(rt.anchoredPosition, damage);
        PlaySfx(satellitePulseSound, satellitePulseVolume);
    }

    // Two bolts per volley; the second follows after a fixed delay so the gap between
    // shots stays constant regardless of how far away the target is.
    private void FireLaser(Monster target, int damage)
    {
        FireLaserBolt(target, damage);
        StartCoroutine(FireFollowUpLaserBolt(target, damage));
    }

    private IEnumerator FireFollowUpLaserBolt(Monster target, int damage)
    {
        yield return new WaitForSeconds(laserFollowUpDelay);
        if (target != null && target.gameObject.activeSelf) FireLaserBolt(target, damage);
    }

    private void FireLaserBolt(Monster target, int damage)
    {
        if (laserBoltPrefab == null) return;

        MissionManager.AddWeaponUsed(LevelSelectActions.RayGunItemName);
        SpawnEffect<LaserBolt>(laserBoltPrefab).Play(rt.anchoredPosition, target, damage);
        PlaySfx(laserShotSound, laserShotVolume);
    }

    // At the final upgrade a second sword goes to a different monster (the next-nearest), if there is one.
    private void FireSwordVolley(int damage)
    {
        var first = FindTargetMonster();
        if (first == null) return;

        FireSword(first, damage, true);
        if (LevelSelectActions.GetWeaponLevel(LevelSelectActions.SwordItemName) < LevelSelectActions.MaxWeaponLevel) return;

        var second = FindTargetMonster(first);
        if (second != null) FireSword(second, damage, false);   // one throw sound per volley
    }

    private void FireSword(Monster target, int damage, bool playSound)
    {
        if (swordSlashPrefab == null) return;

        MissionManager.AddWeaponUsed(LevelSelectActions.SwordItemName);
        SpawnEffect<SwordSlash>(swordSlashPrefab).Play(rt, target, damage);

        if (playSound && swordThrowSounds != null && swordThrowSounds.Length > 0)
        {
            PlaySfx(swordThrowSounds[nextSwordSound], swordThrowVolume);
            nextSwordSound = (nextSwordSound + 1) % swordThrowSounds.Length;
        }
    }

    private void FireDrill(int damage)
    {
        if (drillBoltPrefab == null) return;

        MissionManager.AddWeaponUsed(LevelSelectActions.MachineDrillItemName);
        PlaySfx(drillFireSound, drillFireVolume);

        int level = LevelSelectActions.GetWeaponLevel(LevelSelectActions.MachineDrillItemName);
        int forward = Mathf.Min(drillBaseCount + level, drillMaxForwardCount);
        FireDrillFan(forward, 0f, Mathf.Min(DrillSpreadStep, 2f * DrillMaxHalfSpread / Mathf.Max(1, forward - 1)), damage);
        if (level >= LevelSelectActions.MaxWeaponLevel) FireDrillFan(drillRearCountAtMax, 180f, DrillRearSpread, damage);
    }

    // `count` drills centered on (facing direction + centerDeg), `stepDeg` apart.
    private void FireDrillFan(int count, float centerDeg, float stepDeg, int damage)
    {
        for (int i = 0; i < count; i++)
        {
            float angleDeg = centerDeg + (i - (count - 1) * 0.5f) * stepDeg;
            Vector2 dir = Quaternion.Euler(0f, 0f, angleDeg) * facingDirection;
            SpawnEffect<DrillBolt>(drillBoltPrefab).Play(rt.anchoredPosition, dir, damage);
        }
    }

    // The monster under the mouse cursor wins; otherwise the nearest fully on-screen one.
    private Monster FindTargetMonster(Monster exclude = null)
    {
        var cam = Camera.main;
        var monsters = Monster.Active;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mousePos = mouse.position.ReadValue();
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] == exclude) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(monsters[i].RectTransform, mousePos, cam)) return monsters[i];
            }
        }

        Monster nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == exclude) continue;
            var monsterRt = monsters[i].RectTransform;
            if (!IsFullyOnScreen(monsterRt, cam)) continue;

            float sqr = (rt.anchoredPosition - monsterRt.anchoredPosition).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = monsters[i];
            }
        }
        return nearest;
    }

    private static bool IsFullyOnScreen(RectTransform target, Camera cam)
    {
        if (cam == null) return true;

        target.GetWorldCorners(CornerBuffer);
        foreach (var corner in CornerBuffer)
        {
            Vector3 v = cam.WorldToViewportPoint(corner);
            if (v.z < 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
        }
        return true;
    }

    public void FlashHit()
    {
        if (image == null) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        var wait = new WaitForSeconds(hitFlashInterval);
        for (int i = 0; i < hitFlashCount; i++)
        {
            image.enabled = false;
            yield return wait;
            image.enabled = true;
            yield return wait;
        }
        flashRoutine = null;
    }
}
