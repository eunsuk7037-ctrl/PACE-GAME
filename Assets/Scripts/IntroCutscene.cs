using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Opening cutscene, played when the game starts, on top of the title screen:
//   1. Night sky fades in from black, letterbox bars slide in, stars twinkle   "Far away, in the Milky Way."
//   2. The camera glides past the ten planets, Mercury to the Sun (parallax)   "Ten planets shone together in peace."
//   3. Family shot: the planets slowly orbit the Sun
//   4. Teaser: music fades, purple darkness creeps in from the edges, the planets
//      lose their colour and UFO silhouettes descend                          "Until the day the darkness came..."
//   5. Fade to black, then the title screen.
public class IntroCutscene : Cutscene
{
    [Header("Intro art")]
    public Sprite vignetteSprite;
    public Sprite cometSprite;
    public Sprite ufoSprite;

    [Header("Timing")]
    public float paradeSeconds = 12.5f;
    public float familySeconds = 4.5f;
    public float teaserSeconds = 5f;

    private const float PlanetSpacing = 620f;
    private const float ParadeStartX = W * 0.5f + 300f;   // Mercury waits just off the right edge

    private RectTransform track, family, teaser;
    private Image vignette;
    private readonly List<RectTransform> paradePlanets = new List<RectTransform>();
    private readonly List<TextMeshProUGUI> paradeCaptions = new List<TextMeshProUGUI>();
    private readonly List<Image> familyPlanets = new List<Image>();
    private Image sunGlow;
    private Color sunGlowBase;

    protected override string CanvasName => "IntroCanvas";

    protected override void ResetScene()
    {
        paradePlanets.Clear();
        paradeCaptions.Clear();
        familyPlanets.Clear();
    }

    // ---------------- Timeline ----------------

    protected override IEnumerator Run()
    {
        if (bgm != null && music != null) bgm.PlayClip(music);

        // 1. Opening
        yield return Opening();
        yield return Wait(1.0f);
        yield return Narrate(L("Far away, in the Milky Way.", "머나먼 은하, 밀키웨이."), 2.6f);

        // 2. Planet parade
        StartCoroutine(Parade());
        yield return Wait(4.0f);
        StartCoroutine(Comet(new Vector2(-1100f, 420f), new Vector2(1100f, 140f), 1.6f));
        yield return Wait(0.6f);
        yield return Narrate(L("Ten planets shone together in peace.", "열 개의 행성이 사이좋게 빛나고 있었어요."), 3.6f);
        StartCoroutine(Comet(new Vector2(1100f, 470f), new Vector2(-900f, 250f), 1.4f));
        yield return Wait(Mathf.Max(0f, paradeSeconds - 4.0f - 0.6f - 3.6f - 1.3f));

        // 3. Family shot
        yield return CrossToFamily(1.0f);
        yield return Wait(familySeconds);

        // 4. Teaser
        if (bgm != null) bgm.FadeOut(teaserSeconds * 0.8f);
        StartCoroutine(Darkness(teaserSeconds));
        StartCoroutine(Ufos());
        yield return Wait(0.6f);
        yield return Narrate(L("Until the day the darkness came...", "그날, 어둠이 찾아오기 전까지는…"), teaserSeconds - 0.8f, new Color(0.85f, 0.7f, 1f));

        // 5. Out
        yield return Finish(1.2f);
    }

