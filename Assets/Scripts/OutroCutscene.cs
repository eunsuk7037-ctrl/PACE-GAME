using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ending cutscene, played after the final boss falls and before the credits:
//   1. Final battle - under a dark purple sky the hero fires at the source of the darkness;
//      a last charged beam, a white flash, and it bursts into light.     "The final battle."
//   2. Purification - the ten planets, still wrapped in the purple membrane, are freed one by
//      one, Mercury to the Sun: the membrane pops, the colour comes back, sparkles fly, and
//      the sky brightens a little with each.                              "One by one, the planets got their light back."
//   3. Peace - gentle music, the hero flies past the restored planets.   "Peace returned to the galaxy."
public class OutroCutscene : Cutscene
{
    [Header("Outro art")]
    public Sprite heroSprite;
    public Sprite finalBossSprite;
    [Tooltip("The purple 'dominated' membrane the chapter bosses wear.")]
    public Sprite membraneSprite;

    [Header("Timing")]
    public float purifyStepSeconds = 0.75f;

    private static readonly Color DarkSky = new Color(0.38f, 0.28f, 0.52f);
    private static readonly Color MembraneColor = new Color(0.72f, 0.38f, 1f, 0.55f);
    private static readonly Color BeamColor = new Color(0.65f, 1f, 1f, 1f);

    private CanvasGroup battle, galaxy;
    private RectTransform boss, hero;
    private Image bossImage, bossGlow, heroGlow, flash;
    private bool bossAlive;
    private readonly List<RectTransform> planetHolders = new List<RectTransform>();
    private readonly List<Image> planetImages = new List<Image>();
    private readonly List<Image> planetGlows = new List<Image>();
    private readonly List<Image> membranes = new List<Image>();

    private Vector2 BossHome => new Vector2(0f, 190f);
    private Vector2 HeroHome => new Vector2(0f, -250f);

    protected override string CanvasName => "OutroCanvas";
    protected override bool FadeMusicOnSkip => false;   // the peaceful track carries on under the credits

    // Called when handing over to the credits: make sure the peaceful track is on (a skip during
    // the battle would otherwise leave it silent).
    public void EnsureMusic()
    {
        if (bgm != null && music != null) bgm.PlayClip(music);
    }

    protected override void ResetScene()
    {
        planetHolders.Clear();
        planetImages.Clear();
        planetGlows.Clear();
        membranes.Clear();
    }

    // ---------------- Timeline ----------------

    protected override IEnumerator Run()
    {
        if (bgm != null) bgm.FadeOut(1.2f);   // the battle plays out in near silence
        bossAlive = true;

        // 1. Final battle
        yield return Opening();
        yield return Wait(0.8f);
        StartCoroutine(Narrate(L("The final battle.", "마지막 결전."), 2.4f, new Color(1f, 0.8f, 0.85f)));
        yield return Wait(0.8f);
        for (int i = 0; i < 3; i++)
        {
            StartCoroutine(Beam(48f, 0.3f, BeamColor));
            StartCoroutine(BossHit(6f));
            yield return Wait(0.5f);
        }
        yield return FinalBlow();
        yield return Narrate(L("As the source of darkness vanished...", "어둠의 근원이 사라지자…"), 2.6f);

        // 2. Purification
        yield return CrossToGalaxy(1.0f);
        if (bgm != null && music != null) bgm.PlayClip(music);
        StartCoroutine(Narrate(L("Light returned to every planet,\nand the membranes that forced them into hostility vanished.",
                                 "행성마다 빛이 돌아오고\n강제로 적대하게 만든 막이 사라졌어요."), purifyStepSeconds * planetHolders.Count + 1.2f));
        yield return Wait(0.6f);
        for (int i = 0; i < planetHolders.Count; i++)
        {
            StartCoroutine(Purify(i));
            yield return Wait(purifyStepSeconds);
        }
        yield return Wait(1.4f);

        // 3. Peace
        yield return Narrate(L("Peace returned to the galaxy.", "은하에 다시 평화가 찾아왔어요."), 3.6f);

        // The hero flies off into the distance with the farewell line; the cutscene ends as it leaves.
        StartCoroutine(Narrate(FarewellLine(), 5.2f, new Color(1f, 0.92f, 0.7f)));
        yield return FlyBy(5.0f);
        yield return Wait(0.6f);

        yield return Finish(1.4f);
    }

