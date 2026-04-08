using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry-point script attached to the ParentScene. On startup, additively loads
/// the ForegroundScene (painting interpretation) and BackgroundScene (virtual atelier)
/// so that all three scenes run concurrently.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    /// <summary>
    /// Additively loads the ForegroundScene and BackgroundScene into the running ParentScene.
    /// </summary>
    private void Start()
    {
        SceneManager.LoadScene("ForegroundScene", LoadSceneMode.Additive);
        SceneManager.LoadScene("BackgroundScene", LoadSceneMode.Additive);
    }
}
