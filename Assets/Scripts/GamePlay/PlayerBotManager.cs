using System.Collections.Generic;
using UnityEngine;

public class PlayerBotManager : StateListener
{
    [Header("References")]
    public IncomingNoteManager enemyNoteManager;
    public SphereSpawner playerSphereSpawner; 
    public LaneManager laneManager;
    public HealthManager healthManager;
    public GameObject collisionSparkPrefab;
    public GameObject enemyDamageEffectPrefab;

    [Header("Bot Combat Settings")]
    [Tooltip("How far from the Guzheng should the collision happen?")]
    public float hitDistanceFromGuzheng = 0.2f;
    [Tooltip("How fast the player's notes fly towards the enemy")]
    public float playerNoteSpeed = 10.0f;
    public int botDamage = 10;

    [Tooltip("How far away the player's note is from the enemy's note in the same lane to count as a collision")]
    public float collisionDistanceThreshold = 0.05f;

    private class BotNote
    {
        public GameObject noteObject;
        public IncomingNoteManager.ActiveNote targetEnemyNote;
    }

    private List<BotNote> activeBotNotes = new List<BotNote>();

    void Update()
    {
        if (!isActiveState) return;

        FireAtEnemyNotes();
        MoveBotNotes();
    }

    private void FireAtEnemyNotes()
    {
        // time = distance / speed
        float timeForPlayerNoteToReachHitPoint = hitDistanceFromGuzheng / playerNoteSpeed;
        
        // bot must fire when the enemy note is exactly this distance from the Guzheng
        float triggerDistance = hitDistanceFromGuzheng + (enemyNoteManager.noteSpeed * timeForPlayerNoteToReachHitPoint);

        foreach (var enemyNote in enemyNoteManager.activeNotes)
        {
            if (enemyNote.isTargetedByBot) continue; // already fired

            // Find distance of this enemy note from the Guzheng
            Vector3 guzhengLaneStartPos = laneManager.LaneStarts[enemyNote.laneIndex];
            float distanceFromEnemyNote = Vector3.Distance(enemyNote.noteObject.transform.position, guzhengLaneStartPos);

            if (distanceFromEnemyNote <= triggerDistance)
            {
                enemyNote.isTargetedByBot = true;
                SpawnBotNote(enemyNote);
            }
        }
    }

    private void SpawnBotNote(IncomingNoteManager.ActiveNote target)
    {
        GameObject newNote = playerSphereSpawner.GetSphere();
        newNote.transform.position = laneManager.LaneStarts[target.laneIndex];
        
        Renderer rend = newNote.GetComponent<Renderer>();
        if (rend != null) rend.material.color = target.noteColor;

        activeBotNotes.Add(new BotNote { noteObject = newNote, targetEnemyNote = target });
    }

    private void MoveBotNotes()
    {
        for (int i = activeBotNotes.Count - 1; i >= 0; i--)
        {
            BotNote botNote = activeBotNotes[i];
            
            // if the enemy note disappeared, clean up the bot note
            if (botNote.targetEnemyNote == null || !botNote.targetEnemyNote.noteObject.activeInHierarchy)
            {
                playerSphereSpawner.ReturnSphere(botNote.noteObject);
                activeBotNotes.RemoveAt(i);
                continue;
            }

            // move bot note towards the enemy note
            botNote.noteObject.transform.position = Vector3.MoveTowards(
                botNote.noteObject.transform.position,
                botNote.targetEnemyNote.noteObject.transform.position,
                playerNoteSpeed * Time.deltaTime
            );

            float distanceBetweenNotes = Vector3.Distance(botNote.noteObject.transform.position, botNote.targetEnemyNote.noteObject.transform.position);
            
            if (distanceBetweenNotes < collisionDistanceThreshold) 
            {
                // spawn collision particles
                if (collisionSparkPrefab != null)
                    Instantiate(collisionSparkPrefab, botNote.noteObject.transform.position, Quaternion.identity);

                if (enemyDamageEffectPrefab != null)
                {
                    Vector3 enemyPosition = laneManager.LaneEnds[botNote.targetEnemyNote.laneIndex];
                    Instantiate(enemyDamageEffectPrefab, enemyPosition, Quaternion.identity);
                }

                healthManager.DamageEnemy(botDamage);
                enemyNoteManager.DestroyNoteFromBot(botNote.targetEnemyNote);
                playerSphereSpawner.ReturnSphere(botNote.noteObject);
                activeBotNotes.RemoveAt(i);
            }
        }
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (!isNowActive)
        {
            // clear out any flying player notes if the game stops
            foreach (var note in activeBotNotes)
            {
                playerSphereSpawner.ReturnSphere(note.noteObject);
            }
            activeBotNotes.Clear();
        }
    }
}