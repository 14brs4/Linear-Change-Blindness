using UnityEngine; 

 

  

public class RayInteractorDebugger : MonoBehaviour 

{ 

    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor; 

  

    private void Start() 

    { 

        // Get the XR Ray Interactor component on this controller 

        rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(); 

    } 

  

    private void Update() 

    { 

        // Check if the ray is hitting something 

        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit)) 

        { 

            Debug.Log($"Ray is hitting: {hit.collider.name}"); 

        } 

        else 

        { 

            Debug.Log("Ray is not hitting anything."); 

        } 

    } 

} 

 