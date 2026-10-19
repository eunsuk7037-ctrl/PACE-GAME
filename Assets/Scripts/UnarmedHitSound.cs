using UnityEngine;

// Hit sound for the bare-handed attack (tapping/holding a monster with no weapon equipped).
// Lives on an always-active object; monsters call the static Play().
public class UnarmedHitSound : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.21f;

    private static UnarmedHitSound instance;
    private AudioSource source;

    private void Awake()
    {
        instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
    }

    public static void Play()
    {
        if (instance != null && instance.clip != null)
            instance.source.PlayOneShot(instance.clip, instance.volume * VolumeSettings.SfxGain);
    }
}
