using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DrillBolt : MonoBehaviour
{
    public float speed = 900f;
    public float lifetime = 0.6f;
    public float fadeOutFraction = 0.3f;
    public float hitDistance = 70f;

    private Vector2 direction;
    private int damage;
    private readonly HashSet<Monster> hitMonsters = new HashSet<Monster>();

    public void Play(Vector2 startAnchoredPosition, Vector2 travelDirection, int damageAmount)
    {
        var rt = (RectTransform)transform;
        rt.anchoredPosition = startAnchoredPosition;

        direction = travelDirection.normalized;
        damage = damageAmount;
        // The drill sprite's tip points down by default, not up, so it needs a +90
        // offset instead of the -90 used by sprites that face up at rest (e.g. LaserBolt).
        rt.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);

        hitMonsters.Clear();
        gameObject.SetActive(true);
        StartCoroutine(Travel(rt));
    }

    // Flies straight, piercing every monster it passes (each one only once), and fades
    // out over the last part of its lifetime.
    private IEnumerator Travel(RectTransform rt)
    {
        var img = GetComponent<Image>();
        float startAlpha = img.color.a;
        float fadeStart = lifetime * (1f - fadeOutFraction);

        for (float t = 0f; t < lifetime; )
        {
            t += Time.deltaTime;
            rt.anchoredPosition += direction * speed * Time.deltaTime;
            DamageMonstersInRange(rt.anchoredPosition);

            if (t > fadeStart) UIUtil.SetAlpha(img, Mathf.Lerp(startAlpha, 0f, (t - fadeStart) / (lifetime - fadeStart)));
            yield return null;
        }

        Destroy(gameObject);
    }

    private void DamageMonstersInRange(Vector2 position)
    {
        var monsters = Monster.Active;
        // Backwards: a killed monster removes itself from the list mid-loop.
        for (int i = monsters.Count - 1; i >= 0; i--)
        {
            var monster = monsters[i];
            if (hitMonsters.Contains(monster)) continue;
            if (Vector2.Distance(position, monster.RectTransform.anchoredPosition) > hitDistance) continue;

            hitMonsters.Add(monster);
            monster.TakeDamage(damage, position);
        }
    }
}
