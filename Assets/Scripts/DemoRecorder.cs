#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using Unity.Collections;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.UI;

// Editor-only: plays a scripted 2 minute tour of the game (title, intro, chapter select, a stage
// with its boss, the shop and missions, a silhouette teaser of the final boss) and encodes it to
// an MP4 for the presentation.
// Time runs on a fixed 30 fps capture clock, so the result doesn't depend on editor speed.
// Start it in play mode: new GameObject("DemoRecorder").AddComponent<DemoRecorder>().Begin(path);
public class DemoRecorder : MonoBehaviour
{
    private const int Fps = 30, Width = 1920, Height = 1080;   // layout canvas

    // Output size and quality; the small setting keeps a 2 minute video under ~20 MB (slide decks cap clips there).
    public static bool Small;
    private static int OutWidth => Small ? 1280 : Width;
    private static int OutHeight => Small ? 720 : Height;
    private const float TotalSeconds = 120f;
    private const int DemoStage = 5;   // Saturn: busy enough to show off (circle waves, more monsters)

    public static bool Done { get; private set; }
    public static string Status { get; private set; } = "";

    private MediaEncoder encoder;
    private RenderTexture screenRT, outRT;
    private Texture2D frame;
    private bool recording;
    private int framesWritten;

    private Canvas overlay;
    private CanvasGroup captionGroup, cardGroup;
    private Image cardImage;
    private TextMeshProUGUI captionText, cardTitle, cardSub;
    private TMP_FontAsset font;

    private StageProgress stage;
    private bool autopilot;
    private float autopilotClock;
    private Monster boss;
    private float bossClock;
    private bool silhouetteOutro, outroFrozen;

    // Outro layers drawn as black silhouettes; glows and sparkles are hidden. Light (beam, flash),
    // the sky and the stars stay as they are.
    private static readonly HashSet<string> SilhouetteParts = new HashSet<string> { "Body", "Ship", "Hero", "Planet", "Membrane" };
    private static readonly HashSet<string> HiddenParts = new HashSet<string> { "Glow", "Charge", "Wave", "Spark" };
    // The boss's glow becomes a strong backlight so its black silhouette reads against the dark sky.
    private static readonly Color BossBacklight = new Color(1f, 0.4f, 0.75f, 0.95f);

    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public void Begin(string path)
    {
        Done = false;
        Status = "starting";
        Time.captureFramerate = Fps;

        var video = new VideoTrackAttributes
        {
            frameRate = new MediaRational(Fps),
            width = (uint)OutWidth,
            height = (uint)OutHeight,
            includeAlpha = false,
            bitRateMode = Small ? UnityEditor.VideoBitrateMode.Low : UnityEditor.VideoBitrateMode.High,
        };
        var audio = new AudioTrackAttributes
        {
            sampleRate = new MediaRational(AudioSettings.outputSampleRate),
            channelCount = 2,
            language = "ko",
        };
        encoder = new MediaEncoder(path, video, audio);
        outRT = new RenderTexture(OutWidth, OutHeight, 0, RenderTextureFormat.ARGB32);
        frame = new Texture2D(OutWidth, OutHeight, TextureFormat.RGBA32, false);
        AudioRenderer.Start();

        stage = FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);
        font = FindFirstObjectByType<IntroCutscene>(FindObjectsInactive.Include).font;
        BuildOverlay();

