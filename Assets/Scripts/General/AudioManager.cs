using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : StateListener
{
    private AudioSource audioSource;

    [Tooltip("Assign some music here to play in the menus")]
    public AudioClip menuMusic;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = menuMusic;
        audioSource.loop = true;
        audioSource.playOnAwake = false; 
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        base.OnStateToggled(isNowActive);

        if (audioSource == null || audioSource.clip == null) return;

        if (isNowActive)
        {
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Stop(); // use Pause() to resume playing it the next time it's played
        }
    }
}