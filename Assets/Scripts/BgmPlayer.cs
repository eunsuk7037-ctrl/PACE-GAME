using System.Collections;
using UnityEngine;

// Single BGM player that lives on an always-active object, so switching screens can crossfade
// between tracks instead of cutting off when a screen is disabled. Menu tracks run to the end,
// rest for `gapSeconds`, then start again; stage tracks repeat after `stageGapSeconds`.
// Stage and boss tracks remember where they were cut off, so switching back and forth between
// them (boss appears, boss dies, next boss appears...) always continues instead of restarting.
public class BgmPlayer : MonoBehaviour
{
    public AudioClip titleClip;
    public AudioClip levelSelectClip;
    [Tooltip("Battle music per stage, index = stage index (0=MERCURY .. 9=SUN). Empty slots keep the level select music.")]
    public AudioClip[] stageClips = new AudioClip[10];
    [Tooltip("Boss music per stage, index = stage index. Empty slots keep the stage music going.")]
    public AudioClip[] bossClips = new AudioClip[10];
    [Range(0f, 1f)] public float volume = 0.5f;
    public float gapSeconds = 10f;
    public float stageGapSeconds = 0f;
    public float fadeSeconds = 1.5f;
    [Tooltip("Fade used when the boss track comes back for the next boss (it continues from where it was cut off).")]
    public float bossReturnFadeSeconds = 3f;

    private readonly AudioSource[] sources = new AudioSource[2];
    private int current;
    private Coroutine repeatRoutine;
    private Coroutine fadeRoutine;

    // Where the stage/boss tracks were when the other one took over.
    private float stageResumeTime, bossResumeTime;

    // Final level of the playing track: this player's mix level scaled by the Music setting.
    private float TargetVolume => volume * VolumeSettings.MusicGain;

    private void Awake()
    {
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].loop = false;
            sources[i].volume = 0f;
        }
        VolumeSettings.Changed += OnVolumeChanged;
    }

    private void OnDestroy()
    {
        VolumeSettings.Changed -= OnVolumeChanged;
    }

    // A running crossfade already reads TargetVolume every frame; otherwise apply it directly.
    private void OnVolumeChanged()
    {
        if (fadeRoutine == null) sources[current].volume = TargetVolume;
    }

    public void PlayTitle() => Play(titleClip, gapSeconds);
    public void PlayClip(AudioClip clip) => Play(clip, gapSeconds);   // one-off tracks, e.g. the intro cutscene
    public void PlayLevelSelect() => Play(levelSelectClip, gapSeconds);

    // Entering a stage starts its track from the top.
    public void PlayStage(int stageIndex)
    {
        stageResumeTime = bossResumeTime = 0f;
        var clip = ClipAt(stageClips, stageIndex);
        if (clip != null) Play(clip, stageGapSeconds);
        else PlayLevelSelect();
    }

    // A boss just died: straight back to the stage track, continuing where the boss cut it off.
    public void ReturnToStage(int stageIndex)
    {
        var clip = ClipAt(stageClips, stageIndex);
        if (clip == null) return;   // no stage track: whatever is playing just carries on

        if (sources[current].clip == ClipAt(bossClips, stageIndex)) bossResumeTime = sources[current].time;
        Play(clip, stageGapSeconds, stageResumeTime);
    }

    // A boss appears: the boss track takes over, continuing from where the last boss left it.
    public void PlayBoss(int stageIndex)
    {
        var clip = ClipAt(bossClips, stageIndex);
        if (clip == null || sources[current].clip == clip) return;

        if (sources[current].clip == ClipAt(stageClips, stageIndex)) stageResumeTime = sources[current].time;
        Play(clip, stageGapSeconds, bossResumeTime, bossResumeTime > 0f ? bossReturnFadeSeconds : fadeSeconds);
    }

    // Silences the music (e.g. under the stage-clear fanfare); the next Play* call fades in from silence.
    public void FadeOut(float seconds)
    {
        if (repeatRoutine != null) StopCoroutine(repeatRoutine);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        repeatRoutine = null;
        fadeRoutine = StartCoroutine(FadeAllOut(seconds));
    }

    private IEnumerator FadeAllOut(float seconds)
    {
        // Both sources: a crossfade may have been mid-way when this was called.
        float v0 = sources[0].volume, v1 = sources[1].volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / seconds;
            sources[0].volume = v0 * k;
            sources[1].volume = v1 * k;
            yield return null;
        }
        foreach (var s in sources)
        {
            s.volume = 0f;
            s.Stop();
            s.clip = null;
        }
        fadeRoutine = null;
    }

    private static AudioClip ClipAt(AudioClip[] clips, int index) =>
        clips != null && index >= 0 && index < clips.Length ? clips[index] : null;

    private void Play(AudioClip clip, float gap, float startTime = 0f, float fade = -1f)
    {
        if (clip == null || sources[current].clip == clip) return;

        var from = sources[current];
        current = 1 - current;
        var to = sources[current];

        if (repeatRoutine != null) StopCoroutine(repeatRoutine);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        to.Stop();
        to.clip = clip;
        to.volume = 0f;
        repeatRoutine = StartCoroutine(Repeat(to, gap, startTime < clip.length ? startTime : 0f));
        fadeRoutine = StartCoroutine(Crossfade(from, to, fade >= 0f ? fade : fadeSeconds));
    }

    private IEnumerator Crossfade(AudioSource from, AudioSource to, float seconds)
    {
        float fromStart = from.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            from.volume = fromStart * (1f - k);
            to.volume = TargetVolume * k;
            yield return null;
        }
        from.volume = 0f;
        from.Stop();
        from.clip = null;
        to.volume = TargetVolume;
        fadeRoutine = null;
    }

    // First play starts at `startTime` (set after Play(), which rewinds); repeats start from the top.
    private IEnumerator Repeat(AudioSource source, float gapSeconds, float startTime)
    {
        var gap = new WaitForSecondsRealtime(gapSeconds);
        while (true)
        {
            source.Play();
            if (startTime > 0f)
            {
                source.time = startTime;
                startTime = 0f;
            }
            // Playback can start a moment after Play(); wait for it to really begin before
            // watching for the end, or a slow start would look like the track finishing.
            float requestedAt = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => source.isPlaying || Time.realtimeSinceStartup - requestedAt > 5f);
            yield return new WaitWhile(() => source.isPlaying);
            if (gapSeconds > 0f) yield return gap;
        }
    }
}
