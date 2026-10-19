using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The SUN chapter's final boss: three phases. Each time it "dies" it comes back for the next phase
// with full health and a harder bullet pattern.
//  Phase 1 - two clear barriers; slow rings
//  Phase 2 - one barrier left; twin spiral + aimed bursts
//  Phase 3 - no barrier, hits land on the boss itself; triple spiral + dense rings, moves faster
// Between phases the boss is briefly invulnerable, the outer barrier shatters, its bullets clear
// and "PHASE N" shows. While a barrier is up, only the part of it facing each hit glows red.
public class FinalBoss : MonoBehaviour
{
    private const int PhaseCount = 3;
    private const float PhaseChangeSeconds = 1.2f;
    private const float BulletSize = 40f;
    private const float PhaseSpeedMultiplier = 1.2f;

    private const float InnerBarrierScale = 1.12f;   // barrier diameter vs the boss image
    private const float BarrierScaleStep = 0.16f;    // each further barrier is this much bigger
    // The barrier sprite is clear inside with a thin rim; this alpha only scales that rim.
    private static readonly Color BarrierColor = new Color(0.82f, 0.95f, 1f, 0.6f);
    private static readonly Color BarrierHitColor = new Color(1f, 0.15f, 0.1f, 0.85f);
    private static readonly Color BossHitColor = new Color(1f, 0.45f, 0.45f, 1f);

    private static readonly Color[] PhaseBulletColors =
    {
        new Color(1f, 0.90f, 0.30f),   // yellow
        new Color(1f, 0.60f, 0.15f),   // orange
        new Color(1f, 0.25f, 0.20f),   // red
    };

    private Monster boss;
    private Image image;
    private StageProgress stage;
    private RectTransform player;
    private Sprite bulletSprite;
    private Sprite barrierSprite;
    private int phase = 1;
    private bool changingPhase;
    private float spin;

    private readonly List<Image> barriers = new List<Image>();   // [0] innermost; the last one is hit and breaks first
    private Color bossColor = Color.white;                        // current resting tint (shifts each phase)
    private float hitReactUntil;

    public static FinalBoss Attach(Monster boss, StageProgress stage, RectTransform player, Sprite bulletSprite, Sprite barrierSprite)
    {
        var fb = boss.gameObject.AddComponent<FinalBoss>();
        fb.boss = boss;
        fb.image = boss.GetComponent<Image>();
        fb.stage = stage;
        fb.player = player;
        fb.bulletSprite = bulletSprite;
        fb.barrierSprite = barrierSprite;
        if (fb.image != null) fb.bossColor = fb.image.color;
        boss.InterceptDeath = fb.OnLethalDamage;
        boss.OnDamaged += fb.OnDamaged;

        // One barrier per phase except the last.
        if (barrierSprite != null)
        {
            Vector2 bossSize = boss.RectTransform.rect.size;
            for (int i = 0; i < PhaseCount - 1; i++)
            {
                var b = CreateImage("Barrier", boss.RectTransform, barrierSprite, BarrierColor, bossSize * (InnerBarrierScale + BarrierScaleStep * i));
                fb.barriers.Add(b);
            }
        }
        return fb;
    }

    private void Start()
    {
        StartCoroutine(FireLoop());
    }

