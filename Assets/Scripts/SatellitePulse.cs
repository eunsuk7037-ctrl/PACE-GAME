using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SatellitePulse : MonoBehaviour
{
    public float duration = 1.2f;
    public float startScale = 0.2f;
    public float startAlpha = 0.35f;
    public float coverageMargin = 1.15f;
    public int damage = 1;

    private readonly HashSet<Monster> hitMonsters = new HashSet<Monster>();

    public void Play(Vector2 anchoredPosition, int damageAmount)
    {
        damage = damageAmount;
        var rt = (RectTransform)transform;
        rt.anchoredPosition = anchoredPosition;
        rt.localScale = Vector3.one * startScale;
        UIUtil.SetAlpha(GetComponent<Image>(), startAlpha);

        hitMonsters.Clear();
        gameObject.SetActive(true);
        StartCoroutine(Animate(rt));
    }

    // Expands from the player until it covers the whole screen, hitting each monster once.
    private IEnumerator Animate(RectTransform rt)
    {
        var img = GetComponent<Image>();
        float baseRadius = rt.rect.width * 0.5f;
        float startRadius = baseRadius * startScale;
        float endRadius = Mathf.Max(startRadius, ComputeScreenCoverRadius(rt) * coverageMargin);

        for (float t = 0f; t < duration; )
        {
            t += Time.deltaTime;
            float p = t / duration;
            float radius = Mathf.Lerp(startRadius, endRadius, p);
            if (baseRadius > 0f) rt.localScale = Vector3.one * (radius / baseRadius);
            UIUtil.SetAlpha(img, Mathf.Lerp(startAlpha, 0f, p));

            DamageMonstersInRange(rt.anchoredPosition, radius);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void DamageMonstersInRange(Vector2 center, float radius)
    {
        var monsters = Monster.Active;
        // Backwards: a killed monster removes itself from the list mid-loop.
        for (int i = monsters.Count - 1; i >= 0; i--)
        {
            var monster = monsters[i];
            if (hitMonsters.Contains(monster)) continue;
            if (Vector2.Distance(center, monster.RectTransform.anchoredPosition) > radius) continue;

            hitMonsters.Add(monster);
            monster.TakeDamage(damage, center);
        }
    }

    // Distance from the pulse's center to the farthest visible screen corner.
    private static float ComputeScreenCoverRadius(RectTransform rt)
    {
        if (!UIUtil.TryGetVisibleBounds(rt.parent as RectTransform, out Vector2 min, out Vector2 max)) return 2000f;

        Vector2 c = rt.anchoredPosition;
        float dx = Mathf.Max(Mathf.Abs(min.x - c.x), Mathf.Abs(max.x - c.x));
        float dy = Mathf.Max(Mathf.Abs(min.y - c.y), Mathf.Abs(max.y - c.y));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
