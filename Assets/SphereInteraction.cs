using UnityEngine; 

using UnityEngine.XR.Interaction.Toolkit; 

  

public class SphereInteraction : MonoBehaviour 

{ 

    private GameManager gameManager; 

  

    private void Start() 

    { 

        // Find the GameManager in the scene 

        gameManager = FindObjectOfType<GameManager>(); 

    } 

  

    // Triggered by XR when the sphere is selected 

    public void OnSelectEnter(SelectEnterEventArgs args) 

    { 

        // Notify the GameManager that this sphere was clicked 

        if (gameManager != null) 

        { 

            gameManager.OnSphereClicked(gameObject); 

        } 

    } 

} 