using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Small helpers shared by the gameplay UI objects (monsters, projectiles, effects).
public static class UIUtil
{
    // The camera's visible viewport expressed in `space`'s local (anchoredPosition) space.
    // Gameplay_Stage's own rect is much smaller than the screen, so anything that needs
    // the real screen edge has to measure the view rather than the parent rect.
    public static bool TryGetVisibleBounds(RectTransform space, out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;
        var cam = Camera.main;
        if (cam == null || space == null) return false;

        float depth = cam.WorldToScreenPoint(space.position).z;
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Vector2 corner = space.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(x, y, depth)));
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }
        }
        return true;
    }

    public static void SetAlpha(Graphic graphic, float alpha)
    {
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    // Fades the graphic out over `duration`, then destroys its GameObject.
    public static IEnumerator FadeOutAndDestroy(Graphic graphic, float duration)
    {
        float startAlpha = graphic.color.a;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetAlpha(graphic, Mathf.Lerp(startAlpha, 0f, t / duration));
            yield return null;
        }
        Object.Destroy(graphic.gameObject);
    }
}
