using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Plays after the final chapter: the outro cutscene (final blow, the planets purified one by
// one), then the hero holds the line against planets drifting in from the background while
// the credits come down the screen. Tap/click anywhere to skip.
public class EndingCredits : MonoBehaviour
{
    public GameObject root;

    [Tooltip("Plays first; the credits start when it ends (or is skipped).")]
    public OutroCutscene outro;
    [Tooltip("Off: the ending stops when the outro does (back to chapter select), no credits roll.")]
    public bool showCredits = false;

    [Header("Placeholder intro (used only when no outro is set)")]
    [Tooltip("Shown first for introDuration seconds when there's no outro; leave empty to skip.")]
    public GameObject introRoot;
    public float introDuration = 3f;
    [Tooltip("While true, the intro's label shows the localized 'intro coming soon' placeholder.")]
    public bool introIsPlaceholder = true;

    [Header("Scene")]
    public RectTransform playField;
    public RectTransform hero;
    public Image planetTemplate;
    public Sprite[] planetSprites;
    public Image boltTemplate;
    public float planetSpawnInterval = 0.6f;
    public float planetSpeed = 160f;
    public float planetStartScale = 0.35f;
    public float planetEndScale = 1.2f;
    public float fireInterval = 0.9f;
    public float boltSpeed = 1800f;
    [Tooltip("The hero flies in from below the screen over this many seconds before the credits start.")]
    public float heroEntranceDuration = 1.2f;

    [Header("Credits")]
    public RectTransform creditsText;
    public TMP_Text creditsLabel;
    public string developer = "Developer Name";
    public string producer = "Producer Name";
    public int productionDays = 30;
    [Tooltip("How fast the credits come down, in canvas units per second.")]
    public float scrollSpeed = 110f;
    public float endHold = 2f;

    private readonly List<RectTransform> planets = new List<RectTransform>();
    private Coroutine routine;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    public void Play()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    // Any click/tap during the ending skips straight to the end.
    public void Skip()
    {
        if (routine != null) StopCoroutine(routine);
        Finish();
    }

    private IEnumerator Run()
    {
        if (outro != null)
        {
            // The credits come up behind the outro's last fade from black.
            bool done = false;
            outro.Play(() =>
            {
                done = true;
                outro.EnsureMusic();
                if (showCredits) root.SetActive(true);
            });
            while (!done) yield return null;

            if (!showCredits)
            {
                routine = null;
                yield break;
            }
        }

        root.SetActive(true);

        if (outro == null && introRoot != null)
        {
            var introLabel = introRoot.GetComponentInChildren<TMP_Text>();
            if (introIsPlaceholder && introLabel != null) introLabel.text = LocalizationManager.Get("intro_placeholder", "INTRO\n(coming soon)");
            introRoot.SetActive(true);
            yield return new WaitForSeconds(introDuration);
            introRoot.SetActive(false);
        }

        creditsLabel.text = BuildCreditsText();
        creditsText.anchoredPosition = new Vector2(creditsText.anchoredPosition.x, playField.rect.height * 2f); // hidden until the hero is in
        yield return HeroEntrance();
        creditsLabel.ForceMeshUpdate();
        float textHeight = creditsLabel.preferredHeight;
        float fieldHalfHeight = playField.rect.height * 0.5f;

        // Starts just above the top edge and comes down until it has left the bottom.
        creditsText.anchoredPosition = new Vector2(creditsText.anchoredPosition.x, fieldHalfHeight + textHeight * 0.5f);
        float endY = -fieldHalfHeight - textHeight * 0.5f;

        float spawnTimer = 0f, fireTimer = 0f;
        while (creditsText.anchoredPosition.y > endY)
        {
            float dt = Time.deltaTime;
            creditsText.anchoredPosition += Vector2.down * scrollSpeed * dt;

            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                spawnTimer = planetSpawnInterval;
                SpawnPlanet();
            }

            fireTimer -= dt;
            if (fireTimer <= 0f && planets.Count > 0)
            {
                fireTimer = fireInterval;
                StartCoroutine(FireBolt(NearestPlanet()));
            }

            MovePlanets(dt);
            yield return null;
        }

        yield return new WaitForSeconds(endHold);
        Finish();
    }

    private Vector2 heroHome;
    private bool heroHomeKnown;

    private IEnumerator HeroEntrance()
    {
        if (!heroHomeKnown)
        {
            heroHome = hero.anchoredPosition;
            heroHomeKnown = true;
        }
        Vector2 start = new Vector2(heroHome.x, -playField.rect.height * 0.5f - hero.rect.height);
        for (float t = 0f; t < heroEntranceDuration; t += Time.deltaTime)
        {
            float p = t / heroEntranceDuration;
            hero.anchoredPosition = Vector2.Lerp(start, heroHome, 1f - (1f - p) * (1f - p)); // ease out
            yield return null;
        }
        hero.anchoredPosition = heroHome;
    }

    // The text block comes down from the top, so its bottom line shows up first - the
    // sections are stacked in reverse to make them appear in reading order.
    private string BuildCreditsText()
    {
        string section = "<size=70%>{0}</size>\n{1}";
        string[] sections =
        {
            string.Format(section, LocalizationManager.Get("credits_developer", "Developer"), developer),
            string.Format(section, LocalizationManager.Get("credits_producer", "Producer"), producer),
            string.Format(section, LocalizationManager.Get("credits_days", "Production Time"),
                string.Format(LocalizationManager.Get("credits_days_value", "{0} days"), productionDays)),
            LocalizationManager.Get("credits_thanks", "Thank you for playing!"),
        };
        System.Array.Reverse(sections);
        return string.Join("\n\n\n", sections);
    }

    // Planets appear small somewhere around the edge (far away, behind the scene) and grow
    // as they close in on the hero.
    private void SpawnPlanet()
    {
        if (planetSprites == null || planetSprites.Length == 0) return;

        var img = Instantiate(planetTemplate, planetTemplate.transform.parent);
        img.sprite = planetSprites[Random.Range(0, planetSprites.Length)];
        img.gameObject.SetActive(true);

        // Somewhere along the top/upper sides of the screen, so they drift down toward the
        // hero standing near the bottom.
        var rt = img.rectTransform;
        Vector2 half = playField.rect.size * 0.5f;
        float angle = Random.Range(15f, 165f) * Mathf.Deg2Rad;
        rt.anchoredPosition = new Vector2(Mathf.Cos(angle) * half.x, Mathf.Sin(angle) * half.y) * 1.05f;
        rt.localScale = Vector3.one * planetStartScale;
        planets.Add(rt);
    }

    private void MovePlanets(float dt)
    {
        float maxDistance = playField.rect.size.magnitude * 0.5f;
        for (int i = planets.Count - 1; i >= 0; i--)
        {
            var rt = planets[i];
            Vector2 toHero = hero.anchoredPosition - rt.anchoredPosition;
            rt.anchoredPosition += toHero.normalized * planetSpeed * dt;

            float closeness = 1f - Mathf.Clamp01(toHero.magnitude / maxDistance);
            rt.localScale = Vector3.one * Mathf.Lerp(planetStartScale, planetEndScale, closeness);

            // A planet that slips through just fades out at the hero - the ending never fails.
            if (toHero.magnitude < 40f) RemovePlanet(rt);
        }
    }

    private RectTransform NearestPlanet()
    {
        RectTransform nearest = null;
        float best = float.MaxValue;
        foreach (var rt in planets)
        {
            float d = (rt.anchoredPosition - hero.anchoredPosition).sqrMagnitude;
            if (d < best) { best = d; nearest = rt; }
        }
        return nearest;
    }

    private IEnumerator FireBolt(RectTransform target)
    {
        var bolt = Instantiate(boltTemplate, boltTemplate.transform.parent);
        bolt.gameObject.SetActive(true);
        var rt = bolt.rectTransform;
        rt.anchoredPosition = hero.anchoredPosition;

        while (target != null && planets.Contains(target))
        {
            Vector2 toTarget = target.anchoredPosition - rt.anchoredPosition;
            float step = boltSpeed * Time.deltaTime;
            if (toTarget.magnitude <= step)
            {
                RemovePlanet(target);
                break;
            }
            rt.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f);
            rt.anchoredPosition += toTarget.normalized * step;
            yield return null;
        }
        Destroy(bolt.gameObject);
    }

    private void RemovePlanet(RectTransform planet)
    {
        planets.Remove(planet);
        StartCoroutine(PopAndDestroy(planet));
    }

    private static IEnumerator PopAndDestroy(RectTransform planet)
    {
        var img = planet.GetComponent<Image>();
        Vector3 startScale = planet.localScale;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            float p = t / 0.25f;
            planet.localScale = startScale * (1f + p * 0.5f);
            UIUtil.SetAlpha(img, 1f - p);
            yield return null;
        }
        Destroy(planet.gameObject);
    }

    private void Finish()
    {
        StopAllCoroutines();
        routine = null;
        planets.Clear();
        // Planets and bolts still mid-flight are runtime clones under the play field.
        for (int i = playField.childCount - 1; i >= 0; i--)
        {
            var child = playField.GetChild(i);
            if (child.name.EndsWith("(Clone)")) Destroy(child.gameObject);
        }
        if (introRoot != null) introRoot.SetActive(false);
        root.SetActive(false);
    }
}
