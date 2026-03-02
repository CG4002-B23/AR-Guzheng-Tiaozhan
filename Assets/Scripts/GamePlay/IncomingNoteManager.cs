using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BeatmapNote
{
    public float time;
    public int @string; // 'string' is a reserved keyword in C#
    public string gesture;
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

    [Header("Spawning & Movement")]
    public float noteSpeed = 2.0f;
    public int missedNoteDamage = 5;
    public GameObject playerDamageEffectPrefab;

    private Color colorRightTuo = Color.green;
    private Color colorRightMuo = Color.blue;
    private Color colorTremolo = new Color(1.0f, 0.5f, 0.0f); // orange

    private List<BeatmapNote> upcomingNotes = new List<BeatmapNote>();
    private int currentNoteIndex = 0;
    private bool beatmapLoaded = false;

    private float internalSongTime = 0f;
    private bool isSequenceRunning = false;
    private bool waitingForLanes = false;

    public class ActiveNote
    {
        public GameObject noteObject;
        public int laneIndex;
        public Color noteColor;
        public bool isTargetedByBot;
    }

    public List<ActiveNote> activeNotes = new List<ActiveNote>();

    void Awake()
    {
        LoadBeatmap();  
    }

    private void LoadBeatmap()
    {
        if (beatmapJson == null)
        {
            Debug.LogError("No Beatmap JSON assigned to IncomingNoteManager!");
            return;
        }

        // parse json input for beatmap
        BeatmapData data = JsonUtility.FromJson<BeatmapData>(beatmapJson.text);
        upcomingNotes.Clear();

        foreach (BeatmapNote note in data.notes)
        {
            if (note.gesture == "tremolo")
            {
                // tremolo: 4 continuous notes in succession
                for (int i = 0; i < 4; i++)
                {
                    upcomingNotes.Add(new BeatmapNote 
                    { 
                        time = note.time + (i * 0.08f), 
                        @string = note.@string, 
                        gesture = "tremolo" 
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
            float songLength = AudioManager.Instance.gameplayMusic != null ? AudioManager.Instance.gameplayMusic.length : float.MaxValue;

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

        // determine color based on gesture
        Color requiredNoteColor = colorRightMuo; // default 
        if (noteData.gesture == "tuo") requiredNoteColor = colorRightTuo;
        else if (noteData.gesture == "tremolo") requiredNoteColor = colorTremolo;
        
        Renderer rend = newNoteObj.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = requiredNoteColor;

        activeNotes.Add(new ActiveNote { 
            noteObject = newNoteObj, 
            laneIndex = laneIndex,
            noteColor = requiredNoteColor,
            isTargetedByBot = false
        });
    }

    private void MoveNotes() 
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            ActiveNote note = activeNotes[i];

            if (!laneManager.LaneStarts.ContainsKey(note.laneIndex)) continue;

            Vector3 targetPosition = laneManager.LaneStarts[note.laneIndex]; 
            note.noteObject.transform.position = Vector3.MoveTowards(
                note.noteObject.transform.position, 
                targetPosition, 
                noteSpeed * Time.deltaTime
            );

            if (Vector3.Distance(note.noteObject.transform.position, targetPosition) < 0.05f)
            {
                if (healthManager != null)
                    healthManager.DamagePlayer(missedNoteDamage);

                if (playerDamageEffectPrefab != null)
                    Instantiate(playerDamageEffectPrefab, targetPosition, Quaternion.identity);

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
                sphereSpawner.ReturnSphere(note.noteObject);

            activeNotes.Clear();
            currentNoteIndex = 0; 
            isSequenceRunning = false;
            waitingForLanes = false;
        }
        else if (isNowActive && beatmapLoaded && currentNoteIndex == 0)
            waitingForLanes = true; // starting new game, wait for AR lanes to generate
    }

    public void DestroyNoteFromBot(ActiveNote note)
    {
        if (activeNotes.Contains(note))
        {
            sphereSpawner.ReturnSphere(note.noteObject);
            activeNotes.Remove(note);
        }
    }
}