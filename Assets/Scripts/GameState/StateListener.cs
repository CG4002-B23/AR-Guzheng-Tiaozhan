using UnityEngine;
using System.Collections.Generic;

// handles the functionality changes during transition between game states
// 'abstract' only exists to be inherited from
public abstract class StateListener : MonoBehaviour
{
    [Tooltip("The state in which this script should be active.")]
    public List<GameManager.GameState> targetStates;

    protected bool isActiveState = false; 

    // note: if child classes need OnEnable or OnDisable in their implementation, then 
    // need to start off the function definition with base.OnEnable(); or base.OnDisable();
    // to first run these function definitions in this file
    protected virtual void OnEnable()
    {
        GameManager.OnGameStateChanged += HandleGameStateChange;
    }

    protected virtual void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleGameStateChange;
    }

    private void HandleGameStateChange(GameManager.GameState newState) // (state receiver)
    {
        isActiveState = targetStates.Contains(newState);
        OnStateToggled(isActiveState);
    }

    protected virtual void OnStateToggled(bool isNowActive)
    {
    }
}