    protected override void Tick()
    {
        // The boss looms and wobbles; the hero hovers.
        if (bossAlive && boss != null)
        {
            boss.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(clock * 1.3f) * 4f);
            float s = 1f + 0.03f * Mathf.Sin(clock * 2.1f);
            bossGlow.rectTransform.localScale = new Vector3(s, s, 1f) * (1f + 0.08f * Mathf.Sin(clock * 3.3f));
        }
        if (hero != null && battle.gameObject.activeSelf)
            hero.anchoredPosition = HeroHome + new Vector2(Mathf.Sin(clock * 1.1f) * 10f, Mathf.Sin(clock * 2.2f) * 8f);

        // Planets bob gently.
        for (int i = 0; i < planetImages.Count; i++)
            planetImages[i].rectTransform.localPosition = new Vector3(0f, Mathf.Sin(clock * 1.1f + i * 0.7f) * 8f, 0f);
    }

    // "And so, <player> vanished beyond the stars..." - the account or guest name, or a stand-in if neither is set.
    private static string FarewellLine()
    {
        string name = GameSave.DisplayName;
        if (LocalizationManager.IsKorean)
        {
            if (string.IsNullOrEmpty(name)) name = "우주 영웅";
            return "그렇게 " + name + TopicParticle(name) + "\n다음 행성을 위해 저 우주 너머로 사라졌습니다.";
        }
        if (string.IsNullOrEmpty(name)) name = "the space hero";
        return "And so, " + name + " vanished beyond the stars,\noff to save the next planet.";
    }

    // 은 after a final consonant, 는 after a vowel. Digits and Latin letters go by how they're read in Korean.
    private static string TopicParticle(string word)
    {
        char last = word[word.Length - 1];
        bool batchim;
        if (last >= '가' && last <= '힣') batchim = (last - '가') % 28 != 0;
        else if (char.IsDigit(last)) batchim = "013678".IndexOf(last) >= 0;   // 영 일 삼 육 칠 팔
        else if (char.IsLetter(last)) batchim = "lmnrLMNR".IndexOf(last) >= 0; // 엘 엠 엔 알
        else batchim = false;
        return batchim ? "은" : "는";
    }

    // ---------------- Battle ----------------

    // A straight beam from the hero to the boss that flares and fades.
    private IEnumerator Beam(float width, float seconds, Color color)
    {
        Vector2 from = hero.anchoredPosition + new Vector2(0f, 40f);
        Vector2 to = boss.anchoredPosition;
        var beam = CreateImage("Beam", battle.transform, glowSprite, color, new Vector2(width, (to - from).magnitude));
        beam.rectTransform.anchoredPosition = (from + to) * 0.5f;
        beam.transform.SetSiblingIndex(1);   // under the boss, over its glow
        for (float t = 0f; t < seconds && beam != null; t += Dt)
        {
            float p = t / seconds;
            beam.rectTransform.sizeDelta = new Vector2(width * (1f + 0.4f * Mathf.Sin(p * Mathf.PI)), beam.rectTransform.sizeDelta.y);
            beam.color = new Color(color.r, color.g, color.b, color.a * (1f - p * p));
            yield return null;
        }
        if (beam != null) Destroy(beam.gameObject);
    }

    private IEnumerator BossHit(float shake)
    {
        const float seconds = 0.25f;
        for (float t = 0f; t < seconds && bossAlive; t += Dt)
        {
            float k = 1f - t / seconds;
            bossImage.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.4f), k);
            boss.anchoredPosition = BossHome + Random.insideUnitCircle * shake * k;
            yield return null;
        }
        if (bossAlive)
        {
            bossImage.color = Color.white;
            boss.anchoredPosition = BossHome;
        }
    }

    // The hero charges, a wide beam holds on the boss while it shudders and cracks with light,
    // then a white flash and it bursts apart.
    private IEnumerator FinalBlow()
    {
        heroGlow.gameObject.SetActive(true);
        for (float t = 0f; t < 0.9f; t += Dt)
        {
            float p = t / 0.9f;
            heroGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.0f, p) * (1f + 0.08f * Mathf.Sin(t * 30f));
            heroGlow.color = new Color(0.7f, 1f, 1f, 0.55f * p);
            yield return null;
        }

        StartCoroutine(Beam(110f, 1.4f, new Color(0.9f, 1f, 1f, 0.95f)));
        float sparkTimer = 0f;
        for (float t = 0f; t < 1.4f; t += Dt)
        {
            float p = t / 1.4f;
            boss.anchoredPosition = BossHome + Random.insideUnitCircle * Mathf.Lerp(6f, 26f, p);
            bossImage.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.9f), p);
            sparkTimer -= Dt;
            if (sparkTimer <= 0f)
            {
                // Light breaking out through cracks.
                Vector2 spot = Random.insideUnitCircle * 150f;
                StartCoroutine(Burst(battle.transform, boss.anchoredPosition + spot, 3, 120f, 0.5f, Color.white));
                sparkTimer = Mathf.Lerp(0.18f, 0.05f, p);
            }
            yield return null;
        }
        heroGlow.gameObject.SetActive(false);

        // Gone.
        bossAlive = false;
        boss.gameObject.SetActive(false);
        bossGlow.gameObject.SetActive(false);
        flash.color = Color.white;
        StartCoroutine(Burst(battle.transform, BossHome, 28, 700f, 1.4f, new Color(1f, 0.95f, 0.8f)));
        StartCoroutine(Fade(flash, 1f, 0f, 1.2f));
        StartCoroutine(Brighten(DarkSky, Color.Lerp(DarkSky, Color.white, 0.25f), 1.2f));
        yield return Wait(1.0f);
    }

    // ---------------- Purification ----------------

    private IEnumerator CrossToGalaxy(float seconds)
    {
        galaxy.gameObject.SetActive(true);
        for (float t = 0f; t < seconds; t += Dt)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / seconds);
            battle.alpha = 1f - p;
            galaxy.alpha = p;
            yield return null;
        }
        battle.gameObject.SetActive(false);
        galaxy.alpha = 1f;
    }

    private IEnumerator Purify(int i)
    {
        var holder = planetHolders[i];
        var planet = planetImages[i];
        var glow = planetGlows[i];
        var membrane = membranes[i];
        float size = planet.rectTransform.sizeDelta.x;

        // A ring of light sweeps out from the planet.
        var ring = CreateImage("Wave", holder, glowSprite, new Color(1f, 1f, 1f, 0.8f), new Vector2(size, size));
        ring.transform.SetAsFirstSibling();
        StartCoroutine(Burst(holder, Vector2.zero, 8, 260f, 0.9f, PlanetGlow[i]));

        Color glowTarget = PlanetGlow[i];
        glowTarget.a = 0.35f;
        float skyFrom = i / (float)planetHolders.Count, skyTo = (i + 1) / (float)planetHolders.Count;
        for (float t = 0f; t < 0.7f; t += Dt)
        {
            float p = t / 0.7f;
            float e = 1f - (1f - p) * (1f - p);   // ease out
            ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.6f, e);
            ring.color = new Color(1f, 1f, 1f, 0.8f * (1f - p));

            membrane.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.45f, e);
            membrane.color = new Color(MembraneColor.r, MembraneColor.g, MembraneColor.b, MembraneColor.a * (1f - p));

            planet.color = Color.Lerp(Dominated, Color.white, e);
            glow.color = Color.Lerp(new Color(glowTarget.r, glowTarget.g, glowTarget.b, 0f), glowTarget, e);
            holder.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(p * Mathf.PI));   // a happy little bounce

            background.color = Color.Lerp(SkyAt(skyFrom), SkyAt(skyTo), e);
            yield return null;
        }
        planet.color = Color.white;
        holder.localScale = Vector3.one;
        Destroy(ring.gameObject);
        Destroy(membrane.gameObject);
    }

    // Sky colour over the purification: from the post-battle dusk up to the normal night sky.
    private static Color SkyAt(float progress) => Color.Lerp(Color.Lerp(DarkSky, Color.white, 0.25f), Color.white, progress);

    private IEnumerator Brighten(Color from, Color to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt)
        {
            background.color = Color.Lerp(from, to, t / seconds);
            yield return null;
        }
        background.color = to;
    }

    // ---------------- Peace ----------------

    // The hero sweeps across the restored galaxy, leaving a sparkling trail.
    private IEnumerator FlyBy(float seconds)
    {
        var ship = CreateImage("Hero", galaxy.transform, heroSprite, Color.white, new Vector2(260f, 260f));
        ship.preserveAspect = true;
        Vector2 a = new Vector2(-1150f, -330f), b = new Vector2(0f, -120f), c = new Vector2(1150f, 260f);
        float trail = 0f;
        Vector2 last = a;
        for (float t = 0f; t < seconds && ship != null; t += Dt)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / seconds);
            Vector2 pos = Vector2.Lerp(Vector2.Lerp(a, b, p), Vector2.Lerp(b, c, p), p);   // gentle curve
            Vector2 dir = pos - last;
            if (dir.sqrMagnitude > 0.01f) ship.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            ship.rectTransform.anchoredPosition = pos;
            ship.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, p);   // shrinking into the distance
            last = pos;

            trail -= Dt;
            if (trail <= 0f)
            {
                StartCoroutine(Burst(galaxy.transform, pos - dir.normalized * 50f, 1, 40f, 0.8f, new Color(1f, 0.95f, 0.7f)));
                trail = 0.04f;
            }
            yield return null;
        }
        if (ship != null) Destroy(ship.gameObject);
    }

    // ---------------- Effects ----------------

    // `count` sparkles flying out from `center` and fading.
    private IEnumerator Burst(Transform parent, Vector2 center, int count, float speed, float life, Color color)
    {
        var bits = new List<(Image img, Vector2 vel)>();
        for (int i = 0; i < count; i++)
        {
            float size = Random.Range(18f, 40f);
            var s = CreateImage("Spark", parent, sparkleSprite, color, new Vector2(size, size));
            s.rectTransform.anchoredPosition = center;
            float ang = (i + Random.value) / Mathf.Max(1, count) * Mathf.PI * 2f;
            bits.Add((s, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed * Random.Range(0.6f, 1.1f)));
        }
        for (float t = 0f; t < life; t += Dt)
        {
            float p = t / life;
            for (int i = 0; i < bits.Count; i++)
            {
                var (img, vel) = bits[i];
                if (img == null) continue;
                img.rectTransform.anchoredPosition += vel * Dt * (1f - p);
                img.color = new Color(color.r, color.g, color.b, 1f - p);
                img.rectTransform.localScale = Vector3.one * (1f - 0.5f * p);
            }
            yield return null;
        }
        foreach (var (img, _) in bits) if (img != null) Destroy(img.gameObject);
    }

    // ---------------- Building ----------------

    protected override void BuildScene()
    {
        background.color = DarkSky;

        // Battle layer.
        battle = CreateRect("Battle", world).gameObject.AddComponent<CanvasGroup>();
        bossGlow = CreateImage("BossGlow", battle.transform, glowSprite, new Color(0.85f, 0.2f, 0.6f, 0.45f), new Vector2(760f, 760f));
        bossGlow.rectTransform.anchoredPosition = BossHome;
        boss = CreateRect("FinalBoss", battle.transform);
        boss.anchoredPosition = BossHome;
        bossImage = CreateImage("Body", boss, finalBossSprite, Color.white, new Vector2(440f, 440f));
        bossImage.preserveAspect = true;

        hero = CreateRect("Hero", battle.transform);
        hero.anchoredPosition = HeroHome;
        heroGlow = CreateImage("Charge", hero, glowSprite, new Color(0.7f, 1f, 1f, 0f), new Vector2(220f, 220f));
        heroGlow.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        heroGlow.gameObject.SetActive(false);
        // The ship art has lots of empty margin, so it's drawn large to read at a glance.
        var heroImage = CreateImage("Ship", hero, heroSprite, Color.white, new Vector2(300f, 300f));
        heroImage.preserveAspect = true;

        // The galaxy: the ten planets in a gentle arc, still dominated.
        galaxy = CreateRect("Galaxy", world).gameObject.AddComponent<CanvasGroup>();
        galaxy.alpha = 0f;
        galaxy.gameObject.SetActive(false);
        int n = planetSprites.Length;
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(-800f, 800f, i / (float)(n - 1));
            float y = -10f + 140f * Mathf.Sin(Mathf.PI * i / (n - 1)) + (i % 2 == 0 ? -25f : 25f);
            float size = PlanetSizes[i] * (i == n - 1 ? 0.42f : 0.36f);

            var holder = CreateRect("Planet_" + PlanetNamesEn[i], galaxy.transform);
            holder.anchoredPosition = new Vector2(x, y);
            var glow = CreateImage("Glow", holder, glowSprite, new Color(1f, 1f, 1f, 0f), new Vector2(size, size) * 2.2f);
            var planet = CreateImage("Planet", holder, planetSprites[i], Dominated, new Vector2(size, size));
            planet.preserveAspect = true;
            var membrane = CreateImage("Membrane", holder, membraneSprite, MembraneColor, new Vector2(size, size) * 1.3f);

            planetHolders.Add(holder);
            planetGlows.Add(glow);
            planetImages.Add(planet);
            membranes.Add(membrane);
        }

        // Full-screen white flash for the final blow.
        flash = CreateImage("Flash", world, null, new Color(1f, 1f, 1f, 0f), Vector2.zero);
        Stretch(flash.rectTransform);
    }
}
