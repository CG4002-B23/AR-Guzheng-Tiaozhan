using System.Collections.Generic;
using UnityEngine;

public class IncomingNoteManager : StateListener
{
    [Header("References")]
    public SphereSpawner sphereSpawner;
    public LaneManager laneManager;

    [Header("Spawning & Movement")]
    public float spawnInterval = 1.0f;
    public float noteSpeed = 2.0f;

    private float spawnTimer = 0f;

    private Color colorRightTuo = Color.green;
    private Color colorRightMuo = Color.blue;

    private class ActiveNote
    {
        public GameObject noteObject;
        public int laneIndex;
    }

    private List<ActiveNote> activeNotes = new List<ActiveNote>();

    void Update()
    {
        if (!isActiveState) return;

        HandleSpawning(); MoveNotes();
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
        
        Renderer rend = newNoteObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = isRightTuo ? colorRightTuo : colorRightMuo;
        }

        activeNotes.Add(new ActiveNote { noteObject = newNoteObj, laneIndex = randomLane });
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

            //  if sphere reaches destination (with tiny threshold), recycle it
            if (Vector3.Distance(note.noteObject.transform.position, targetPosition) < 0.05f)
            {
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
}