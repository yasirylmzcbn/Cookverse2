using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// I am not sure what else to do with the ammo count without a gun implemented

public class AmmoScript : MonoBehaviour
{
    public Potato_Shooter potatoShooter;
    public TextMeshProUGUI ammoText;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResolveShooter();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Scene changes can move/enable the player and gun.
        ResolveShooter(force: true);
    }

    // Update is called once per frame
    void Update()
    {
        if (ammoText == null)
            return;

        if (potatoShooter == null)
            ResolveShooter();

        if (potatoShooter == null || !potatoShooter.gameObject.activeInHierarchy)
        {
            ammoText.text = "";
            return;
        }

        ammoText.text = potatoShooter.currentAmmo + " / " + potatoShooter.maxAmmo;
    }

    private void ResolveShooter(bool force = false)
    {
        if (!force && potatoShooter != null) return;

        // Active objects first.
        potatoShooter = FindFirstObjectByType<Potato_Shooter>();
        if (potatoShooter != null) return;

        // Include inactive/persisted objects.
        Potato_Shooter[] all = Resources.FindObjectsOfTypeAll<Potato_Shooter>();
        for (int i = 0; i < all.Length; i++)
        {
            Potato_Shooter s = all[i];
            if (s == null) continue;
            if (!s.gameObject.scene.IsValid() || !s.gameObject.scene.isLoaded) continue;

            potatoShooter = s;
            return;
        }
    }
}
