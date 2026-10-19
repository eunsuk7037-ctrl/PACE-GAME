using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The translucent "mind control" membrane over a chapter boss (the planet the aliens have taken):
// it pulses and throws off electric sparks while the boss is alive. When the boss dies, Melt()
// detaches it, leaves the freed planet in place, and lets the membrane drain away top-down while
// drops fall off it.
//
// Everything is UI (Images under the boss's canvas), so sparks stay behind popups like Pause/Settings.
public class BossShield : MonoBehaviour
{
    private const float SizeMultiplier = 1.15f;   // membrane diameter vs the boss image
    private const float MeltSeconds = 1.4f;
    private const float FreedHoldSeconds = 0.3f; // freed planet stays alone this long before STAGE CLEAR
    private const float FreedFadeSeconds = 0.6f;
    private const float SparkFps = 12f;

    private Image bubble;
    private Color color;
    private Sprite[] sparkFrames;
    private float nextSparkIn;
    private bool melting;

    public static BossShield Attach(Monster boss, Sprite shieldSprite, Color shieldColor, Sprite[] sparkFrames)
    {
        var bossRt = boss.RectTransform != null ? boss.RectTransform : (RectTransform)boss.transform;

        // No "(Clone)" in the name: ClearLeftoverEffects runs as the stage clear starts and must not
        // sweep the melting membrane. StageProgress.EnterStage clears any left over instead.
        var go = new GameObject("BossShield", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(bossRt, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = bossRt.rect.size * SizeMultiplier;

        var shield = go.AddComponent<BossShield>();
        shield.color = shieldColor;
        shield.sparkFrames = sparkFrames;
        shield.bubble = CreateImage("Membrane", rt, shieldSprite, shieldColor, rt.sizeDelta);
        shield.bubble.type = Image.Type.Filled;
        shield.bubble.fillMethod = Image.FillMethod.Vertical;
        shield.bubble.fillOrigin = (int)Image.OriginVertical.Bottom;
        shield.nextSparkIn = Random.Range(0.2f, 0.6f);
        return shield;
    }

    private void Update()
    {
        if (melting) return;

        // Slow uneasy pulse.
        float t = Time.time;
        bubble.rectTransform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 2.5f));
        var c = color;
        c.a = color.a * (0.85f + 0.15f * Mathf.Sin(t * 3.7f));
        bubble.color = c;

        nextSparkIn -= Time.deltaTime;
        if (nextSparkIn <= 0f)
        {
            SpawnSpark(Random.Range(0.42f, 0.5f));
            nextSparkIn = Random.Range(0.35f, 1.1f);
        }
    }

    // Detaches from the (now inactive) boss and plays the melt. Returns how long the caller should
    // wait before showing STAGE CLEAR.
    public float Melt(Monster boss)
    {
        melting = true;
        var rt = (RectTransform)transform;
        var bossRt = boss.RectTransform;
        rt.SetParent(bossRt.parent, true);   // boss is inactive now; keep the membrane alive on its own
        rt.SetSiblingIndex(bossRt.GetSiblingIndex() + 1);

        // The freed planet, left where the boss was.
        var bossImage = boss.GetComponent<Image>();
        Image planet = null;
        if (bossImage != null)
        {
            planet = CreateImage("FreedPlanet", rt, bossImage.sprite, bossImage.color, bossRt.rect.size);
            planet.preserveAspect = bossImage.preserveAspect;
            planet.transform.SetAsFirstSibling();
        }

        StartCoroutine(MeltRoutine(planet));
        return MeltSeconds + FreedHoldSeconds;
    }

    private IEnumerator MeltRoutine(Image planet)
    {
        var brt = bubble.rectTransform;
        brt.localScale = Vector3.one;
        float height = brt.rect.height;

        for (int i = 0; i < 3; i++) SpawnSpark(Random.Range(0.3f, 0.5f));   // the hold snaps

        float dripTimer = 0f;
        for (float t = 0f; t < MeltSeconds; t += Time.deltaTime)
        {
            float p = t / MeltSeconds;
            float eased = p * p;                                  // starts slow, then gives way
            bubble.fillAmount = 1f - eased;
            brt.localScale = new Vector3(1f + 0.12f * eased, 1f, 1f);   // sags outward as it drains
            var c = color;
            c.a = color.a * (1f - 0.5f * p);
            bubble.color = c;

            dripTimer -= Time.deltaTime;
            if (dripTimer <= 0f)
            {
                // Drops come off the bottom half, below the falling surface.
                float surface = -height * 0.5f + height * bubble.fillAmount;
                float y = Random.Range(-height * 0.45f, Mathf.Min(surface, 0f));
                float x = Random.Range(-0.35f, 0.35f) * brt.rect.width;
                StartCoroutine(Drip(new Vector2(x, y)));
                dripTimer = Random.Range(0.04f, 0.1f);
            }
            yield return null;
        }
        bubble.enabled = false;

        yield return new WaitForSeconds(FreedHoldSeconds);

        if (planet != null)
        {
            var start = planet.color;
            for (float t = 0f; t < FreedFadeSeconds; t += Time.deltaTime)
            {
                planet.color = new Color(start.r, start.g, start.b, start.a * (1f - t / FreedFadeSeconds));
                yield return null;
            }
        }

        // Let any last drops finish before going.
        yield return new WaitForSeconds(0.4f);
        Destroy(gameObject);
    }

    private IEnumerator Drip(Vector2 start)
    {
        float size = Random.Range(16f, 30f);
        var drop = CreateImage("Drop", (RectTransform)transform, bubble.sprite, color, new Vector2(size, size * 1.25f));
        var drt = drop.rectTransform;
        drt.anchoredPosition = start;

        const float life = 0.7f;
        float vy = Random.Range(-60f, -20f);
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            vy -= 1400f * Time.deltaTime;
            drt.anchoredPosition += new Vector2(0f, vy * Time.deltaTime);
            var c = color;
            c.a = Mathf.Min(1f, color.a * 1.6f) * (1f - t / life);
            drop.color = c;
            yield return null;
        }
        Destroy(drop.gameObject);
    }

    // One electric spark on the membrane, `radiusFraction` of the membrane's size out from the centre.
    private void SpawnSpark(float radiusFraction)
    {
        if (sparkFrames == null || sparkFrames.Length == 0) return;

        var rt = (RectTransform)transform;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = rt.rect.width * radiusFraction;
        float size = Random.Range(90f, 140f);
        var spark = CreateImage("Spark", rt, sparkFrames[0], Color.white, new Vector2(size, size));
        spark.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        spark.rectTransform.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
        StartCoroutine(PlaySpark(spark));
    }

    private IEnumerator PlaySpark(Image spark)
    {
        var wait = new WaitForSeconds(1f / SparkFps);
        foreach (var frame in sparkFrames)
        {
            spark.sprite = frame;
            yield return wait;
        }
        Destroy(spark.gameObject);
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
        img.raycastTarget = false;   // never block taps on the boss or the UI
        return img;
    }
}
