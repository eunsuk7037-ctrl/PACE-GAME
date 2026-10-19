using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SwordSlash : MonoBehaviour
{
    public float outboundSpeed = 1300f;
    public float returnSpeed = 1300f;
    public float hitDistance = 24f;
    public float spinDegreesPerSecond = 1080f;
    public float fadeOutDuration = 0.1f;

    private RectTransform playerRt;
    private Monster target;
    private int damage;

    public void Play(RectTransform playerRectTransform, Monster targetMonster, int damageAmount)
    {
        var rt = (RectTransform)transform;
        rt.anchoredPosition = playerRectTransform.anchoredPosition;

        playerRt = playerRectTransform;
        target = targetMonster;
        damage = damageAmount;

        gameObject.SetActive(true);
        StartCoroutine(Run(rt));
    }

    // Spins out to the target, hits it, then boomerangs back to the player.
    private IEnumerator Run(RectTransform rt)
    {
        while (target != null && target.gameObject.activeSelf)
        {
            Vector2 from = rt.anchoredPosition;   // before Step lands it on the target
            if (Step(rt, target.RectTransform.anchoredPosition, outboundSpeed))
            {
                target.TakeDamage(damage, from);
                break;
            }
            yield return null;
        }

        while (playerRt != null && !Step(rt, playerRt.anchoredPosition, returnSpeed))
        {
            yield return null;
        }

        yield return UIUtil.FadeOutAndDestroy(GetComponent<Image>(), fadeOutDuration);
    }

    // Spins and moves one frame toward `goal`; returns true once it's within hit range.
    private bool Step(RectTransform rt, Vector2 goal, float speed)
    {
        rt.Rotate(0f, 0f, -spinDegreesPerSecond * Time.deltaTime);

        Vector2 toGoal = goal - rt.anchoredPosition;
        float step = speed * Time.deltaTime;
        if (toGoal.magnitude <= Mathf.Max(step, hitDistance)) return true;

        rt.anchoredPosition += toGoal.normalized * step;
        return false;
    }
}
