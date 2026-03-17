using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BeatmapNote
{
    public float time;
    public int @string; // 'string' is a reserved keyword in C#
    public string gesture;
    public float duration;
    public string vibrato;
}

[System.Serializable]
public class BeatmapData
{
    public List<BeatmapNote> notes;
}

public class IncomingNoteManager : StateListener
{
    [Header("References")]
    public SphereSpawner sphereSpawner;
    public LaneManager laneManager;
    public HealthManager healthManager;
    public GameManager gameManager;
    
    [Header("Beatmap & Audio Settings")]
    [Tooltip("Drop generated beatmap json here")]
    public TextAsset beatmapJson; 
    [Tooltip("Drop the TUTORIAL beatmap json here")]
    public TextAsset tutorialBeatmapJson;

    [Header("Spawning & Movement")]
    public float noteSpeed = 2.0f;
    public int missedNoteDamage = 5;
    public GameObject playerDamageEffectPrefab;

    public Color colorThumb = Color.green;
    public Color colorIndex = Color.blue;
    public Color colorMiddle = Color.red;
    public Color colorRing = Color.yellow;
    public Color colorPinky = Color.magenta;
    public Color colorMute = Color.gray;
    public Color colorTremolo = new Color(1.0f, 0.5f, 0.0f); // orange

    [Header("Vibrato Visuals")]
    public GameObject lightVibratoPrefab;
    public GameObject heavyVibratoPrefab;

    private List<BeatmapNote> upcomingNotes = new List<BeatmapNote>();
    private int currentNoteIndex = 0;
    private bool beatmapLoaded = false;

    private float internalSongTime = 0f;
    public float CurrentSongTime => internalSongTime;
    private bool isSequenceRunning = false;
    private bool waitingForLanes = false;

    public class ActiveNote
    {
        public GameObject noteObject;
        public int laneIndex;
        public Color noteColor;
        public bool isTargetedByBot;
        public float hitTime;
        public GameObject vibratoEffectObj;
    }

    public List<ActiveNote> activeNotes = new List<ActiveNote>();

    protected override void OnEnable()
    {
        base.OnEnable();
        StateManager.OnGameStateChanged += PrepareBeatmapOnGameStart;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StateManager.OnGameStateChanged -= PrepareBeatmapOnGameStart;
    }

    private void PrepareBeatmapOnGameStart(StateManager.GameState newState)
    {
        // transitioning out of the start menu: set the beatmap 
        if (StateManager.Instance != null && 
            StateManager.Instance.PreviousState == StateManager.GameState.StartMenu && 
            newState != StateManager.GameState.StartMenu)
        {
            LoadBeatmap();
            internalSongTime = 0f;
        }
    }

    private void LoadBeatmap()
    {
        // determine beatmap file to use based on game mode
        TextAsset targetJson = beatmapJson;
        if (StateManager.Instance != null && StateManager.Instance.isTutorialMode && tutorialBeatmapJson != null)
            targetJson = tutorialBeatmapJson;

        if (targetJson == null)
        {
            Debug.LogError("No Beatmap JSON assigned to IncomingNoteManager!");
            return;
        }

        // Parse json input for beatmap using the targetJson
        BeatmapData data = JsonUtility.FromJson<BeatmapData>(targetJson.text);
        upcomingNotes.Clear();

        foreach (BeatmapNote note in data.notes)
        {
            if (note.gesture == "tremolo")
            {
                // how many  tremolo notes to include within the duration of the tremolo
                int tremoloCount = Mathf.Max(1, Mathf.RoundToInt(note.duration / 0.08f));
                for (int i = 0; i < tremoloCount; i++)
                {
                    upcomingNotes.Add(new BeatmapNote 
                    { 
                        time = note.time + (i * 0.08f), 
                        @string = note.@string, 
                        gesture = "tremolo",
                        duration = 0.075f, 
                        vibrato = note.vibrato
                    });
                }
            }
            else
            {
                upcomingNotes.Add(note);
            }
        }

        // resort in case the injected tremolo notes got out of order
        upcomingNotes.Sort((a, b) => a.time.CompareTo(b.time));
        currentNoteIndex = 0;
        beatmapLoaded = true;
    }

    void Update()
    {
        if (!isActiveState) return;
        if (StateManager.Instance.IsTutorialPaused) return;
        if (StateManager.Instance.CurrentState == StateManager.GameState.Paused) return;
        if (!beatmapLoaded) return;

        if (waitingForLanes)
        {
            if (laneManager.LaneEnds.ContainsKey(0) && laneManager.LaneStarts.ContainsKey(0))
            {
                float travelDistance = Vector3.Distance(laneManager.LaneEnds[0], laneManager.LaneStarts[0]);
                float timeToReachPlayer = travelDistance / noteSpeed;
                
                internalSongTime = -timeToReachPlayer; // ensure the music starts at the right time, so the first note hits correctly
                
                waitingForLanes = false;
                isSequenceRunning = true;
            }
            return; // Don't do anything else until lanes are ready
        }

        if (!isSequenceRunning) return;

        internalSongTime += Time.deltaTime; // start music once value is positive

        if (AudioManager.Instance != null)
        {
            AudioClip activeTrack = AudioManager.Instance.GetActiveGameplayTrack();
            float songLength = activeTrack != null ? activeTrack.length : float.MaxValue;

            if (internalSongTime >= 0f && !AudioManager.Instance.IsPlaying() && internalSongTime < 1.0f) // 1.0f ensure that song doesn't restart at end of song
                AudioManager.Instance.PlayGameplayMusic();

            // force synchronisation when playing the game
            if (internalSongTime > 0f && AudioManager.Instance.IsPlaying())
                internalSongTime = AudioManager.Instance.GetPlaybackTime();

            if (internalSongTime >= songLength) 
            {
                isSequenceRunning = false;
                gameManager.HandleGameOver(true);
                return; 
            }
        }

        HandleSpawning(internalSongTime); 
        MoveNotes();
    }

