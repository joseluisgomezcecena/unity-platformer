using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pickup : MonoBehaviour
{

    public bool isGem, isHealth;

    private bool collected;

    public GameObject pickupEffect;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !collected)
        {
            if (isGem)
            {
                LevelManager.instance.gemsCollected++;
                collected = true;
                Destroy(gameObject);

                Instantiate(pickupEffect, transform.position, transform.rotation);
                
                
                UIController.instance.UpdateGemCount();
            }

            if (isHealth)
            {
               if(PlayerHealthController.instance.currentHealth != PlayerHealthController.instance.maxHealth)
               {
                    PlayerHealthController.instance.HealPlayer();
                    collected = true;
                    Destroy(gameObject);
                    Instantiate(pickupEffect, transform.position, transform.rotation);
               }
            }
        }
    }
}
