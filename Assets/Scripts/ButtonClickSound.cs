using UnityEngine;
using UnityEngine.UI;

// Gives every Button in the scene (inactive ones included) a click sound, so buttons added to the
// scene later get it without any per-button setup. Each press plays the next clip in order, looping.
public class ButtonClickSound : MonoBehaviour
{
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 0.7f;

    private AudioSource source;
    private int next;
    private static int skipFrame = -1;

    // For button actions that play their own sound (weapon equip, upgrade...): the button's own
    // onClick listeners run before this one, so calling this from them silences this press's click.
    public static void SkipThisClick() => skipFrame = Time.frameCount;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;

        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            button.onClick.AddListener(Play);
    }

    public void Play()
    {
        if (clips == null || clips.Length == 0) return;
        if (skipFrame == Time.frameCount)
        {
            skipFrame = -1;
            return;
        }

        var clip = clips[next];
        next = (next + 1) % clips.Length;
        if (clip != null) source.PlayOneShot(clip, volume * VolumeSettings.SfxGain);
    }
}
