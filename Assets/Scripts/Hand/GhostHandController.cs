using System.Collections;
using UnityEngine;

public class GhostHandController : MonoBehaviour
{
    [Header("References")]
    public GameObject ghostHandContainer;
    public Animator ghostHandAnimator;

    [Header("Positioning & Timing")]
    public Vector3 handOffset = new Vector3(0, 0.1f, 0); // Hover slightly above the string
    public float hoverHeight = 0.15f; 
    public float hoverDuration = 0.5f;
    public float gestureHoldTime = 1.0f;
    public float loopPauseTime = 1.0f;
    
    [Header("Visuals")]
    [Tooltip("Max opacity (0-1)")]
    public float maxHandOpacity = 0.5f;

    private Coroutine activeHandLoop;
    private Renderer[] handRenderers;
    private bool isAnimating = false;

    private void Awake()
    {
        if (ghostHandContainer != null)
        {
            ghostHandContainer.SetActive(false);
            handRenderers = ghostHandContainer.GetComponentsInChildren<Renderer>();
        }
    }

    public void StartSequence(string gestureTriggerName, Vector3 targetStringPos)
    {
        if (ghostHandContainer == null) return;

        // Clean up any existing sequences before starting a new one
        StopSequence(); 

        isAnimating = true;
        ghostHandContainer.SetActive(true);
        activeHandLoop = StartCoroutine(GhostHandRoutine(gestureTriggerName, targetStringPos));
    }

    public void StopSequence()
    {
        isAnimating = false;
        
        if (activeHandLoop != null)
        {
            StopCoroutine(activeHandLoop);
            activeHandLoop = null;
        }

        if (ghostHandContainer != null)
        {
            ghostHandContainer.SetActive(false);
        }
    }

    private IEnumerator GhostHandRoutine(string gestureTriggerName, Vector3 targetStringPos)
    {
        Vector3 bottomPos = targetStringPos + handOffset;
        Vector3 topPos = bottomPos + (Vector3.up * hoverHeight);

        while (isAnimating) 
        {
            // --- STEP 1 & 2: Start high/faded, hover downwards, fade in ---
            float timer = 0f;
            ghostHandContainer.transform.position = topPos;
            SetHandAlpha(0f);
            
            if (ghostHandAnimator != null) ghostHandAnimator.SetTrigger("IdleTrigger");

            while (timer < hoverDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / hoverDuration;
                
                ghostHandContainer.transform.position = Vector3.Lerp(topPos, bottomPos, progress);
                SetHandAlpha(progress * maxHandOpacity); 
                yield return null; 
            }

            // --- STEP 3: Do the gesture and hold ---
            if (ghostHandAnimator != null) ghostHandAnimator.SetTrigger(gestureTriggerName);
            yield return new WaitForSeconds(gestureHoldTime);

            // --- STEP 4: Hover back upwards and fade out ---
            timer = 0f;
            while (timer < hoverDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / hoverDuration;
                
                ghostHandContainer.transform.position = Vector3.Lerp(bottomPos, topPos, progress);
                SetHandAlpha((1f - progress) * maxHandOpacity); 
                yield return null; 
            }

            // Pause invisibly before repeating the loop
            yield return new WaitForSeconds(loopPauseTime);
        }
    }

    private void SetHandAlpha(float alpha)
    {
        if (handRenderers == null) return;
        
        foreach (var rend in handRenderers)
        {
            if (rend.material.HasProperty("_Color"))
            {
                Color c = rend.material.color;
                c.a = alpha;
                rend.material.color = c;
            }
        }
    }
}