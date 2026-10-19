using UnityEngine;
using UnityEngine.UI;

// Binds a settings Slider to one volume channel: shows the saved level and saves changes.
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    public VolumeSettings.Channel channel;

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, VolumeSettings.GetLevel(channel)));
        slider.onValueChanged.AddListener(OnChanged);
    }

    private void OnChanged(float value)
    {
        VolumeSettings.SetLevel(channel, Mathf.InverseLerp(slider.minValue, slider.maxValue, value));
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }
}