        recording = true;
        StartCoroutine(CaptureLoop());
        StartCoroutine(Direct());
    }

    // ---------------- Script ----------------

    private IEnumerator Direct()
    {
        var nav = FindFirstObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
        var actions = FindFirstObjectByType<LevelSelectActions>(FindObjectsInactive.Include);
        foreach (var h in FindObjectsByType<HomePopup>(FindObjectsInactive.Include, FindObjectsSortMode.None)) h.gameObject.SetActive(false);

        // 1. Title card over the title screen.
        Status = "title";
        nav.ShowTitle();
        yield return Card("SPACE GAME", "YUMM", 5f);

        // 2. Opening cutscene (full length).
        Status = "intro";
        var intro = FindFirstObjectByType<IntroCutscene>(FindObjectsInactive.Include);
        bool introDone = false;
        intro.Play(() => introDone = true);
        StartCoroutine(Caption("오프닝 — 평화롭던 열 개의 행성에 어둠이 찾아와요", 1.5f, 6f));
        while (!introDone) yield return null;

        // 3. Chapter select.
        Status = "level select";
        nav.ShowLevelSelect();
        yield return Caption("행성 10개 = 챕터 10개. 보스를 쓰러뜨리면 다음 행성이 열려요", 0.5f, 6.5f);

        // 4. A stage: auto-firing weapon, dodging, drops, then the boss.
        Status = "stage";
        stage.bossSpawnDelay = 36f;   // a longer run of regular play before the boss (fills the two minutes)
        stage.EnterStage(DemoStage);
        yield return new WaitForSeconds(4f);
        autopilot = true;
        StartCoroutine(Caption("무기는 자동으로 발사돼요. 플레이어는 움직이며 피하기만!", 0f, 6f));
        yield return new WaitForSeconds(10f);
        StartCoroutine(Caption("몬스터를 잡으면 골드와 회복 아이템이 떨어져요", 0f, 5.5f));
        yield return new WaitForSeconds(10f);
        StartCoroutine(Caption("시간이 지날수록 몬스터가 늘고, 패턴 공격이 몰려와요", 0f, 5.5f));
        while (boss == null) { boss = FindBoss(); yield return null; }
        StartCoroutine(Caption("보스 등장! 행성을 지배한 보라색 막을 벗겨내요", 0.5f, 6f));
        while (stage.IsInStage) yield return null;
        autopilot = false;
        Status = "back to chapter select";
        yield return Caption("스테이지 클리어 — 경험치와 레벨업, 새 행성 해금", 0.3f, 5f);

        // 5. Menus.
        Status = "menus";
        actions.ShowItemShop();
        yield return Caption("상점: 무기 구매·강화, 회복 아이템", 0.3f, 4.5f);
        actions.ShowMissions();
        yield return Caption("챕터별 미션을 깨고 골드 보상 받기", 0.3f, 4.5f);
        actions.HideShop();

        // 6. Teaser: the final boss in silhouette only (no narration or captions, see LateUpdate),
        //    cut before the fight plays out.
        Status = "outro";
        var outro = FindFirstObjectByType<OutroCutscene>(FindObjectsInactive.Include);
        outro.gameObject.SetActive(true);
        silhouetteOutro = true;
        outro.Play(null);
        // Freeze the timeline just before the final blow (≈3.1s in): the boss keeps hovering in
        // silhouette, and the white flash / explosion never comes.
        yield return new WaitForSeconds(3f);
        outro.StopAllCoroutines();
        outroFrozen = true;
        yield return new WaitForSeconds(3f);

        // 7. "To Be Continued" on an opaque card; the outro is dropped underneath once covered.
        Status = "end card";
        cardImage.color = new Color(0.02f, 0.01f, 0.05f, 1f);
        cardTitle.fontSize = 120f;
        cardTitle.characterSpacing = 6f;
        cardTitle.text = "To Be Continued...";
        cardSub.text = "SPACE GAME  ·  YUMM";
        yield return FadeGroup(cardGroup, 0f, 1f, 0.8f);
        outro.StopAllCoroutines();
        var outroCanvas = GameObject.Find("OutroCanvas");
        if (outroCanvas != null) Destroy(outroCanvas);
        if (outro.bgm != null) outro.bgm.FadeOut(1f);
        while (Elapsed < TotalSeconds) yield return null;   // the video ends on the card

        Finish();
    }

    private float Elapsed => framesWritten / (float)Fps;

    private Monster FindBoss()
    {
        var list = typeof(StageProgress).GetField("activeBosses", Private).GetValue(stage) as List<Monster>;
        return list != null && list.Count > 0 ? list[0] : null;
    }

    // ---------------- Autopilot ----------------

    private void Update()
    {
        if (!recording || stage == null || !stage.IsInStage) return;

        // Never lose the demo run: keep the health bar full.
        typeof(StageProgress).GetField("playerHitCount", Private).SetValue(stage, 0);
        typeof(StageProgress).GetMethod("RefreshHealthBar", Private).Invoke(stage, null);

        if (autopilot && stage.player.activeInHierarchy)
        {
            // A lazy figure-eight around the middle of the field, eased in from wherever the ship is.
            autopilotClock += Time.deltaTime;
            var rt = (RectTransform)stage.player.transform;
            var target = new Vector2(Mathf.Sin(autopilotClock * 0.55f) * 430f, Mathf.Sin(autopilotClock * 1.1f) * 200f - 40f);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
        }

        // Keep the boss fight to a watchable length.
        if (boss != null && boss.gameObject.activeInHierarchy)
        {
            bossClock += Time.deltaTime;
            if (bossClock > 10f && !boss.invulnerable) boss.TakeDamage(100000);
        }
    }

    // After the cutscene has set this frame's colours.
    private void LateUpdate()
    {
        if (!silhouetteOutro) return;
        var canvas = GameObject.Find("OutroCanvas");
        if (canvas == null) return;

        foreach (var img in canvas.GetComponentsInChildren<Image>())
        {
            var c = img.color;
            if (SilhouetteParts.Contains(img.name)) img.color = new Color(0f, 0f, 0f, c.a);
            else if (HiddenParts.Contains(img.name)) img.color = new Color(c.r, c.g, c.b, 0f);
            else if (img.name == "BossGlow") img.color = BossBacklight;
            // Beams/flash caught mid-fade by the freeze would otherwise hang there.
            else if (outroFrozen && (img.name == "Beam" || img.name == "Flash")) img.color = new Color(c.r, c.g, c.b, 0f);
        }
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>()) text.alpha = 0f;
    }

    // ---------------- Overlay ----------------

    private void BuildOverlay()
    {
        var go = new GameObject("DemoOverlay", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        overlay = go.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 32000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(Width, Height);
        scaler.matchWidthOrHeight = 0.5f;

        // Caption: a dark pill near the top centre (cutscenes narrate at the bottom).
        var cap = NewRect("Caption", go.transform);
        cap.anchorMin = cap.anchorMax = new Vector2(0.5f, 1f);
        cap.anchoredPosition = new Vector2(0f, -150f);
        cap.sizeDelta = new Vector2(1240f, 92f);
        cap.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.03f, 0.12f, 0.72f);
        captionGroup = cap.gameObject.AddComponent<CanvasGroup>();
        captionGroup.alpha = 0f;
        captionText = NewText("Text", cap, 40f);
        Stretch(captionText.rectTransform);

        // Title / end card: full-screen fade with big lettering.
        var card = NewRect("Card", go.transform);
        Stretch(card);
        cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.03f, 0.02f, 0.08f, 0.82f);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        cardTitle = NewText("Title", card, 150f);
        cardTitle.rectTransform.sizeDelta = new Vector2(1800f, 220f);
        cardTitle.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        cardTitle.characterSpacing = 12f;
        cardTitle.color = new Color(1f, 0.93f, 0.75f);
        cardSub = NewText("Sub", card, 54f);
        cardSub.rectTransform.sizeDelta = new Vector2(1800f, 100f);
        cardSub.rectTransform.anchoredPosition = new Vector2(0f, -110f);
        cardSub.color = new Color(0.85f, 0.8f, 1f);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, float size)
    {
        var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.color = Color.white;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private IEnumerator Caption(string text, float delay, float seconds)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        captionText.text = text;
        yield return FadeGroup(captionGroup, 0f, 1f, 0.4f);
        yield return new WaitForSeconds(Mathf.Max(0f, seconds - 0.8f));
        yield return FadeGroup(captionGroup, 1f, 0f, 0.4f);
    }

    private IEnumerator Card(string title, string sub, float seconds)
    {
        cardTitle.text = title;
        cardSub.text = sub;
        yield return FadeGroup(cardGroup, 0f, 1f, 0.8f);
        yield return new WaitForSeconds(Mathf.Max(0f, seconds - 1.6f));
        yield return FadeGroup(cardGroup, 1f, 0f, 0.8f);
    }

    private static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        g.alpha = to;
    }

    // ---------------- Capture ----------------

    private IEnumerator CaptureLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (recording)
        {
            yield return endOfFrame;
            if (!recording) break;
            CaptureFrame();
        }
    }

    private void CaptureFrame()
    {
        if (screenRT == null || screenRT.width != Screen.width || screenRT.height != Screen.height)
        {
            if (screenRT != null) screenRT.Release();
            screenRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        }
        ScreenCapture.CaptureScreenshotIntoRenderTexture(screenRT);
        // The capture comes back upside down on D3D/Metal/Vulkan; flip while scaling to 1080p.
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(screenRT, outRT, new Vector2(1f, -1f), new Vector2(0f, 1f));
        else Graphics.Blit(screenRT, outRT);

        var prev = RenderTexture.active;
        RenderTexture.active = outRT;
        frame.ReadPixels(new Rect(0, 0, OutWidth, OutHeight), 0, 0, false);
        frame.Apply(false);
        RenderTexture.active = prev;
        encoder.AddFrame(frame);

        int samples = AudioRenderer.GetSampleCountForCaptureFrame();
        using (var buffer = new NativeArray<float>(samples * 2, Allocator.Temp))
        {
            AudioRenderer.Render(buffer);
            encoder.AddSamples(buffer);
        }
        framesWritten++;
    }

    private void Finish()
    {
        if (!recording) return;
        recording = false;
        AudioRenderer.Stop();
        encoder.Dispose();
        encoder = null;
        Time.captureFramerate = 0;
        Status = "done: " + framesWritten + " frames (" + Elapsed.ToString("0.0") + "s)";
        Done = true;
        Destroy(overlay.gameObject);
    }

    private void OnDestroy()
    {
        if (encoder != null) { AudioRenderer.Stop(); encoder.Dispose(); }
        Time.captureFramerate = 0;
        if (screenRT != null) screenRT.Release();
        if (outRT != null) outRT.Release();
    }
}
#endif
