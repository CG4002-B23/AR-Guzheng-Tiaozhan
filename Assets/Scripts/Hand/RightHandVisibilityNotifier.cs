using UnityEngine;

public class RightHandVisibilityNotifier : MonoBehaviour
{
    public RightHandAnimationController animationController;

    void OnEnable()
    {
        animationController.OnHandBecameVisible();
    }
}