    private void Update()
    {
        // Barriers shimmer slightly, out of step with each other.
        for (int i = 0; i < barriers.Count; i++)
        {
            var c = BarrierColor;
            c.a *= 0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f + i * 1.7f);
            barriers[i].color = c;
        }
    }

    private bool OnLethalDamage(Monster m)
    {
        if (phase >= PhaseCount) return false;   // last phase: really dies

        phase++;
        m.RestoreHealth();
        StartCoroutine(PhaseChange());
        return true;
    }

    private IEnumerator PhaseChange()
    {
        changingPhase = true;
        boss.invulnerable = true;
        ClearBullets();
        stage.ShowBossPhase(phase);
        if (barriers.Count > 0) ShatterOuterBarrier();

        // Flash, then come back tinted toward this phase's colour and faster.
        bossColor = Color.Lerp(Color.white, PhaseBulletColors[phase - 1], 0.35f);
        for (float t = 0f; t < PhaseChangeSeconds; t += Time.deltaTime)
        {
            float a = Mathf.PingPong(t * 10f, 1f) > 0.5f ? 0.35f : 1f;
            if (image != null) image.color = new Color(bossColor.r, bossColor.g, bossColor.b, a);
            yield return null;
        }
        if (image != null) image.color = bossColor;
        boss.moveSpeed *= PhaseSpeedMultiplier;

        boss.invulnerable = false;
        changingPhase = false;
    }

    // ---- Hits ----

    private void OnDamaged(Monster m, Vector2? from)
    {
        if (barriers.Count > 0) MarkBarrierHit(from);
        else if (Time.time >= hitReactUntil) StartCoroutine(HitReact());
    }

    // A red patch on the outer barrier, on the side the hit came from.
    private void MarkBarrierHit(Vector2? from)
    {
        var outer = barriers[barriers.Count - 1];
        Vector2 source = from ?? (player != null ? player.anchoredPosition : Vector2.zero);
        Vector2 dir = source - boss.RectTransform.anchoredPosition;
        if (dir.sqrMagnitude < 1f) dir = Random.insideUnitCircle;
        dir.Normalize();

        float radius = outer.rectTransform.rect.width * 0.5f;
        var patch = CreateImage("BarrierHit", boss.RectTransform, bulletSprite, BarrierHitColor, new Vector2(radius * 0.75f, radius * 0.32f));
        patch.rectTransform.anchoredPosition = dir * radius * 0.9f;
        patch.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);   // lies along the rim
        StartCoroutine(FadeOut(patch, 0.35f));
    }

    // No barrier left: the boss itself flinches - a red flash and a quick squash.
    private IEnumerator HitReact()
    {
        const float seconds = 0.12f;
        hitReactUntil = Time.time + seconds;
        var rt = boss.RectTransform;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / seconds * Mathf.PI);
            if (image != null) image.color = Color.Lerp(bossColor, BossHitColor * bossColor, k);
            rt.localScale = new Vector3(1f + 0.06f * k, 1f - 0.08f * k, 1f);
            yield return null;
        }
        if (image != null) image.color = bossColor;
        rt.localScale = Vector3.one;
    }

    // The outer barrier flashes, swells and fades while shards fly off.
    private void ShatterOuterBarrier()
    {
        var barrier = barriers[barriers.Count - 1];
        barriers.RemoveAt(barriers.Count - 1);
        StartCoroutine(BarrierBurst(barrier));

        var bossRt = boss.RectTransform;
        float radius = barrier.rectTransform.rect.width * 0.5f;
        for (int i = 0; i < 18; i++)
        {
            float angle = (i + Random.value * 0.6f) / 18f * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float size = Random.Range(18f, 36f);
            // Under the boss's parent so the shards stay where it broke; "(Clone)" so a stage end sweeps them.
            var shard = CreateImage("BarrierShard(Clone)", (RectTransform)bossRt.parent, barrierSprite,
                new Color(BarrierColor.r, BarrierColor.g, BarrierColor.b, 0.9f), new Vector2(size, size * 0.55f));
            shard.rectTransform.anchoredPosition = bossRt.anchoredPosition + dir * radius * 0.95f;
            StartCoroutine(FlyShard(shard, dir * Random.Range(260f, 480f)));
        }
    }

    private IEnumerator BarrierBurst(Image barrier)
    {
        const float seconds = 0.4f;
        var rt = barrier.rectTransform;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float p = t / seconds;
            rt.localScale = Vector3.one * (1f + 0.18f * p);
            barrier.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 0f, p));
            yield return null;
        }
        Destroy(barrier.gameObject);
    }

    private IEnumerator FlyShard(Image shard, Vector2 velocity)
    {
        const float life = 0.6f;
        var rt = shard.rectTransform;
        float spinSpeed = Random.Range(-540f, 540f);
        var start = shard.color;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            rt.anchoredPosition += velocity * Time.deltaTime;
            velocity *= 1f - 2.5f * Time.deltaTime;   // drag
            rt.localEulerAngles += new Vector3(0f, 0f, spinSpeed * Time.deltaTime);
            shard.color = new Color(start.r, start.g, start.b, start.a * (1f - t / life));
            yield return null;
        }
        Destroy(shard.gameObject);
    }

    private IEnumerator FadeOut(Image img, float seconds)
    {
        var start = img.color;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (img == null) yield break;
            img.color = new Color(start.r, start.g, start.b, start.a * (1f - t / seconds));
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private IEnumerator FireLoop()
    {
        yield return new WaitForSeconds(1f);
        while (true)
        {
            if (changingPhase)
            {
                yield return null;
                continue;
            }

            switch (phase)
            {
                case 1: yield return PhaseOne(); break;
                case 2: yield return PhaseTwo(); break;
                default: yield return PhaseThree(); break;
            }
        }
    }

    // Slow 14-bullet rings, each one turned a little.
    private IEnumerator PhaseOne()
    {
        Ring(14, 230f, spin);
        spin += 12f;
        yield return new WaitForSeconds(1.6f);
    }

    // Two arms spiralling out, then a 3-shot burst aimed at the player.
    private IEnumerator PhaseTwo()
    {
        for (int i = 0; i < 18 && !changingPhase; i++)
        {
            Shoot(spin, 240f);
            Shoot(spin + 180f, 240f);
            spin += 14f;
            yield return new WaitForSeconds(0.12f);
        }
        if (changingPhase) yield break;

        Aimed(3, 12f, 380f);
        yield return new WaitForSeconds(0.8f);
    }

    // Three arms spiralling out, then a dense ring.
    private IEnumerator PhaseThree()
    {
        for (int i = 0; i < 30 && !changingPhase; i++)
        {
            for (int arm = 0; arm < 3; arm++) Shoot(spin + arm * 120f, 260f);
            spin += 11f;
            yield return new WaitForSeconds(0.09f);
        }
        if (changingPhase) yield break;

        Ring(20, 200f, spin);
        yield return new WaitForSeconds(0.6f);
    }

    private void Ring(int count, float speed, float offsetDeg)
    {
        for (int i = 0; i < count; i++) Shoot(offsetDeg + i * 360f / count, speed);
    }

    private void Aimed(int count, float spreadDeg, float speed)
    {
        if (player == null) return;
        Vector2 toPlayer = player.anchoredPosition - boss.RectTransform.anchoredPosition;
        float center = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++) Shoot(center + (i - (count - 1) * 0.5f) * spreadDeg, speed);
    }

    private void Shoot(float angleDeg, float speed)
    {
        if (bulletSprite == null) return;

        var go = new GameObject("BossBullet(Clone)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        var bossRt = boss.RectTransform;
        rt.SetParent(bossRt.parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(BulletSize, BulletSize);
        rt.anchoredPosition = bossRt.anchoredPosition;

        var img = go.GetComponent<Image>();
        img.sprite = bulletSprite;
        img.color = PhaseBulletColors[phase - 1];
        img.raycastTarget = false;

        // White-hot centre so bullets stay readable against the purple sky.
        var core = new GameObject("Core", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var coreRt = (RectTransform)core.transform;
        coreRt.SetParent(rt, false);
        coreRt.anchorMin = coreRt.anchorMax = coreRt.pivot = new Vector2(0.5f, 0.5f);
        coreRt.sizeDelta = new Vector2(BulletSize, BulletSize) * 0.45f;
        var coreImg = core.GetComponent<Image>();
        coreImg.sprite = bulletSprite;
        coreImg.raycastTarget = false;

        float rad = angleDeg * Mathf.Deg2Rad;
        go.AddComponent<BossBullet>().Init(stage, player, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed);
    }

    private void ClearBullets()
    {
        foreach (var bullet in boss.RectTransform.parent.GetComponentsInChildren<BossBullet>()) Destroy(bullet.gameObject);
    }
}
