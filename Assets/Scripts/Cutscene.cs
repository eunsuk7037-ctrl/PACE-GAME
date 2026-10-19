using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shared frame for the intro and outro cutscenes: its own overlay canvas with a night sky and
// twinkling stars, letterbox bars, narration lines, a skip hint and a black fader.
// Subclasses add their own layers in BuildScene() and play them out in Run().
// Any key / click / tap skips to the end.
public abstract class Cutscene : MonoBehaviour
{
    [Header("Art")]
    public Sprite backgroundSprite;
    [Tooltip("Mercury .. Sun, in order.")]
    public Sprite[] planetSprites = new Sprite[10];
    public Sprite glowSprite;
    public Sprite sparkleSprite;
    public TMP_FontAsset font;

    [Header("Sound")]
    public BgmPlayer bgm;
    public AudioClip music;

    protected const float W = 1920f, H = 1080f;

    protected static readonly string[] PlanetNamesEn = { "MERCURY", "VENUS", "EARTH", "MARS", "JUPITER", "SATURN", "URANUS", "NEPTUNE", "PLUTO", "SUN" };
    protected static readonly string[] PlanetNamesKo = { "수성", "금성", "지구", "화성", "목성", "토성", "천왕성", "해왕성", "명왕성", "태양" };
    protected static readonly float[] PlanetSizes = { 330, 380, 400, 360, 470, 450, 400, 400, 320, 540 };
    protected static readonly Color[] PlanetGlow =
    {
        new Color(0.85f, 0.80f, 0.75f), new Color(1f, 0.75f, 0.45f), new Color(0.45f, 0.85f, 1f), new Color(1f, 0.50f, 0.40f),
        new Color(1f, 0.80f, 0.60f), new Color(1f, 0.90f, 0.55f), new Color(0.55f, 1f, 0.95f), new Color(0.45f, 0.60f, 1f),
        new Color(0.90f, 0.85f, 1f), new Color(1f, 0.85f, 0.35f),
    };
    protected static readonly Color Dominated = new Color(0.62f, 0.45f, 0.85f);

    protected bool skipping;
    protected float clock;   // seconds since the cutscene started (drives the idle motion)

    protected RectTransform root, world, starLayer;
    protected Image background, fader, barTop, barBottom;
    protected TextMeshProUGUI narration, skipHint;

    private Action onFinished;
    private readonly List<Image> stars = new List<Image>();
    private readonly List<float> starPhase = new List<float>();

    protected static string L(string en, string ko) => LocalizationManager.IsKorean ? ko : en;

