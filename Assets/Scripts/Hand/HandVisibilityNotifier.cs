using UnityEngine;

public class HandVisibilityNotifier : MonoBehaviour
{
    public HandAnimationController animationController;

    void OnEnable()
    {
        if (animationController != null) animationController.OnHandBecameVisible();
    }
}