using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SphereClickHandler : MonoBehaviour
{
    private GameManager gameManager; // Reference to the GameManager

    private void Start()
    {
        // Find the GameManager in the Scene
        gameManager = FindObjectOfType<GameManager>();

        // Log an error if the GameManager is not found
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found in the Scene! Please ensure there is a GameManager in the Scene.");
        }
    }

    // This method will be triggered by the Select Entered event
    public void OnSphereSelected(SelectEnterEventArgs args)
    {
        if (gameManager != null)
        {
            // Pass the selected sphere (this GameObject) to the GameManager
            gameManager.OnSphereClicked(gameObject);
        }
    }
}
