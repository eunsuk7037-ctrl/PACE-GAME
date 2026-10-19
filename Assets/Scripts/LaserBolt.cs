using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LaserBolt : MonoBehaviour
{
    public float speed = 3500f;
    public float hitDistance = 20f;
    public float fadeOutDuration = 0.1f;

    private Monster target;
    private int damage;

    public void Play(Vector2 startAnchoredPosition, Monster targetMonster, int damageAmount)
    {
        var rt = (RectTransform)transform;
        rt.anchoredPosition = startAnchoredPosition;
        target = targetMonster;
        damage = damageAmount;

        gameObject.SetActive(true);
        StartCoroutine(Travel(rt));
    }

    // Homes in on the target; if it dies first, the bolt just fades out where it is.
    private IEnumerator Travel(RectTransform rt)
    {
        while (target != null && target.gameObject.activeSelf)
        {
            Vector2 toTarget = target.RectTransform.anchoredPosition - rt.anchoredPosition;
            float step = speed * Time.deltaTime;

            if (toTarget.magnitude <= Mathf.Max(step, hitDistance))
            {
                target.TakeDamage(damage, rt.anchoredPosition);
                break;
            }

            float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
            rt.rotation = Quaternion.Euler(0f, 0f, angle);
            rt.anchoredPosition += toTarget.normalized * step;

            yield return null;
        }

        yield return UIUtil.FadeOutAndDestroy(GetComponent<Image>(), fadeOutDuration);
    }
}
