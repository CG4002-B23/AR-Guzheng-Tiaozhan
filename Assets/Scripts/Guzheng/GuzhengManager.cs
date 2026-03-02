using System.Collections.Generic;
using UnityEngine;

public class GuzhengController : MonoBehaviour
{
    [Header("String References")]
    [Tooltip("Assign the 5 string children here from Lane 0 to Lane 4")]
    public List<GuzhengStringInteraction> stringsInOrder;
}