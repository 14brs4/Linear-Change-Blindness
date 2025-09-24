using UnityEngine; 

  

public class SphereManager : MonoBehaviour 

{ 

    public bool isChanged; // Tracks if this sphere was the one that changed color 

    private GameManager gameManager; 

  

    private void Start() 

    { 

        // Find the GameManager in the scene 

        gameManager = FindObjectOfType<GameManager>(); 

    } 

  

    // Function to set if this sphere was changed (public so it can be called by other scripts) 

    public void SetChanged(bool changed) 

    { 

        isChanged = changed; // Set this when the sphere changes color 

    } 

  

    // Shared logic for when a sphere is clicked (2D or VR) 

    private void HandleSphereClick() 

    { 

        if (gameManager.CanClick()) 

        { 

            // Save that this sphere was clicked 

            gameManager.selectedSphere = this.name; 

  

            // Check if this sphere was the one changed and save success or failure 

            if (isChanged) 

            { 

                gameManager.success = "1"; 

            } 

            else 

            { 

                gameManager.success = "0"; 

            } 

  

            // Save trial results now that trial is complete 

            gameManager.SaveTrialResults(); 

  

            // Show the in-between screen 

            gameManager.ShowBlackScreen("Press Space to Continue"); 

        } 

        else 

        { 

            Debug.Log("Wait until trial is complete to select a sphere"); 

        } 

    } 

  

    // If this sphere is clicked with a mouse (2D input) 

    private void OnMouseDown() 

    { 

        HandleSphereClick(); 

    } 

  

    // Triggered when a VR ray or direct interactor selects this sphere 

    public void OnSelect(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor interactor) 

    { 

        Debug.Log($"Sphere {name} selected via VR!"); 

        HandleSphereClick(); 

    } 

} 