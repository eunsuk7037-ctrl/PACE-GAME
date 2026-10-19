using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Monster : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool isBoss = false;
    public bool isElite = false;
    public int maxHealth = 10;
    public float moveSpeed = 130f;
    public float spawnDistance = 1400f;
    public float touchDistance = 60f;
    public float attackInterval = 0.3f;
    public int attackPower = 1;
    public float touchCooldown = 1f;
    public RectTransform target;
    public GameObject hitEffectPrefab;

    // Pattern monsters (walls, streams) hold a straight line instead of homing in on the
    // player, so a formation keeps its shape as it sweeps across the screen.
    [Header("Pattern movement")]
    public bool movesInFixedDirection;
    public Vector2 fixedDirection;
    public float fixedTravelLimit = 3000f;
    [Tooltip("Side-to-side sway while moving in a fixed direction (0 = straight line). Gives snake formations their wiggle.")]
    public float zigzagAmplitude;
    [Tooltip("Sway cycles per canvas unit travelled (2*PI / wavelength).")]
    public float zigzagFrequency;
    [Tooltip("Sway phase offset, so members further back in a snake trail the head's wiggle instead of swaying in lockstep.")]
    public float zigzagPhase;
    [Tooltip("While homing, also circle around the player at this rate - a ring with this set spirals in instead of closing straight.")]
    public float orbitDegreesPerSecond;

    // Homing monsters all converge on the player, so without this they stack into one blob.
    [Header("Separation")]
    [Tooltip("How close another monster may get before being pushed away. 0 = use this monster's own width, so bigger monsters (bosses) keep more room.")]
    public float separationRadius;
    [Tooltip("How hard overlapping monsters push apart (fraction of the overlap resolved per second).")]
    public float separationStrength = 14f;

    // Every spawned, alive monster - lets weapons and separation check monsters without
    // searching the scene. Removed again when the monster is deactivated (dies/leaves).
    private static readonly List<Monster> ActiveMonsters = new List<Monster>();
    public static IReadOnlyList<Monster> Active => ActiveMonsters;

    public event Action<Monster> OnDefeated;
    public event Action<Monster> OnReachedPlayer;

    // Multi-phase bosses: return true to cancel this death (the handler refills health and moves to
    // its next phase). Invulnerable monsters ignore damage, e.g. during a phase change.
    public Func<Monster, bool> InterceptDeath;
    // Fired for every hit that lands, with where the attack came from (stage anchored position) when known.
    public event Action<Monster, Vector2?> OnDamaged;
    [NonSerialized] public bool invulnerable;
    public void RestoreHealth() => currentHealth = maxHealth;

    private RectTransform rt;
    private int currentHealth;
    private bool isSpawned;
    private bool isRegistered;
    private bool isBeingHeld;
    private float attackTimer;
    private float touchCooldownTimer;

    private float fixedTravelled;
    private Vector2 fixedBasePosition;

    private bool isApproachingPoint;
    private Vector2 approachPoint;
    private float approachSwitchDistance;

    public RectTransform RectTransform => rt;

    private float SeparationRadius => separationRadius > 0f ? separationRadius : rt.rect.width;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void OnDisable()
    {
        if (!isRegistered) return;
        ActiveMonsters.Remove(this);
        isRegistered = false;
    }

    // ---- Spawning ----

    public void Spawn()
    {
        SpawnAtAngle(UnityEngine.Random.Range(0f, Mathf.PI * 2f));
    }

    // Places the monster at `angleRadians` around the player. By default it sits just at
    // the visible screen edge; a positive explicitDistance puts it a fixed radius out
    // instead, so formation members land on a true circle rather than a screen-shaped ring.
    public void SpawnAtAngle(float angleRadians, float explicitDistance = -1f)
    {
        PrepareSpawn();

        Vector2 origin = target != null ? target.anchoredPosition : Vector2.zero;
        Vector2 dir = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));

        float distance = explicitDistance;
        if (distance <= 0f)
        {
            float edgeDistance = GetScreenEdgeDistance(origin, dir);
            distance = edgeDistance > 0f ? edgeDistance : spawnDistance;
        }
        Activate(origin + dir * distance);
    }

    // Places the monster at an exact anchored position - used by pattern waves that lay
    // monsters out in a formation (a wall, a stream) rather than around the player.
    public void SpawnAtPosition(Vector2 anchoredPosition)
    {
        PrepareSpawn();
        Activate(anchoredPosition);
    }

    // Call right after spawning: the monster first closes in on a fixed map point (so a
    // ring keeps its shape instead of sliding after the player), then switches to homing
    // on the player once it has covered half the distance to that point.
    public void ApproachPointFirst(Vector2 point)
    {
        approachPoint = point;
        approachSwitchDistance = Vector2.Distance(rt.anchoredPosition, point) * 0.5f;
        isApproachingPoint = true;
    }

    private void PrepareSpawn()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        if (target == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) target = player.GetComponent<RectTransform>();
        }

        currentHealth = maxHealth;
        isBeingHeld = false;
        attackTimer = 0f;
        touchCooldownTimer = 0f;
        fixedTravelled = 0f;
        isApproachingPoint = false;
    }

    private void Activate(Vector2 anchoredPosition)
    {
        rt.anchoredPosition = anchoredPosition;
        fixedBasePosition = anchoredPosition;
        gameObject.SetActive(true);
        isSpawned = true;

        if (!isRegistered)
        {
            ActiveMonsters.Add(this);
            isRegistered = true;
        }
    }

    // How far along `direction` (from `origin`) the visible screen edge lies, or -1 if it
    // can't be measured (no camera), so the caller can fall back to a fixed distance.
    private float GetScreenEdgeDistance(Vector2 origin, Vector2 direction)
    {
        if (!UIUtil.TryGetVisibleBounds(rt.parent as RectTransform, out Vector2 min, out Vector2 max)) return -1f;

        float tx = float.PositiveInfinity;
        if (direction.x > 0f) tx = (max.x - origin.x) / direction.x;
        else if (direction.x < 0f) tx = (min.x - origin.x) / direction.x;

        float ty = float.PositiveInfinity;
        if (direction.y > 0f) ty = (max.y - origin.y) / direction.y;
        else if (direction.y < 0f) ty = (min.y - origin.y) / direction.y;

        float t = Mathf.Min(tx, ty);
        return (t > 0f && !float.IsInfinity(t)) ? t : -1f;
    }

    // ---- Per-frame ----

    private void Update()
    {
        if (!isSpawned) return;

        UpdateHeldAttack();
        if (!isSpawned || target == null) return; // the held attack may have just killed it

        if (movesInFixedDirection)
        {
            if (!MoveInFixedDirection()) return;
        }
        else
        {
            MoveTowardGoal();
        }

        UpdateTouch();
    }

    // Holding a finger/mouse on a monster keeps damaging it when no weapon is equipped.
    private void UpdateHeldAttack()
    {
        if (!isBeingHeld) return;
        if (LevelSelectActions.IsAnyWeaponEquipped())
        {
            isBeingHeld = false;
            return;
        }

        attackTimer += Time.deltaTime;
        if (attackTimer < attackInterval) return;
        attackTimer -= attackInterval;
        UnarmedHitSound.Play();
        TakeDamage(1);
    }

    // Returns false once the monster has swept past the screen and vanished (no reward).
    private bool MoveInFixedDirection()
    {
        float step = moveSpeed * Time.deltaTime;
        fixedBasePosition += fixedDirection * step;
        fixedTravelled += step;

        Vector2 sway = Vector2.zero;
        if (zigzagAmplitude > 0f)
        {
            Vector2 side = new Vector2(-fixedDirection.y, fixedDirection.x);
            sway = side * (Mathf.Sin(fixedTravelled * zigzagFrequency + zigzagPhase) * zigzagAmplitude);
        }
        rt.anchoredPosition = fixedBasePosition + sway;

        if (fixedTravelled < fixedTravelLimit) return true;

        isSpawned = false;
        gameObject.SetActive(false);
        return false;
    }

    private void MoveTowardGoal()
    {
        Vector2 goal = target.anchoredPosition;
        if (isApproachingPoint)
        {
            if ((rt.anchoredPosition - approachPoint).sqrMagnitude <= approachSwitchDistance * approachSwitchDistance)
                isApproachingPoint = false;
            else
                goal = approachPoint;
        }

        if (orbitDegreesPerSecond != 0f)
        {
            Vector2 fromGoal = rt.anchoredPosition - goal;
            rt.anchoredPosition = goal + (Vector2)(Quaternion.Euler(0f, 0f, orbitDegreesPerSecond * Time.deltaTime) * fromGoal);
        }
        rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, goal, moveSpeed * Time.deltaTime);
        // Capped so a long frame can't overshoot and fling stacked monsters apart.
        rt.anchoredPosition += GetSeparationPush() * Mathf.Min(separationStrength * Time.deltaTime, 0.5f);
    }

    private void UpdateTouch()
    {
        if (touchCooldownTimer > 0f) touchCooldownTimer -= Time.deltaTime;
        if (touchCooldownTimer > 0f) return;
        if (Vector2.Distance(rt.anchoredPosition, target.anchoredPosition) > touchDistance) return;

        OnReachedPlayer?.Invoke(this);
        touchCooldownTimer = touchCooldown;
    }

    // Monsters right on top of the player stop separating (and are ignored by the rest),
    // otherwise a crowd would hold every member back out of touch range and the player
    // could never be hit.
    private bool IsAtPlayer => target != null && (rt.anchoredPosition - target.anchoredPosition).sqrMagnitude < SeparationRadius * SeparationRadius;

    // Spring-like push away from every monster currently overlapping this one. Only homing
    // monsters apply it to themselves - formations (walls, snakes) stay rigid and the
    // homing ones get nudged around them instead.
    private Vector2 GetSeparationPush()
    {
        Vector2 push = Vector2.zero;
        if (IsAtPlayer) return push;

        Vector2 me = rt.anchoredPosition;
        float myRadius = SeparationRadius;

        for (int i = 0; i < ActiveMonsters.Count; i++)
        {
            var other = ActiveMonsters[i];
            if (other == this || other.IsAtPlayer) continue;

            float minDistance = (myRadius + other.SeparationRadius) * 0.5f;
            Vector2 away = me - other.rt.anchoredPosition;
            float sqr = away.sqrMagnitude;
            if (sqr >= minDistance * minDistance) continue;

            float distance = Mathf.Sqrt(sqr);
            if (distance < 0.01f)
            {
                // Exactly stacked: pick a stable per-monster direction to split them.
                float angle = GetInstanceID() * 2.39996f;
                away = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                distance = 1f;
            }
            push += away / distance * (minDistance - distance);
        }

        return push;
    }

    // ---- Damage ----

    public void OnPointerDown(PointerEventData eventData)
    {
        if (LevelSelectActions.IsAnyWeaponEquipped()) return;

        isBeingHeld = true;
        attackTimer = 0f;
        UnarmedHitSound.Play();
        TakeDamage(1);
    }

    public void OnPointerUp(PointerEventData eventData) => isBeingHeld = false;
    public void OnPointerExit(PointerEventData eventData) => isBeingHeld = false;

    // `from`: where the hit came from (projectile position, in the same space as anchoredPosition), if known.
    public void TakeDamage(int amount, Vector2? from = null)
    {
        if (!isSpawned || invulnerable) return;

        currentHealth -= amount;
        SpawnHitEffect();
        MissionManager.AddAttackForStage(StageProgress.CurrentStageIndex);
        OnDamaged?.Invoke(this, from);

        if (currentHealth <= 0)
        {
            if (InterceptDeath != null && InterceptDeath(this)) return;
            Die();
        }
    }

    private void SpawnHitEffect()
    {
        if (hitEffectPrefab == null) return;

        // Canvas is Screen Space - Camera, so rt.position is already a real world
        // position on the canvas plane. Nudge toward the camera so the effect
        // doesn't get z-fought against the UI it's rendered alongside.
        var cam = Camera.main;
        Vector3 toCamera = cam != null ? (cam.transform.position - rt.position).normalized : Vector3.back;
        Instantiate(hitEffectPrefab, rt.position + toCamera, Quaternion.identity);
    }

    private void Die()
    {
        isSpawned = false;
        gameObject.SetActive(false);
        OnDefeated?.Invoke(this);
    }
}
