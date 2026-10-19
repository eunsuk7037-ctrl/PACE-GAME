using System;
using UnityEngine;

// Device-wide volume settings (plain PlayerPrefs, like language): "Sound" scales every sound
// effect, "Music" scales the background music. Levels are slider positions in 0..1; the gains
// players hear follow a squared curve so each slider step sounds like a similar change.
public static class VolumeSettings
{
    public enum Channel { Sfx, Music }

    private const string SfxKey = "Settings_SfxVolume";
    private const string MusicKey = "Settings_MusicVolume";

    // Cached because some sounds read their gain every frame.
    private static float? sfxLevel, musicLevel;

    public static event Action Changed;

    public static float GetLevel(Channel channel)
    {
        if (channel == Channel.Sfx) return sfxLevel ??= PlayerPrefs.GetFloat(SfxKey, 1f);
        return musicLevel ??= PlayerPrefs.GetFloat(MusicKey, 1f);
    }

    public static void SetLevel(Channel channel, float level)
    {
        level = Mathf.Clamp01(level);
        if (channel == Channel.Sfx) { sfxLevel = level; PlayerPrefs.SetFloat(SfxKey, level); }
        else { musicLevel = level; PlayerPrefs.SetFloat(MusicKey, level); }
        Changed?.Invoke();
    }

    public static float SfxGain => Gain(Channel.Sfx);
    public static float MusicGain => Gain(Channel.Music);

    private static float Gain(Channel channel)
    {
        float level = GetLevel(channel);
        return level * level;
    }
}
