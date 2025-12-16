using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    private void Start()
    {
        SceneManager.LoadScene("ForegroundScene", LoadSceneMode.Additive);
        SceneManager.LoadScene("BackgroundScene", LoadSceneMode.Additive);
    }
}