    // Planets breathe: bob and sway a little.
    protected override void Tick()
    {
        for (int i = 0; i < paradePlanets.Count; i++)
        {
            var p = paradePlanets[i];
            var img = p.GetChild(1);   // [0] glow, [1] planet
            img.localPosition = new Vector3(0f, Mathf.Sin(clock * 0.9f + i * 0.8f) * 14f, 0f);
            img.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(clock * 0.6f + i) * 3f);
            float pulse = 1f + 0.05f * Mathf.Sin(clock * 1.3f + i);
            p.GetChild(0).localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    private IEnumerator Parade()
    {
        // The track starts with Mercury just off the right edge and ends with the Sun centred.
        float startX = ParadeStartX;
        float endX = -PlanetSpacing * (planetSprites.Length - 1);
        for (float t = 0f; t < paradeSeconds && !skipping; t += Dt)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / paradeSeconds);
            float x = Mathf.Lerp(startX, endX, p);
            track.anchoredPosition = new Vector2(x, 0f);

            // Parallax: the sky and stars drift much slower than the planets.
            float travelled = startX - x;
            background.rectTransform.anchoredPosition = new Vector2(-travelled * 0.05f, 0f);
            starLayer.localPosition = new Vector3(-travelled * 0.18f, 0f, 0f);

            // Names show while their planet is near the centre.
            for (int i = 0; i < paradePlanets.Count; i++)
            {
                float screenX = x + paradePlanets[i].anchoredPosition.x;
                paradeCaptions[i].alpha = Mathf.Clamp01(1f - Mathf.Abs(screenX) / 420f);
            }
            yield return null;
        }
    }

    private IEnumerator CrossToFamily(float seconds)
    {
        var trackGroup = track.gameObject.AddComponent<CanvasGroup>();
        var familyGroup = family.gameObject.AddComponent<CanvasGroup>();
        familyGroup.alpha = 0f;
        family.gameObject.SetActive(true);
        StartCoroutine(Orbit());
        for (float t = 0f; t < seconds; t += Dt)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / seconds);
            trackGroup.alpha = 1f - p;
            familyGroup.alpha = p;
            track.localScale = Vector3.one * (1f - 0.15f * p);   // pull back as we cut wide
            yield return null;
        }
        track.gameObject.SetActive(false);
        familyGroup.alpha = 1f;
    }

    // Nine planets on a tilted ellipse around the Sun, slowly going round.
    private IEnumerator Orbit()
    {
        const float rx = 760f, ry = 300f;
        int n = familyPlanets.Count - 1;   // last one is the Sun
        while (root != null)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + clock * 0.12f;
                var rt = familyPlanets[i].rectTransform;
                rt.anchoredPosition = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry - 20f + Mathf.Sin(clock + i) * 6f);
                // Planets "behind" the Sun (top of the ellipse) are smaller.
                float depth = 0.5f - 0.5f * Mathf.Sin(a);
                rt.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.08f, depth);
                rt.SetSiblingIndex(Mathf.Sin(a) > 0f ? 0 : family.childCount - 1);
            }
            yield return null;
        }
    }

    private IEnumerator Darkness(float seconds)
    {
        vignette.gameObject.SetActive(true);
        for (float t = 0f; t < seconds && !skipping; t += Dt)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / seconds);
            vignette.color = new Color(0.10f, 0.02f, 0.18f, p * 0.97f);
            vignette.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.9f, 1.0f, p);
            foreach (var img in familyPlanets) img.color = Color.Lerp(Color.white, Dominated, p * 0.9f);
            if (sunGlow != null) sunGlow.color = new Color(sunGlowBase.r, sunGlowBase.g, sunGlowBase.b, sunGlowBase.a * (1f - p));
            background.color = Color.Lerp(Color.white, new Color(0.45f, 0.35f, 0.62f), p);
            // A faint tremor once the shadow has nearly arrived.
            float shake = Mathf.Max(0f, p - 0.6f) * 10f;
            world.anchoredPosition = Random.insideUnitCircle * shake;
            yield return null;
        }
    }

    private IEnumerator Ufos()
    {
        var specs = new[]
        {
            (from: new Vector2(-1150f, 650f), to: new Vector2(-560f, 330f), size: 210f, delay: 0.4f),
            (from: new Vector2(1150f, 700f), to: new Vector2(600f, 360f), size: 240f, delay: 1.1f),
            (from: new Vector2(0f, 800f), to: new Vector2(30f, 430f), size: 170f, delay: 1.8f),
        };
        foreach (var s in specs) StartCoroutine(Ufo(s.from, s.to, s.size, s.delay));
        yield break;
    }

    private IEnumerator Ufo(Vector2 from, Vector2 to, float size, float delay)
    {
        yield return Wait(delay);
        // A dark silhouette with an eerie red glow under it - menacing, not cute.
        var holder = CreateRect("Ufo", teaser);
        var glow = CreateImage("Glow", holder, glowSprite, new Color(1f, 0.15f, 0.3f, 0f), new Vector2(size * 1.3f, size * 0.7f));
        glow.rectTransform.anchoredPosition = new Vector2(0f, -size * 0.28f);
        var body = CreateImage("Body", holder, ufoSprite, new Color(0.05f, 0.01f, 0.09f, 0f), new Vector2(size, size));
        const float seconds = 2.2f;
        for (float t = 0f; holder != null; t += Dt)   // hovers until the cutscene is torn down
        {
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
            float fade = Mathf.Clamp01(t / 1.2f);
            holder.anchoredPosition = Vector2.Lerp(from, to, p) + new Vector2(0f, Mathf.Sin(t * 2.2f) * 8f);
            holder.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 1.7f) * 4f);
            holder.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, p);   // grows as it comes closer
            body.color = new Color(0.05f, 0.01f, 0.09f, fade * 0.96f);
            glow.color = new Color(1f, 0.15f, 0.3f, fade * (0.45f + 0.25f * Mathf.Sin(t * 5f)));
            yield return null;
        }
    }

    private IEnumerator Comet(Vector2 from, Vector2 to, float seconds)
    {
        var comet = CreateImage("Comet", world, cometSprite, new Color(1f, 1f, 1f, 0f), new Vector2(150f, 150f));
        comet.transform.SetSiblingIndex(2);   // above the stars, behind the planets
        Vector2 dir = (to - from).normalized;
        // The art points down-left; turn it to fly along `dir`.
        comet.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 135f);
        for (float t = 0f; t < seconds && comet != null; t += Dt)
        {
            float p = t / seconds;
            comet.rectTransform.anchoredPosition = Vector2.Lerp(from, to, p);
            comet.color = new Color(1f, 1f, 1f, Mathf.Sin(p * Mathf.PI) * 0.9f);
            yield return null;
        }
        if (comet != null) Destroy(comet.gameObject);
    }

    // ---------------- Building ----------------

    protected override void BuildScene()
    {
        // Parade track.
        track = CreateRect("PlanetTrack", world);
        track.anchoredPosition = new Vector2(ParadeStartX, 0f);   // off screen until the parade starts
        for (int i = 0; i < planetSprites.Length; i++)
        {
            var holder = CreateRect("Planet_" + PlanetNamesEn[i], track);
            float size = PlanetSizes[i];
            holder.anchoredPosition = new Vector2(i * PlanetSpacing, (i % 2 == 0 ? 40f : -30f));
            var glowColor = PlanetGlow[i];
            glowColor.a = 0.22f;
            CreateImage("Glow", holder, glowSprite, glowColor, new Vector2(size, size) * 1.9f);
            var planet = CreateImage("Planet", holder, planetSprites[i], Color.white, new Vector2(size, size));
            planet.preserveAspect = true;
            var caption = CreateText("Name", holder, L(PlanetNamesEn[i], PlanetNamesKo[i]), 54f, Color.white);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -size * 0.5f - 70f);
            caption.characterSpacing = 6f;
            caption.alpha = 0f;
            paradePlanets.Add(holder);
            paradeCaptions.Add(caption);
        }

        // Family shot (hidden until the cut).
        family = CreateRect("Family", world);
        family.gameObject.SetActive(false);
        for (int i = 0; i < planetSprites.Length; i++)
        {
            bool sun = i == planetSprites.Length - 1;
            float size = sun ? 330f : PlanetSizes[i] * 0.42f;
            var p = CreateImage(PlanetNamesEn[i], family, planetSprites[i], Color.white, new Vector2(size, size));
            p.preserveAspect = true;
            familyPlanets.Add(p);
        }
        sunGlowBase = PlanetGlow[planetSprites.Length - 1];
        sunGlowBase.a = 0.3f;
        sunGlow = CreateImage("SunGlow", family, glowSprite, sunGlowBase, new Vector2(760f, 760f));
        sunGlow.transform.SetAsFirstSibling();

        teaser = CreateRect("Teaser", world);
        // Oversized so its solid edge always covers the screen edge, even at full scale-in and while shaking.
        vignette = CreateImage("Shadow", teaser, vignetteSprite, new Color(0.10f, 0.02f, 0.18f, 0f), new Vector2(W, H) * 1.25f);
        vignette.gameObject.SetActive(false);
    }
}
