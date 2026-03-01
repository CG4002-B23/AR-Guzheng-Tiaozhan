using System.Collections.Generic;
using UnityEngine;

public class IncomingNoteManager : StateListener
{
    [Header("References")]
    public SphereSpawner sphereSpawner;
    public LaneManager laneManager;
    public HealthManager healthManager;

    [Header("Spawning & Movement")]
    public float spawnInterval = 1.0f;
    public float noteSpeed = 2.0f;
    public int missedNoteDamage = 5;

    private float spawnTimer = 0f;

    private Color colorRightTuo = Color.green;
    private Color colorRightMuo = Color.blue;

    public class ActiveNote
    {
        public GameObject noteObject;
        public int laneIndex;
        public Color noteColor;
        public bool isTargetedByBot;
    }

    public List<ActiveNote> activeNotes = new List<ActiveNote>();

    void Update()
    {
        if (!isActiveState) return;

        HandleSpawning(); 
        MoveNotes();
    }

    private void HandleSpawning()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            SpawnNote();
            spawnTimer = 0f;
        }
    }

    private void SpawnNote()
    {
        if (laneManager.LaneEnds.Count == 0 || laneManager.LaneStarts.Count == 0) return;

        int randomLane = Random.Range(0, 5); 

        if (!laneManager.LaneEnds.ContainsKey(randomLane)) return;

        GameObject newNoteObj = sphereSpawner.GetSphere();
        newNoteObj.transform.position = laneManager.LaneEnds[randomLane];

        // temporary logic for deciding which note to spawn
        bool isRightTuo = Random.value > 0.5f; // 50% chance of spawning a right tuo
        Color requiredNoteColor = isRightTuo ? colorRightTuo : colorRightMuo;
        
        Renderer rend = newNoteObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = requiredNoteColor;
        }

        activeNotes.Add(new ActiveNote { 
            noteObject = newNoteObj, 
            laneIndex = randomLane,
            noteColor = requiredNoteColor,
            isTargetedByBot = false
        });
    }

    private void MoveNotes() // move the notes down their respective lanes
    {
        // Loop backwards so we can safely remove items from the list as they finish
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

            // sphere reaches guzheng (with tiny threshold)
            if (Vector3.Distance(note.noteObject.transform.position, targetPosition) < 0.05f)
            {
                if (healthManager != null)
                    healthManager.DamagePlayer(missedNoteDamage);

                sphereSpawner.ReturnSphere(note.noteObject);
                activeNotes.RemoveAt(i);
            }
        }
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (!isNowActive)
        {
            foreach (var note in activeNotes)
            {
                sphereSpawner.ReturnSphere(note.noteObject);
            }
            activeNotes.Clear();
            spawnTimer = 0f;
        }
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