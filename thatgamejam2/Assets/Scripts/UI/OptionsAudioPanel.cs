using UnityEngine;
using UnityEngine.UI;

public class OptionsAudioPanel : MonoBehaviour
{
    [SerializeField] Toggle muteAllToggle;
    [SerializeField] Slider bgmSlider;
    [SerializeField] Slider sfxSlider;

    void OnEnable()
    {
        var audio = AudioManager.Instance;
        if (audio == null) return;

        if (muteAllToggle != null) muteAllToggle.SetIsOnWithoutNotify(audio.MuteAll);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(audio.BgmVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audio.SfxVolume);

        if (muteAllToggle != null) muteAllToggle.onValueChanged.AddListener(OnMuteAllChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    void OnDisable()
    {
        if (muteAllToggle != null) muteAllToggle.onValueChanged.RemoveListener(OnMuteAllChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
    }

    void OnMuteAllChanged(bool mute) => AudioManager.Instance.SetMuteAll(mute);
    void OnBgmChanged(float value) => AudioManager.Instance.SetBgmVolume(value);
    void OnSfxChanged(float value) => AudioManager.Instance.SetSfxVolume(value);
}
