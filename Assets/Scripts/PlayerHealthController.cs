using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealthController : MonoBehaviour
{

    public static PlayerHealthController instance;

    public int currentHealth, maxHealth;

    public float invincibilityLength;
    private float invincibleCounter;

    private SpriteRenderer theSR;

    public GameObject deathEffect;
    private void Awake()
    {
        instance = this;
    }


    // Start is called before the first frame update
    void Start()
    {
        currentHealth = maxHealth;
        theSR = GetComponent<SpriteRenderer>();

    }

    // Update is called once per frame
    void Update()
    {

        if (invincibleCounter > 0)
        {
            invincibleCounter -= Time.deltaTime;
            theSR.color = new Color(theSR.color.r, theSR.color.g, theSR.color.b, 0.5f);
        }
        else
        {
            theSR.color = new Color(theSR.color.r, theSR.color.g, theSR.color.b, 1f);
        }

    }


    public void DealDamage()
    {

        if (invincibleCounter <= 0)
        {

            currentHealth -= 1;

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                //gameObject.SetActive(false);

                Instantiate(deathEffect, transform.position, transform.rotation);

                LevelManager.instance.RespawnPlayer();
            }
            else
            {
                invincibleCounter = invincibilityLength;
                theSR.color = new Color(theSR.color.r, theSR.color.g, theSR.color.b, 0.5f);

                PlayerController.instance.Knockback();

                AudioManager.instance.PlaySFX(9);//Player hurt sound.


            }

            UIController.instance.UpdateHealthDisplay();

        }

    }


    public void HealPlayer()
    {
        //currentHealth = maxHealth;

        if (currentHealth < maxHealth)
        {
            currentHealth += 1;
        }

        UIController.instance.UpdateHealthDisplay();
    }

}