    private void HandleSpawning(float currentSongTime)
    {
        while (currentNoteIndex < upcomingNotes.Count)
        {
            BeatmapNote nextNote = upcomingNotes[currentNoteIndex];
            int laneIndex = nextNote.@string - 1; 

            if (!laneManager.LaneEnds.ContainsKey(laneIndex) || !laneManager.LaneStarts.ContainsKey(laneIndex))
            {
                currentNoteIndex++;
                continue;
            }

            // compute how long it takes for the note to travel down the lane
            float travelDistance = Vector3.Distance(laneManager.LaneEnds[laneIndex], laneManager.LaneStarts[laneIndex]);
            float timeToReachPlayer = travelDistance / noteSpeed;

            // spawn the note at the correct time at the enemy side so by the time it reaches the guzheng, the timing is right
            if (currentSongTime >= (nextNote.time - timeToReachPlayer))
            {
                SpawnNoteFromData(nextNote, laneIndex);
                currentNoteIndex++;
            }
            else
            {
                // not time for this note yet, check again next frame
                break; 
            }
        }
    }

    private void SpawnNoteFromData(BeatmapNote noteData, int laneIndex)
    {
        GameObject newNoteObj = sphereSpawner.GetSphere();
        newNoteObj.transform.position = laneManager.LaneEnds[laneIndex];

        Color requiredNoteColor = colorIndex; // default to index
        switch (noteData.gesture)
        {
            case "thumb": requiredNoteColor = colorThumb; break;
            case "index": requiredNoteColor = colorIndex; break;
            case "middle": requiredNoteColor = colorMiddle; break;
            case "ring": requiredNoteColor = colorRing; break;
            case "pinky": requiredNoteColor = colorPinky; break;
            case "mute": requiredNoteColor = colorMute; break;
            case "tremolo": requiredNoteColor = colorTremolo; break;
        }
        
        Renderer rend = newNoteObj.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = requiredNoteColor;

        GameObject vibratoObj = null;
        if (noteData.vibrato == "light" && lightVibratoPrefab != null)
            vibratoObj = Instantiate(lightVibratoPrefab, newNoteObj.transform);
        else if (noteData.vibrato == "heavy" && heavyVibratoPrefab != null)
            vibratoObj = Instantiate(heavyVibratoPrefab, newNoteObj.transform);

        if (vibratoObj != null)
        {
            vibratoObj.transform.localPosition = Vector3.zero; // centre on the note
            Renderer vibratoRend = vibratoObj.GetComponent<Renderer>();
            if (vibratoRend != null)
            {
                vibratoRend.material.color = requiredNoteColor;
                float glowIntensity = 2.5f; 
                vibratoRend.material.SetColor("_EmissionColor", requiredNoteColor * glowIntensity);
            }
        }

        activeNotes.Add(new ActiveNote { 
            noteObject = newNoteObj, 
            laneIndex = laneIndex,
            noteColor = requiredNoteColor,
            isTargetedByBot = false,
            hitTime = noteData.time,
            vibratoEffectObj = vibratoObj
        });
    }

    private void MoveNotes() 
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            ActiveNote note = activeNotes[i];

            if (!laneManager.LaneStarts.ContainsKey(note.laneIndex) || !laneManager.LaneEnds.ContainsKey(note.laneIndex)) continue;

            Vector3 targetPosition = laneManager.LaneStarts[note.laneIndex]; 
            Vector3 startPosition = laneManager.LaneEnds[note.laneIndex];

            float timeRemainingToNoteHit = note.hitTime - internalSongTime;

            // calc exact position that the note should be on the lane
            Vector3 directionToStart = (startPosition - targetPosition).normalized;
            float distanceFromTarget = timeRemainingToNoteHit * noteSpeed;
            note.noteObject.transform.position = targetPosition + (directionToStart * distanceFromTarget);

            if (timeRemainingToNoteHit <= 0f) // note has reached player
            {
                if (healthManager != null)
                    healthManager.DamagePlayer(missedNoteDamage);

                if (playerDamageEffectPrefab != null)
                    Instantiate(playerDamageEffectPrefab, targetPosition, Quaternion.identity);

                if (note.vibratoEffectObj != null) Destroy(note.vibratoEffectObj);

                sphereSpawner.ReturnSphere(note.noteObject);
                activeNotes.RemoveAt(i);
            }
        }
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (!isNowActive && StateManager.Instance.CurrentState != StateManager.GameState.Paused)
        {
            // Only clear notes and reset sequence if we are leaving the play state entirely (not just pausing)
            foreach (var note in activeNotes)
            {
                if (note.vibratoEffectObj != null) Destroy(note.vibratoEffectObj);
                sphereSpawner.ReturnSphere(note.noteObject);
            }

            activeNotes.Clear();
            currentNoteIndex = 0; 
            isSequenceRunning = false;
            waitingForLanes = false;
        }
        else if (isNowActive && beatmapLoaded && currentNoteIndex == 0)
            waitingForLanes = true; // starting new game, wait for AR lanes to generate
        else if (StateManager.Instance.CurrentState == StateManager.GameState.StartMenu)
            beatmapLoaded = false;
    }

    public void DestroyNoteFromBot(ActiveNote note)
    {
        if (activeNotes.Contains(note))
        {
            if (note.vibratoEffectObj != null) Destroy(note.vibratoEffectObj);
            sphereSpawner.ReturnSphere(note.noteObject);
            activeNotes.Remove(note);
        }
    }
}