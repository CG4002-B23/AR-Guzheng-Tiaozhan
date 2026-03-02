using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; } // singleton
    private AudioSource audioSource;

    [Tooltip("Assign some music here to play in the menus")]
    public AudioClip menuMusic;
    [Tooltip("Assign some music here to play in the game")]
    public AudioClip gameplayMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false; 
    }

    private void OnEnable()
    {
        StateManager.OnGameStateChanged += HandleGameStateChange;
        
        if (StateManager.Instance != null)
            HandleGameStateChange(StateManager.Instance.CurrentState);
    }

    private void OnDisable()
    {
        StateManager.OnGameStateChanged -= HandleGameStateChange;
    }

    private void HandleGameStateChange(StateManager.GameState newState)
    {
        if (audioSource == null) return;

        if (newState == StateManager.GameState.Playing)
        {
            if (audioSource.clip != gameplayMusic)
            {
                // switch to gameplay music
                audioSource.clip = gameplayMusic;
                audioSource.time = 0f; // start from beginning
                audioSource.Play();
            }
            else if (!audioSource.isPlaying)
                audioSource.Play(); // resume from pause
        }
        else if (newState == StateManager.GameState.Paused)
        {
            if (audioSource.isPlaying)
                audioSource.Pause();
        }
        else if (newState == StateManager.GameState.StartMenu ||
                 newState == StateManager.GameState.Victory ||
                 newState == StateManager.GameState.Defeat)
        {
            if (audioSource.clip != menuMusic)
            {
                audioSource.clip = menuMusic;
                audioSource.Play();
            }
        }
        else
        {
            audioSource.Stop();
        }
    }

    public float GetPlaybackTime()
    {
        return audioSource != null ? audioSource.time : 0f;
    }

    public bool IsPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }
}