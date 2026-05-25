using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        PlayerController.ClearRememberedScenePositions();

        if (PlayerController.Instance != null)
            Destroy(PlayerController.Instance.gameObject);

        SceneManager.LoadSceneAsync("MainScene");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
