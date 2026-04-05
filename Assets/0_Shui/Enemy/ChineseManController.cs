using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChineseManController : MonoBehaviour
{
    private Animator anim;
    // Start is called before the first frame update
    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        
    }

    public void PlayAttack() 
    {
        if (anim != null) 
        {
            int randomAttack = Random.Range(0, 2);
            anim.SetInteger("AttackIndex", randomAttack);
            anim.SetTrigger("Attack");
        }
    }

    public void PlayDeath()
    {
        if (anim != null) 
        {
            anim.SetTrigger("Die");
        }
    }

    public void PlayHit()
    {
        if (anim != null) 
        {
            anim.SetTrigger("Hit");
        }
    }
}
