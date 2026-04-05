using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatManager : StateListener
{
    [Header("References")]
    public IncomingNoteManager enemyNoteManager;
    public SphereSpawner playerSphereSpawner; 
    public LaneManager laneManager;
    public HealthManager healthManager;
    public ScoreManager scoreManager;
    public MockGestureProvider gestureProvider; // for testing standalone
    // public GestureProvider gestureProvider;
    public GameObject collisionSparkPrefab;
    public GameObject enemyDamageEffectPrefab;

    [Header("Guzheng Strings (Auto-Assigned at Runtime)")]
    [HideInInspector] public List<GuzhengStringInteraction> guzhengStrings;

    [Header("Combat Settings")]
    public float playerNoteSpeed = 10.0f;
    public int attackDamage = 10;
    public float collisionDistanceThreshold = 0.2f;

    [Header("Gesture Colors (Must match IncomingNoteManager)")]
    public Color colorThumb = Color.green;
    public Color colorIndex = Color.blue;
    public Color colorMiddle = Color.red;
    public Color colorRing = Color.yellow;
    public Color colorPinky = Color.magenta;
    public Color colorMute = Color.gray;
    public Color colorTremolo = new Color(1.0f, 0.5f, 0.0f); // orange

    private class PlayerNote
    {
        public GameObject noteObject;
        public int laneIndex;
        public Color noteColor;
    }

    private List<PlayerNote> activePlayerNotes = new List<PlayerNote>();

    protected override void OnEnable()
    {
        base.OnEnable();
        if (gestureProvider != null)
            gestureProvider.OnGestureReceived += HandleGesture;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (gestureProvider != null)
            gestureProvider.OnGestureReceived -= HandleGesture;

        foreach (var note in activePlayerNotes)
        {
            if (note.noteObject != null)
                playerSphereSpawner.ReturnSphere(note.noteObject); // clean up notes immediately when script is disabled
        }
        activePlayerNotes.Clear();
    }

    private bool IsValidCombatGesture(string gesture)
    {
        switch (gesture)
        {
            case "Tuo":
            case "Index":
            case "Middle":
            case "Ring":
            case "Pinky":
            case "Mute":
            case "YaoZhi":
                return true;
            default:
                return false;
        }
    }

    private void HandleGesture(HandType targetHand, string gesture)
    {
        if (!isActiveState) return;
        if (StateManager.Instance != null && StateManager.Instance.IsTutorialPaused) return;
        if (!gestureProvider.gestureNames.Contains(gesture)) return;
        if (gesture == "Idle") return;
        if (!IsValidCombatGesture(gesture)) return;

        for (int i = 0; i < guzhengStrings.Count; i++)
        {
            if (guzhengStrings[i] != null && guzhengStrings[i].IsTouched) // touching the string and making gesture
                FireNote(i, gesture);
        }
    }

    private void FireNote(int laneIndex, string gesture)
    {
        if (!laneManager.LaneStarts.ContainsKey(laneIndex) || !laneManager.LaneEnds.ContainsKey(laneIndex)) return;

        GameObject newNote = playerSphereSpawner.GetSphere();
        newNote.transform.position = laneManager.LaneStarts[laneIndex];

        Color mappedColor = colorIndex; // default
        switch (gesture)
        {
            case "Tuo": mappedColor = colorThumb; break;
            case "Index": mappedColor = colorIndex; break;
            case "Middle": mappedColor = colorMiddle; break;
            case "Ring": mappedColor = colorRing; break;
            case "Pinky": mappedColor = colorPinky; break;
            case "Mute": mappedColor = colorMute; break;
            case "YaoZhi": mappedColor = colorTremolo; break;
        }

        Renderer rend = newNote.GetComponent<Renderer>();
        if (rend != null) rend.material.color = mappedColor;

        activePlayerNotes.Add(new PlayerNote { 
            noteObject = newNote, 
            laneIndex = laneIndex,
            noteColor = mappedColor 
        });
    }

    void Update()
    {
        if (!isActiveState) return;

        // freeze player notes if the tutorial is active or the game is paused
        if (StateManager.Instance != null && StateManager.Instance.IsTutorialPaused) return;
        if (StateManager.Instance != null && StateManager.Instance.CurrentState == StateManager.GameState.Paused) return;

        MovePlayerNotes();
    }

    private void MovePlayerNotes()
    {
        for (int i = activePlayerNotes.Count - 1; i >= 0; i--)
        {
            PlayerNote pNote = activePlayerNotes[i];

            if (!laneManager.LaneEnds.ContainsKey(pNote.laneIndex)) continue;

            Vector3 targetPos = laneManager.LaneEnds[pNote.laneIndex];
            
            pNote.noteObject.transform.position = Vector3.MoveTowards(
                pNote.noteObject.transform.position,
                targetPos,
                playerNoteSpeed * Time.deltaTime
            );

            // sphere reaches enemy (with tiny threshold)
            if (Vector3.Distance(pNote.noteObject.transform.position, targetPos) < 0.05f)
            {
                playerSphereSpawner.ReturnSphere(pNote.noteObject);
                activePlayerNotes.RemoveAt(i);
                continue;
            }

            // collision detection
            bool hitRegistered = false;
            for (int j = enemyNoteManager.activeNotes.Count - 1; j >= 0; j--)
            {
                var eNote = enemyNoteManager.activeNotes[j];
                
                if (eNote.laneIndex != pNote.laneIndex) continue; // only check collisions that are in the same lane
                if (eNote.noteColor != pNote.noteColor) continue; // only notes of the same color can collide

                float dist = Vector3.Distance(pNote.noteObject.transform.position, eNote.noteObject.transform.position);

                if (dist < collisionDistanceThreshold)
                {
                    if (collisionSparkPrefab != null)
                        Instantiate(collisionSparkPrefab, pNote.noteObject.transform.position, Quaternion.identity);

                    if (enemyDamageEffectPrefab != null)
                        Instantiate(enemyDamageEffectPrefab, targetPos, Quaternion.identity);

                    Vector3 guzhengLaneStartPosition = laneManager.LaneStarts[eNote.laneIndex];
                    float distanceToGuzheng = Vector3.Distance(eNote.noteObject.transform.position, guzhengLaneStartPosition);

                    if (scoreManager != null)
                        scoreManager.RegisterHit(distanceToGuzheng, pNote.noteObject.transform.position);

                    enemyNoteManager.DestroyNoteFromBot(eNote); 
                    hitRegistered = true;
                    
                    break; // stop checking other enemy notes since this player note exploded
                }
            }

            // clean up the player note if it exploded
            if (hitRegistered)
            {
                playerSphereSpawner.ReturnSphere(pNote.noteObject);
                activePlayerNotes.RemoveAt(i);
            }
        }
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (isNowActive)
        {
            //automatically find the spawned guzheng in the scene and grab its strings
            GuzhengController spawnedGuzheng = FindObjectOfType<GuzhengController>();
            if (spawnedGuzheng != null)
                guzhengStrings = spawnedGuzheng.stringsInOrder;
        }
        else
        {
            foreach (var note in activePlayerNotes)
                playerSphereSpawner.ReturnSphere(note.noteObject);

            activePlayerNotes.Clear();
        }
    }
}