    // Frame step, capped: the first frames after a load can stall for seconds, and an uncapped
    // step would skip a whole fade in one jump. While a fixed-rate capture runs (video recording)
    // it follows the capture clock instead, so cutscenes keep their real length in the video.
    protected static float Dt => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.05f);

    public bool IsPlaying => root != null;

    public void Play(Action finished)
    {
        if (IsPlaying) return;

        // Fresh state, so it can be replayed.
        enabled = true;
        skipping = false;
        clock = 0f;
        stars.Clear();
        starPhase.Clear();
        ResetScene();

        onFinished = finished;
        Build();
        StartCoroutine(Run());
    }

    protected abstract IEnumerator Run();
    protected abstract void BuildScene();
    protected virtual void ResetScene() { }
    protected virtual void Tick() { }   // per-frame idle motion of the subclass
    protected virtual string CanvasName => GetType().Name + "Canvas";

    private void Update()
    {
        if (root == null) return;
        clock += Dt;

        for (int i = 0; i < stars.Count; i++)
        {
            var c = stars[i].color;
            c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(clock * (1.2f + (i % 5) * 0.35f) + starPhase[i]));
            stars[i].color = c;
        }

        Tick();

        if (!skipping && clock > 0.4f && AnyInput())
        {
            skipping = true;
            StopAllCoroutines();   // drop the timeline; only the fade-out runs from here
            StartCoroutine(Skip());
        }
    }

    // ---------------- Shared beats ----------------

    protected IEnumerator Opening()
    {
        StartCoroutine(Letterbox(true, 0.9f));
        StartCoroutine(Fade(fader, 1f, 0f, 1.6f));
        StartCoroutine(ShowSkipHint());
        yield break;
    }

    protected IEnumerator Narrate(string text, float seconds, Color? color = null)
    {
        narration.text = text;
        narration.color = color ?? Color.white;
        var rt = narration.rectTransform;
        const float fadeIn = 0.9f, fadeOut = 0.7f;
        for (float t = 0f; t < seconds && !skipping; t += Dt)
        {
            float a = t < fadeIn ? t / fadeIn : (t > seconds - fadeOut ? (seconds - t) / fadeOut : 1f);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeIn));
            narration.alpha = Mathf.Clamp01(a);
            narration.characterSpacing = Mathf.Lerp(14f, 2f, rise);
            rt.anchoredPosition = new Vector2(0f, -360f + 18f * rise);
            yield return null;
        }
        narration.alpha = 0f;
    }

    private IEnumerator ShowSkipHint()
    {
        yield return Wait(1.5f);
        for (float t = 0f; t < 0.8f && !skipping; t += Dt)
        {
            skipHint.alpha = t / 0.8f * 0.55f;
            yield return null;
        }
    }

    // The intro hands over to the title track, so a skip fades its own music out first.
    protected virtual bool FadeMusicOnSkip => true;

    private IEnumerator Skip()
    {
        if (FadeMusicOnSkip && bgm != null) bgm.FadeOut(0.5f);
        yield return Finish(0.5f);
    }

    // Fades to black, hands over to whatever comes next, then fades the black away over it.
    protected IEnumerator Finish(float seconds)
    {
        StartCoroutine(Letterbox(false, seconds));
        skipHint.alpha = 0f;
        yield return Fade(fader, fader.color.a, 1f, seconds);

        var finished = onFinished;
        onFinished = null;
        finished?.Invoke();
        world.gameObject.SetActive(false);
        narration.gameObject.SetActive(false);
        yield return Fade(fader, 1f, 0f, 0.8f);
        Destroy(root.gameObject);
        root = null;
        enabled = false;
    }

    // ---------------- Small helpers ----------------

    protected IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt) yield return null;
    }

    protected IEnumerator Fade(Image img, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt)
        {
            var c = img.color;
            c.a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            img.color = c;
            yield return null;
        }
        var end = img.color;
        end.a = to;
        img.color = end;
    }

    protected IEnumerator Letterbox(bool show, float seconds)
    {
        const float bar = 95f;
        for (float t = 0f; t <= seconds; t += Dt)
        {
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
            float h = show ? bar * p : bar * (1f - p);
            barTop.rectTransform.sizeDelta = new Vector2(0f, h);
            barBottom.rectTransform.sizeDelta = new Vector2(0f, h);
            yield return null;
        }
    }

    private static bool AnyInput()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }

    // ---------------- Building ----------------

    private void Build()
    {
        var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // above every game canvas
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(W, H);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        // Solid backdrop so nothing underneath shows through, and it eats clicks meant for it.
        var backdrop = CreateImage("Backdrop", root, null, new Color(0.04f, 0.02f, 0.08f), Vector2.zero);
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        world = CreateRect("World", root);
        Stretch(world);

        // Oversized so a parallax drift or a screen shake never shows its edge.
        background = CreateImage("Sky", world, backgroundSprite, Color.white, new Vector2(W, H) * 1.25f);
        StartCoroutine(SlowZoom(background.rectTransform));

        starLayer = CreateRect("Stars", world);
        for (int i = 0; i < 130; i++)
        {
            float size = UnityEngine.Random.value < 0.15f ? UnityEngine.Random.Range(22f, 34f) : UnityEngine.Random.Range(6f, 16f);
            var tint = i % 3 == 0 ? new Color(1f, 0.95f, 0.8f) : (i % 3 == 1 ? new Color(0.8f, 0.9f, 1f) : Color.white);
            var s = CreateImage("Star", starLayer, sparkleSprite, tint, new Vector2(size, size));
            s.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-W * 0.5f, W * 1.6f), UnityEngine.Random.Range(-H * 0.5f, H * 0.5f));
            stars.Add(s);
            starPhase.Add(UnityEngine.Random.Range(0f, Mathf.PI * 2f));
        }

        BuildScene();

        narration = CreateText("Narration", root, "", 64f, Color.white);
        narration.rectTransform.sizeDelta = new Vector2(1700f, 200f);   // room for a two-line caption
        narration.lineSpacing = -8f;
        narration.enableAutoSizing = true;   // long lines shrink to fit instead of running off screen
        narration.fontSizeMin = 40f;
        narration.fontSizeMax = 64f;
        narration.alpha = 0f;
        narration.outlineWidth = 0.18f;
        narration.outlineColor = new Color32(30, 12, 48, 255);

        barTop = CreateImage("BarTop", root, null, Color.black, Vector2.zero);
        barBottom = CreateImage("BarBottom", root, null, Color.black, Vector2.zero);
        SetBar(barTop.rectTransform, top: true);
        SetBar(barBottom.rectTransform, top: false);

        skipHint = CreateText("SkipHint", root, L("Tap to skip", "터치하여 건너뛰기"), 30f, Color.white);
        skipHint.alignment = TextAlignmentOptions.BottomRight;
        var shrt = skipHint.rectTransform;
        shrt.anchorMin = shrt.anchorMax = shrt.pivot = new Vector2(1f, 0f);
        shrt.anchoredPosition = new Vector2(-40f, 28f);
        shrt.sizeDelta = new Vector2(500f, 50f);
        skipHint.alpha = 0f;

        fader = CreateImage("Fader", root, null, Color.black, Vector2.zero);
        Stretch(fader.rectTransform);
    }

    private IEnumerator SlowZoom(RectTransform rt)
    {
        while (rt != null)
        {
            float s = 1f + 0.04f * Mathf.Sin(clock * 0.15f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    private static void SetBar(RectTransform rt, bool top)
    {
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    protected static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    protected static RectTransform CreateRect(string name, Transform parent)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    protected static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    protected TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 90f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }
}
