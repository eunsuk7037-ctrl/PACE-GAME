using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Coin/mushroom pickup popup: floats up from where a monster died and fades away.
public class ItemDrop : MonoBehaviour
{
    public float riseDistance = 60f;
    public float duration = 0.8f;

    public void Play(Vector2 anchoredPosition)
    {
        var rt = (RectTransform)transform;
        rt.anchoredPosition = anchoredPosition;
        gameObject.SetActive(true);
        StartCoroutine(Animate(rt));
    }

    private IEnumerator Animate(RectTransform rt)
    {
        var img = GetComponent<Image>();
        Vector2 start = rt.anchoredPosition;
        Vector2 end = start + Vector2.up * riseDistance;

        for (float t = 0f; t < duration; )
        {
            t += Time.deltaTime;
            float p = t / duration;
            rt.anchoredPosition = Vector2.Lerp(start, end, p);
            UIUtil.SetAlpha(img, 1f - p);
            yield return null;
        }

        Destroy(gameObject);
    }
}
