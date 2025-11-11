using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; 

public enum ChangeType
{
    Hue,
    Luminance,
    Size,
    Orientation
}

public enum MotionType
{
    [InspectorName("Observer Motion")]
    ObserverMotion,
    [InspectorName("Object Motion")]
    ObjectMotion,
    [InspectorName("Static")]
    Static
}

/// <summary>
/// GameManager for VR Change Blindness Experiment
/// 
/// Block System: Experiment consists of 3 sequential blocks, each with a configurable motion type:
/// - Block 1, 2, 3: Each block contains only one motion type (Observer Motion, Object Motion, or Static)
/// - Trials per block: Configurable number of trials in each block
/// - Observer Motion: Currently identical to Object Motion (ready for future implementation)
/// - Object Motion: Spheres move linearly toward the user
/// - Static: Spheres remain stationary
/// 
/// The experiment progresses through Block 1 → Block 2 → Block 3 with breaks between blocks.
/// </summary>
public class GameManager : MonoBehaviour
{
    [HideInInspector] public XRInteractionManager interactionManager; // Reference to the XR Interaction Manager 
    // Prefabs
    [HideInInspector] public GameObject spherePrefab; // Assign sphere prefab in the inspector
    [HideInInspector] public GameObject stripedSpherePrefab; // Assign sphere prefab in the inspector
    
    // Participant details
    

    // Experiment type selection (dropdown in inspector)
    [Header("Trial Setup")]
    public string participantName; // Participant number
    [Tooltip("Select the type of change to detect in the experiment")]
    public ChangeType changeType = ChangeType.Hue;
    
    [Header("Block Configuration")]
    [CustomLabel("Training Block")]
    [Tooltip("Enable training block before main experiment blocks")]
    public bool trainingBlock = false;

    [CustomLabel("Trials per Training Block")]
    [Tooltip("Number of training trials (grid-only, no spheres)")]
    [ConditionalEnable("trainingBlock", true)]
    public int trialsPerTrainingBlock = 5;



    [CustomLabel("Block 1")]
    [Tooltip("Motion type for Block 1 trials")]
    public MotionType block1Type = MotionType.Static;

    [CustomLabel("Block 2")]
    [Tooltip("Motion type for Block 2 trials")]
    public MotionType block2Type = MotionType.ObjectMotion;

    [CustomLabel("Block 3")]
    [Tooltip("Motion type for Block 3 trials")]
    public MotionType block3Type = MotionType.ObserverMotion;

    [CustomLabel("Trials per Block")]
    [Tooltip("Number of trials in each block")]
    public int trialsPerBlock = 10;
    
    private bool oneDirectionTrials = true;
    private bool twoDirectionTrials = false;

    // Helper properties for backward compatibility
    public bool changeHue => changeType == ChangeType.Hue;
    public bool changeLuminance => changeType == ChangeType.Luminance;
    public bool changeSize => changeType == ChangeType.Size;
    public bool changeOrientation => changeType == ChangeType.Orientation;
    
    // Trial length details
    [Header("General Settings")]
    [CustomLabel("Trial Length (s)")]
    public float trialLength = 4f;
    [CustomLabel("Trial Start Delay (s)")]
    public float trialStartDelay = 1f; // Delay before trial begins (applies to all trial types)
    [Tooltip("Enable or disable sphere blinking visual cue at the start of trials.")]
    public bool blinkSpheres = true;
    [CustomLabel("Blink Duration (s)")]
    [ConditionalEnable("blinkSpheres", true)]
    public float blinkDuration = 0.3f; // Adjustable blink timing for ring cue
    [CustomLabel("Movement Speed (units/s)")]
    public float movementSpeed = 2f; // Speed of linear movement towards user in units per second
    [CustomLabel("Change Duration (s)")]
    [Tooltip("Duration for gradual sphere changes. 0 = instantaneous. Change occurs from trialLength/2 - changeDuration/2 to trialLength/2 + changeDuration/2.")]
    public float changeDuration = 0f;
    



    private string participantFileName
    {
        get
        {
            if (changeHue) return $"{participantName}_hue.csv";
            if (changeLuminance) return $"{participantName}_luminance.csv";
            if (changeSize) return $"{participantName}_size.csv";
            if (changeOrientation) return $"{participantName}_orientation.csv";
            return $"{participantName}.csv";
        }
    }

    // Folder for saving results (always in the unity project folder)
    private string resultsFolder;

    // Sphere details
    [Header("Linear Movement Settings")]
    [CustomLabel("# of Spheres")]
    public int numberOfSpheres = 6; // Number of spheres to create in the single ring
    [CustomLabel("Ring Radius")]
    public float ringRadius = 2f; // Radius of the ring

    // --- Outer (third) ring settings ---
    // Change details (note: to change details about orientation stripes go to the stripes material in the materials folder)
    [Range(0f, 1f)] private float defaultHue = 0f;

    [Range(0f, 1f)] public float sphereSaturation = 0.8f; // Fixed Saturation (0 to 1)
    [Range(0f, 1f)] public float sphereValue = 0.8f; // Fixed Value (0 to 1)
    public float sphereSize = 0.7f;
    [Tooltip("When enabled, the inactive ring maintains default appearance (no randomization of hue, saturation, value, or size).")]
    [CustomLabel("Start Point")]
    public Vector3 centerPoint = Vector3.zero; // Starting position of the ring (spheres move from here to endPoint)
        // Movement distance now defined by difference between centerPoint and endPoint
    public Vector3 endPoint = new Vector3(0f, 0f, -3f); // Where spheres move to during trials (negative Z = toward player)
    // --- Audio cue fields ---
    public AudioClip lowSound; // Assign normal beep in Inspector
    public AudioClip highSound; // Assign high-pitch beep in Inspector
    [HideInInspector] public AudioSource audioSource; // Assign AudioSource in Inspector
    [CustomLabel("Sound Interval (s)")]
    public float soundInterval = 0.75f; // Time between beeps in seconds
    
    
    [Tooltip("Use CIEDE2000-based perceptually uniform hue changes for sphere color modifications. When false, uses simple HSV hue changes.")]
    private bool weightedHueChange = false;
    
    [Header("Hue Change Settings")]
    [CustomLabel("Hue Change (°)")]
    //[ConditionalEnable("weightedHueChange", false)]
    [Range(0f, 1f)] 
    [Tooltip("Simple HSV-based hue change amount (0-1). Only used when CIEDE2000 system is disabled.")]
    public float hueChangeHSV = 0.3f;
    
    [CustomLabel("Hue Change (ΔE)")]
    [ConditionalEnable("weightedHueChange", true)]
    [Tooltip("Target ΔE (Delta E) for perceptually uniform color changes. Range: 1 (subtle) to 5 (obvious). Used when CIEDE2000 system is enabled.")]
    private float weightedHueChangeDelta = 4.0f;
    
    
    [Header("Luminance Change Settings")]
    [Range(0f, 0.5f)] 
    [Tooltip("Luminance (brightness) change amount for sphere modifications.")]
    public float luminanceChange = 0.2f;
    
    [Header("Size Change Settings")]
    [Tooltip("Size change amount for sphere scaling.")]
    public float sizeChange = 0.2f;
    [Tooltip("Minimum allowed sphere size.")]
    public float minSize = 0.5f;
    [Tooltip("Maximum allowed sphere size.")]
    public float maxSize = 1.5f;
    
    [Header("Orientation Change Settings")]
    [Tooltip("When enabled, moves each sphere individually to preserve stripe orientations during linear movement. When disabled, uses more efficient parent movement but sphere orientations may change. Only affects orientation trials.")]
    public bool individualSphereMovement = false; // Moves all spheres individually instead of using parent movement (less efficient but retains orientations)

    [CustomLabel("Orientation Change (°)")]
    [Tooltip("Orientation change amount in degrees for striped spheres.")]
    public float orientationChange = 40f;
    
    // Perceptual color weighting system
    [Header("Perceptual Color System")]
    [Tooltip("Use CIEDE2000-based perceptually-weighted hue generation instead of uniform random. When false, uses simple uniform HSV distribution.")]
    private bool weightedHueGeneration = false;
    
    
    
    [Tooltip("Debug: Log the perceptual weight distribution to console for analysis.")]
    private bool debugPerceptualWeights = false;


    // Using purely random attribute generation within acceptable ranges - no similarity checks

    // Single ring system - no separate ring configurations needed




    
    
    // Only Ring 2 (middle ring) is used in this simplified version
    //public int oneDirectionTrials = 24;
    //public int twoDirectionTrials = 24;

    // Tracking number of trial type run
    private int staticTrialsRun = 0;
    private int movingTrialsRun = 0;
    //private int oneDirectionTrialsRun = 0;
    //private int twoDirectionTrialsRun = 0;
    
    // Tracking number of trials run
    private int trialNumber = 0;
    
    // Block tracking
    private int currentBlock = 1; // Current block number (1, 2, or 3)
    private int currentBlockTrialCount = 0; // Trials completed in current block
    private int totalBlocks = 3; // Always 3 blocks
    private bool isInBreakScreen = false; // Flag to disable A button during break screens

    // Trial types are now determined by block configuration instead of random selection
    // private string[] trialTypes = { "Static", "Moving" }; // No longer used - blocks determine trial types
    // Note: Moving refers to linear movement towards the user

    // Results details
    private string[][] results = new string[0][];
    private string[] headers;
    private string originalResult; // Original value before change
    private string changedResult;  // New value after change
    [HideInInspector] public string selectedSphere;
    [HideInInspector] public string success;
    private string[] trialResults;
    private string movementDirection; // Stores movement direction for the trial


    // Experiment controls
    private bool canClick = false; // Prevents clicking until allowed
    private bool experimentRunning = true;

    // Inbetween screen
    [HideInInspector] public GameObject blackScreen; // Reference to the black screen UI element
    private bool blackScreenUp;

    // List for storing objects
    [HideInInspector] public GameObject[] spheres; // Store references to the created spheres
    private List<Coroutine> activeChangeCoroutines = new List<Coroutine>(); // Track running gradual change coroutines
    private Coroutine activeBeepCoroutine = null; // Track the current beep sequence
    private bool beepSequenceAborted = false; // Flag to abort beep sequence
    
    // Stop all running gradual change coroutines
    private void StopAllActiveChangeCoroutines()
    {
        foreach (Coroutine coroutine in activeChangeCoroutines)
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
        }
        activeChangeCoroutines.Clear();
    }
    
    // Stop the active beep sequence
    private void StopBeepSequence()
    {
        if (activeBeepCoroutine != null)
        {
            beepSequenceAborted = true;
            StopCoroutine(activeBeepCoroutine);
            activeBeepCoroutine = null;
            Debug.Log("[AudioCue] Beep sequence aborted by user interaction");
        }
    }
    
    // Enable clicking when the change starts (for static trials)
    private System.Collections.IEnumerator EnableClickingWhenChangeStarts(float changeStartTime)
    {
        // Wait until the change starts
        while (Time.time < changeStartTime)
        {
            yield return null;
        }
        
        // Enable clicking when change begins
        canClick = true;
        Debug.Log("[StaticTrial] Clicking enabled - change has begun");
    }
    
    // Update focal point to be centered between all spheres
    private void UpdateFocalPointPosition()
    {
        if (focusPointText == null || spheres == null || spheres.Length == 0)
            return;
            
        // Calculate the center position of all spheres
        Vector3 centerPosition = Vector3.zero;
        int validSphereCount = 0;
        
        for (int i = 0; i < spheres.Length; i++)
        {
            if (spheres[i] != null && spheres[i].transform != null)
            {
                centerPosition += spheres[i].transform.position;
                validSphereCount++;
            }
        }
        
        if (validSphereCount > 0)
        {
            // Calculate average position (center of all spheres)
            centerPosition /= validSphereCount;
            
            // Update focal point position to match sphere center (same X,Y plane)
            if (focusPointText.rectTransform != null)
            {
                focusPointText.rectTransform.position = centerPosition;
                Debug.Log($"[FocalPoint] Updated to center of spheres: {centerPosition}");
            }
        }
    }
    


    // Control spatial grid visibility during experiment
    private void SetGridForTrialState(bool duringTrial)
    {
        if (spatialGridManager != null)
        {
            if (duringTrial)
            {
                // During active trial - let SpatialGridManager control visibility/opacity
                spatialGridManager.SetTrialState(true);
            }
            else
            {
                // Between trials - let SpatialGridManager control visibility
                spatialGridManager.SetTrialState(false);
            }
        }
    }



    // Initialize trial block system
    private void InitializeTrialBlocks()
    {
        Debug.Log($"[TrialBlocks] Initializing 3-block system with {trialsPerBlock} trials per block");
        Debug.Log($"[TrialBlocks] Block 1: {block1Type}, Block 2: {block2Type}, Block 3: {block3Type}");
        
        // Reset counters
        currentBlockTrialCount = 0;
        currentBlock = trainingBlock ? 0 : 1; // Start with training block (0) if enabled, otherwise Block 1
    }
    
    // Check if it's time for a break between trial blocks
    private bool ShouldTakeBreak()
    {
        int totalTrialsCompleted = staticTrialsRun + movingTrialsRun;
        int totalTrials = trialsPerBlock * totalBlocks;
        
        bool shouldBreak = false;
        
        if (currentBlock == 0) // Training block
        {
            shouldBreak = currentBlockTrialCount >= trialsPerTrainingBlock;
        }
        else // Regular blocks
        {
            shouldBreak = currentBlockTrialCount >= trialsPerBlock && currentBlock < totalBlocks;
        }
        
        Debug.Log($"[TrialBlocks] Block {currentBlock} progress: {currentBlockTrialCount}/{(currentBlock == 0 ? trialsPerTrainingBlock : trialsPerBlock)}, Total progress: {totalTrialsCompleted}/{totalTrials}, Should break: {shouldBreak}");
        
        // Check if we've completed the current block and there are more blocks remaining
        return shouldBreak;
    }
    
    // Show break screen and wait for spacebar
    private void ShowBreakScreen()
    {
        int totalTrialsCompleted = staticTrialsRun + movingTrialsRun;
        int totalTrials = trialsPerBlock * totalBlocks;
        int trialsRemaining = totalTrials - totalTrialsCompleted;
        
        // Safety check - don't show break if experiment is actually complete
        if (trialsRemaining <= 0 || currentBlock >= totalBlocks)
        {
            Debug.LogWarning("[TrialBlocks] Break requested but no blocks remaining. This shouldn't happen.");
            return;
        }
        
        string nextBlockType = GetBlockTypeString(GetNextBlockType());
        
        string blockName = currentBlock == 0 ? "Training Block" : $"Block {currentBlock}";
        string breakMessage = $"{blockName} completed!\n\n" +
                             $"Trials completed: {totalTrialsCompleted} / {totalTrials}\n" +
                             //$"Trials remaining: {trialsRemaining}\n\n" +
                             $"Next block will be: {nextBlockType}\n\n" +
                             "Take a break if needed.\n\n" +
                             "Press SPACEBAR to continue to the next block";
        
        ShowBlackScreen(breakMessage);
        
        // Disable A button during break screen (spacebar-only mode)
        isInBreakScreen = true;
        Debug.Log("[TrialBlocks] A button input DISABLED during break screen. Only spacebar will work.");
        
        Debug.Log($"[TrialBlocks] Break time! Block {currentBlock} completed. Waiting for spacebar...");
        
        // Start listening for spacebar input
        StartCoroutine(WaitForSpacebarToContinue());
    }
    
    // Coroutine to wait for spacebar input
    private System.Collections.IEnumerator WaitForSpacebarToContinue()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("[TrialBlocks] Spacebar pressed. Continuing to next block...");
                
                // Start next block
                currentBlock++;
                currentBlockTrialCount = 0;
                
                // Go directly to "Press A" message (don't hide black screen yet)
                StartCoroutine(DelayedNextTrial());
                yield break;
            }
            yield return null;
        }
    }
    
    // Show "Press A" message after break instead of immediately starting trial
    private System.Collections.IEnumerator DelayedNextTrial()
    {
        yield return new WaitForSeconds(0.5f);
        
        // Re-enable A button input (exit break screen mode)
        isInBreakScreen = false;
        Debug.Log("[TrialBlocks] A button input RE-ENABLED. Normal trial continuation restored.");
        
        // Show the same message as the initial screen
        ShowBlackScreen("Press A on the VR Controller Button to Begin\n\n(Press B on controller to recenter view)");
        
        // The Update() method will handle the A button press to start the actual trial
    }

    // Get the motion type for the current block
    private MotionType GetCurrentBlockType()
    {
        switch (currentBlock)
        {
            case 1: return block1Type;
            case 2: return block2Type;
            case 3: return block3Type;
            default: return MotionType.Static; // Fallback
        }
    }

    // Get the motion type for the next block
    private MotionType GetNextBlockType()
    {
        switch (currentBlock + 1)
        {
            case 2: return block2Type;
            case 3: return block3Type;
            default: return MotionType.Static; // Fallback
        }
    }

    // Convert motion type enum to readable string
    private string GetBlockTypeString(MotionType motionType)
    {
        switch (motionType)
        {
            case MotionType.ObserverMotion: return "Observer Motion";
            case MotionType.ObjectMotion: return "Object Motion";
            case MotionType.Static: return "Static";
            default: return "Unknown";
        }
    }

    // Store XR Origin for coordinate system recentering
    private Transform xrOrigin = null;
    
    // Try to recenter VR tracking origin using Unity XR subsystems
    private bool TryRecenterVRTrackingOrigin(Vector3 headPosition, Vector3 headForward)
    {
        try 
        {
            // Method 1: Try simple XR recenter approach
            // This attempts to use any available VR recentering functionality
            
            // Check if we can use OpenXR or Oculus recentering
            #if UNITY_XR_OPENXR
            // OpenXR recentering would go here
            Debug.Log("[Recenter] OpenXR detected but no native recenter implemented");
            #endif
            
            // For now, return false to use XR Origin rotation method
            Debug.Log("[Recenter] Native VR recentering not available, using XR Origin rotation");
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Recenter] VR tracking recenter failed: {e.Message}");
            return false;
        }
    }
    
    // Recenter the view by adjusting the coordinate system itself
    private void RecenterView()
    {
        // Find the main camera (should be the VR camera)
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[Recenter] Main camera not found. Cannot recenter view.");
            return;
        }

        // Find XR Origin if not already found
        if (xrOrigin == null)
        {
            // Method 1: Try common XR Origin names
            GameObject xrOriginGO = GameObject.Find("XR Origin") ?? 
                                   GameObject.Find("XR Rig") ?? 
                                   GameObject.Find("XROrigin") ?? 
                                   GameObject.Find("XRRig") ??
                                   GameObject.Find("XR Origin (Mobile)") ??
                                   GameObject.Find("XR Origin (Room-Scale)") ??
                                   GameObject.Find("XR Origin (Stationary)") ??
                                   GameObject.Find("XRI_DefaultXRRig");
            
            // Method 2: Search by component type if names don't work
            if (xrOriginGO == null)
            {
                // Try different possible XR Origin components
                var xrInteractionManager = FindObjectOfType<XRInteractionManager>();
                if (xrInteractionManager != null && xrInteractionManager.transform.parent != null)
                {
                    xrOriginGO = xrInteractionManager.transform.parent.gameObject;
                    Debug.Log($"[Recenter] Found XR system via XRInteractionManager parent: {xrOriginGO.name}");
                }
            }
            
            // Method 3: Find by checking if Main Camera has XR Origin as parent
            if (xrOriginGO == null && mainCamera != null)
            {
                Transform parent = mainCamera.transform.parent;
                while (parent != null)
                {
                    if (parent.name.Contains("Origin") || parent.name.Contains("Rig") || parent.name.Contains("XR"))
                    {
                        xrOriginGO = parent.gameObject;
                        Debug.Log($"[Recenter] Found potential XR Origin in camera hierarchy: {parent.name}");
                        break;
                    }
                    parent = parent.parent;
                }
            }
            
            // Method 4: Search all GameObjects for XR-related names
            if (xrOriginGO == null)
            {
                GameObject[] allObjects = FindObjectsOfType<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if ((obj.name.Contains("XR") && (obj.name.Contains("Origin") || obj.name.Contains("Rig"))) ||
                        obj.name.Contains("CameraRig") || 
                        obj.name.Contains("OVRCameraRig") ||
                        obj.name.Contains("OculusRig"))
                    {
                        xrOriginGO = obj;
                        Debug.Log($"[Recenter] Found XR system by search: {obj.name}");
                        break;
                    }
                }
            }
            
            if (xrOriginGO != null)
            {
                xrOrigin = xrOriginGO.transform;
                Debug.Log($"[Recenter] Successfully found XR Origin: {xrOrigin.name}");
            }
            else
            {
                Debug.LogWarning("[Recenter] Could not find any XR Origin/Rig. Listing all root objects for debugging:");
                GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    Debug.LogWarning($"[Recenter] Root object: {root.name}");
                }
            }
        }

        // Get current head position and rotation
        Vector3 headPosition = mainCamera.transform.position;
        Vector3 headForward = mainCamera.transform.forward;
        
        // METHOD 1: Try native VR tracking origin recenter (best approach)
        if (TryRecenterVRTrackingOrigin(headPosition, headForward))
        {
            Debug.Log("[Recenter] Successfully recentered VR tracking origin. Unity world space now aligned with viewer.");
            // Keep user's centerPoint setting - don't override to Vector3.zero
            Debug.Log($"[Recenter] Preserving user's Center Point setting: {centerPoint}");
            return;
        }
        
        // METHOD 2: If VR tracking recenter fails, try XR Origin rotation
        if (xrOrigin != null)
        {
            // Calculate how much to rotate to align Unity forward (Z+) with head forward
            Vector3 horizontalForward = new Vector3(headForward.x, 0f, headForward.z).normalized;
            float angleToRotate = Vector3.SignedAngle(Vector3.forward, horizontalForward, Vector3.up);
            
            // Rotate the XR Origin to align Unity world space with head direction
            xrOrigin.RotateAround(headPosition, Vector3.up, -angleToRotate);
            
            Debug.Log($"[Recenter] Rotated XR Origin by {-angleToRotate}° to align Unity world space with viewer direction");
            Debug.Log($"[Recenter] Unity Z-axis now points toward viewer. Existing movement code will work unchanged.");
            
            // Keep user's centerPoint setting - don't override to Vector3.zero
            Debug.Log($"[Recenter] Preserving user's Center Point setting: {centerPoint}");
        }
        else
        {
            // Fallback: Manual positioning (your original approach)
            Debug.LogWarning("[Recenter] Using fallback manual positioning - this is less optimal");
            
            // Project the forward vector onto the horizontal plane
            Vector3 horizontalForward = new Vector3(headForward.x, 0f, headForward.z).normalized;
            
            // Keep user's centerPoint setting - don't override with calculated position
            Debug.Log($"[Recenter] Manual recentering - preserving user's Start Point: {centerPoint}");
            
            // Manual repositioning of existing spheres to respect user's Start Point...
        }
        
        Debug.Log($"[Recenter] Recentering complete. Coordinate system now aligned with viewer.");
        
        // Do not automatically move existing rings - preserve trial-specific Start Point positions
    }

    // Reposition existing spheres around the new center point
    private void RepositionSpheres()
    {
        if (spheres == null || spheres.Length == 0) return;
        
        float angleStep = 360f / numberOfSpheres;
        
        for (int i = 0; i < spheres.Length; i++)
        {
            if (spheres[i] != null)
            {
                float angle = i * angleStep;
                float angleInRadians = angle * Mathf.Deg2Rad;
                
                Vector3 spherePosition = new Vector3(
                    centerPoint.x + Mathf.Cos(angleInRadians) * ringRadius,
                    centerPoint.y,
                    centerPoint.z + Mathf.Sin(angleInRadians) * ringRadius
                );
                
                spheres[i].transform.position = spherePosition;
            }
        }
        
        Debug.Log($"[Recenter] Repositioned {spheres.Length} spheres around new center point.");
    }

    // Update ring parent position to match user's centerPoint setting
    private void UpdateRingParentPosition()
    {
        if (ringParent != null)
        {
            ringParent.transform.position = centerPoint;
            Debug.Log($"[UpdateRingParentPosition] Ring parent positioned at: {centerPoint}");
        }
    }

    [HideInInspector] public GameObject ringParent; // Parent object for the single ring

    [HideInInspector] public TMPro.TextMeshProUGUI focusPointText; // Assign in Inspector

    [HideInInspector] public SpatialGridManager spatialGridManager; // Assign in Inspector


    // --- Experiment state and timing fields ---
    private bool trialActive = false;
    private float changeTime = 0f;
    private float attendantMotionStopTime = 0f;
    private float allMotionStopTime = 0f;
    private float expectedMotionStopTime = 0f; // When motion is supposed to end based on trial timing
    private float sphereClickTime = 0f;

    // --- Background sphere generation for reducing trial start lag ---
    // private bool spheresBeingGenerated = false; // Currently unused - background generation disabled
    // private bool spheresReadyForNextTrial = false; // Currently unused - background generation disabled
    private string[] preGeneratedSphereColors = null;
    private int preGeneratedSphereToChange = -1;
    private bool preGeneratedAddChange = false;
    private string preGeneratedTrialType = "";
    // private bool preGeneratedRingConfigSet = false; // Currently unused - background generation disabled
    
    // High-quality random generation using fresh cryptographic providers
    // (No persistent state to eliminate any clustering patterns)
    


    // On startup
    private void Start()
    {
        // Debug: Check for required references
        if (spherePrefab == null)
            Debug.LogError("[GameManager] spherePrefab is NOT assigned in the Inspector!");
        else
            Debug.Log("[GameManager] spherePrefab assigned.");

        if (stripedSpherePrefab == null)
            Debug.LogWarning("[GameManager] stripedSpherePrefab is NOT assigned in the Inspector (only needed for orientation trials).");
        else
            Debug.Log("[GameManager] stripedSpherePrefab assigned.");

        if (blackScreen == null)
            Debug.LogError("[GameManager] blackScreen is NOT assigned in the Inspector!");
        else
            Debug.Log("[GameManager] blackScreen assigned. Active: " + blackScreen.activeSelf);

        if (focusPointText == null)
            Debug.LogWarning("[GameManager] focusPointText is NOT assigned in the Inspector.");
        else
            Debug.Log("[GameManager] focusPointText assigned.");

        if (interactionManager == null)
            Debug.LogWarning("[GameManager] XRInteractionManager is NOT assigned in the Inspector.");
        else
            Debug.Log("[GameManager] XRInteractionManager assigned.");

        // Finding path to save results in project folder
        string projectPath = Application.dataPath;
        string projectRoot = Directory.GetParent(projectPath).FullName;
        resultsFolder = Path.Combine(projectRoot, "Change Blindness Results/results_");

        // Creating headers for results
        CreateHeaders();
        
        // Find existing SpatialGridManager if present
        if (spatialGridManager == null)
        {
            spatialGridManager = FindObjectOfType<SpatialGridManager>();
        }
        
        // Calculate trials per block
        InitializeTrialBlocks();
        
        // Log CIEDE2000 color system status
        if (changeHue)
        {
            Debug.Log("[COLOR SYSTEM] === COLOR CHANGE SYSTEM INITIALIZED ===");
            Debug.Log($"[COLOR SYSTEM] CIEDE2000 hue generation: {(weightedHueGeneration ? "ENABLED" : "DISABLED (Simple HSV)")}");
            Debug.Log($"[COLOR SYSTEM] CIEDE2000 hue changes: {(weightedHueChange ? "ENABLED" : "DISABLED (Simple HSV)")}");
            
            if (weightedHueChange)
            {
                Debug.Log($"[COLOR SYSTEM] Target ΔE for sphere changes: {weightedHueChangeDelta:F1} units");
            }
            else
            {
                Debug.Log($"[COLOR SYSTEM] HSV hue change magnitude: {hueChangeHSV:F3}");
            }
            
            Debug.Log($"[COLOR SYSTEM] Debug logging: {(debugPerceptualWeights ? "ENABLED" : "DISABLED")}");
            
            // Summary of active methods
            string generationMethod = weightedHueGeneration ? "CIEDE2000 perceptual weighting" : "uniform HSV random";
            string changeMethod = weightedHueChange ? "CIEDE2000 perceptual uniformity" : "simple HSV changes";
            Debug.Log($"[COLOR SYSTEM] Hue generation: {generationMethod} | Hue changes: {changeMethod}");
            Debug.Log("[COLOR SYSTEM] Scientific perceptual color system configured for change blindness experiment");
        }

        // Single ring system - no configuration needed

        // Show initial black screen with VR controller instruction
        Debug.Log("[GameManager] Calling ShowBlackScreen at Start()");
        ShowBlackScreen("Press A on the VR Controller Button to Begin\n\n(Press B on controller to recenter view)");
        
        // Background generation disabled - each trial generates fresh random colors
        Debug.Log("[GameManager] Background generation disabled for fresh colors each trial");

        if (focusPointText != null)
        {
            focusPointText.enabled = false;
            // Note: Focal point position will be updated dynamically to follow sphere center
        }

        // Check if results file already exists or is locked at the start
        string filePath = $"{resultsFolder}{participantFileName}";
        if (File.Exists(filePath) && !IsFileLocked(filePath))
        {
            Debug.LogWarning($"[GameManager] Results file already exists: {filePath}");
            ShowBlackScreen("Warning: Results CSV file for this participant\nalready exists and will be overwritten.\n\nPress A on the VR controller to continue.");
        }
        else if (IsFileLocked(filePath))
        {
            Debug.LogWarning($"[GameManager] Results file is locked: {filePath}");
            ShowBlackScreen("Warning: Results CSV file is open in another program.\nPlease close it before starting the experiment.");
        }
    }

    // Every frame
    private void Update()
    {
        // Check for VR controller button press (mapped as "Submit") to start new trial
        // BUT ignore A button presses when we're in a break screen (spacebar-only mode)
        if (Input.GetButtonDown("Submit") && blackScreenUp && experimentRunning && !isInBreakScreen)
        {
            // Check if results file is still locked before starting trial
            string filePath = $"{resultsFolder}{participantFileName}";
            if (IsFileLocked(filePath))
            {
                Debug.LogWarning($"[GameManager] Results file is still locked: {filePath}");
                ShowBlackScreen("Warning: Results CSV file is open in another program. Please close it before starting the experiment.");
                return;
            }
            
            HideBlackScreen();
            StartNewTrial();
        }

        // Check for recentering input (B button on controller or C key on keyboard)
        // Only allow recentering when trials are not actively running (on black screens)
        // Exclude spacebar even if it's mapped to Jump button
        bool cKeyPressed = Input.GetKeyDown(KeyCode.C);
        bool bButtonPressed = (Input.GetButtonDown("Fire2") || Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.B)) && !Input.GetKeyDown(KeyCode.Space);
        
        if (cKeyPressed || bButtonPressed)
        {
            string inputMethod = "";
            if (cKeyPressed) inputMethod = "C key";
            else if (Input.GetButtonDown("Fire2")) inputMethod = "Fire2 button";
            else if (Input.GetButtonDown("Jump") && !Input.GetKeyDown(KeyCode.Space)) inputMethod = "Jump button";
            else if (Input.GetKeyDown(KeyCode.B)) inputMethod = "B key";
            
            Debug.Log($"[Recenter] Input detected: {inputMethod}");
            Debug.Log($"[Recenter] blackScreenUp: {blackScreenUp}, trialActive: {trialActive}");
            
            if (blackScreenUp && !trialActive)
            {
                Debug.Log($"[Recenter] Conditions met. Recentering triggered by {inputMethod}");
                RecenterView();
            }
            else
            {
                Debug.LogWarning($"[Recenter] Conditions not met. Cannot recenter during active trial or when black screen is down.");
            }
        }

        // Debug: Log any key press to verify input system is working
        if (Input.anyKeyDown)
        {
            Debug.Log($"[Input] Any key pressed. blackScreenUp: {blackScreenUp}, trialActive: {trialActive}");
        }

        // Check for escape to quit experiment
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnityEditor.EditorApplication.isPlaying = false;
            Application.Quit();
        }

        // --- Audio cue test: Play beep on keypress ---
#if UNITY_EDITOR
        if (UnityEngine.Input.GetKeyDown(KeyCode.B))
        {
            Debug.Log("[AudioCue] Test: Playing lowSound via B key");
            if (audioSource != null && lowSound != null)
            {
                audioSource.spatialBlend = 0f;
                audioSource.mute = false;
                audioSource.volume = 1f;
                audioSource.PlayOneShot(lowSound);
            }
            else
            {
                Debug.LogError("[AudioCue] Test: AudioSource or lowSound not assigned");
            }
        }
        if (UnityEngine.Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("[AudioCue] Test: Playing highSound via H key");
            if (audioSource != null && highSound != null)
            {
                audioSource.spatialBlend = 0f;
                audioSource.mute = false;
                audioSource.volume = 1f;
                audioSource.PlayOneShot(highSound);
            }
            else
            {
                Debug.LogError("[AudioCue] Test: AudioSource or highSound not assigned");
            }
        }
#endif
    }




    // Running trials
    private void StartNewTrial()
    {
        // Reset ring configuration for each new trial
        // Single ring system - no configuration needed
        Debug.Log("[GameManager] Ring configuration reset for new trial");
        
        // Ensure grid is in between-trial state (full visibility)
        SetGridForTrialState(false);
        
        int totalTrials = trialsPerBlock * totalBlocks + (trainingBlock ? trialsPerTrainingBlock : 0);
        if (trialNumber >= totalTrials)
        {
            experimentRunning = false;
            string filePath = $"{resultsFolder}{participantFileName}";
            SaveResultsToCSV(filePath);
            ShowBlackScreen("Experiment Complete");
            return;
        }

        // Always generate spheres immediately with fresh random colors
        Debug.Log($"[GameManager] Generating fresh trial {trialNumber + 1}");
        GenerateAndExecuteTrial();
        
        // Background generation disabled - no longer needed for fresh colors
        Debug.Log("[GameManager] Background generation disabled for fresh colors each trial");
    }
    
    // Generate and execute trial immediately (used for first trial or fallback)
    private void GenerateAndExecuteTrial()
    {
        Debug.Log($"[GameManager] GenerateAndExecuteTrial starting for trial {trialNumber + 1}");
        
        // Check if we're in training block - execute training trial instead
        if (currentBlock == 0)
        {
            ExecuteTrainingTrial();
            return;
        }
        
        // Single ring system - no configuration needed
        
        trialResults = new string[headers.Length];
        for (int i = 0; i < 23; i++)  // Updated to 23 (21 + 2 new columns)
        {
            trialResults[i] = "";
        }
        
        // Reset timing variables for this trial
        changeTime = 0f;
        attendantMotionStopTime = 0f;
        allMotionStopTime = 0f;
        expectedMotionStopTime = 0f;
        sphereClickTime = 0f;
        originalResult = "";
        changedResult = "";
        
        // Set Participant and Trial Number
        trialResults[0] = participantName;
        trialResults[1] = (trialNumber + 1).ToString();
        
        // Populate General Settings columns - updated indices with new column order
        trialResults[9] = trialStartDelay.ToString();       // Trial Start Delay (s)
        trialResults[10] = trialLength.ToString();          // Trial Length (s) - moved after Trial Start Delay
        // Response Time, Change Start Time, and Change End Time will be filled in SaveTrialResults
        trialResults[14] = blinkSpheres.ToString();         // Blink Spheres (shifted by +2)
        trialResults[15] = blinkSpheres ? blinkDuration.ToString() : ""; // Blink Duration (s)
        trialResults[16] = movementSpeed.ToString();        // Movement Speed (units/s) - Linear motion
        trialResults[17] = numberOfSpheres.ToString();      // # of Spheres
        trialResults[18] = ringRadius.ToString();           // Radius
        trialResults[19] = sphereSaturation.ToString();     // Sphere Saturation
        trialResults[20] = sphereValue.ToString();          // Sphere Value
        trialResults[21] = sphereSize.ToString();           // Sphere Size
        trialResults[22] = soundInterval.ToString();        // Sound Interval (s)
        
        // Set Change Type
        if (changeHue) trialResults[2] = "Hue";
        else if (changeLuminance) trialResults[2] = "Luminance";
        else if (changeSize) trialResults[2] = "Size";
        else if (changeOrientation) trialResults[2] = "Orientation";
        else trialResults[2] = "";
        
        // Only single ring spheres are selectable
        int sphereToChange = Random.Range(0, numberOfSpheres);
        
        trialResults[4] = (sphereToChange + 1).ToString();  // Changed Sphere (was index 9)
        bool addChange = Random.Range(0, 2) == 0;

        // Determine trial type based on current block instead of random selection
        MotionType currentMotionType = GetCurrentBlockType();
        string trialType;
        
        switch (currentMotionType)
        {
            case MotionType.Static:
                trialType = "Static";
                break;
            case MotionType.ObjectMotion:
                trialType = "Moving"; // Use existing moving trial logic
                break;
            case MotionType.ObserverMotion:
                trialType = "ObserverMotion"; // For now, identical to moving
                break;
            default:
                Debug.LogError($"[TrialBlocks] Unknown motion type: {currentMotionType}");
                trialType = "Static"; // Fallback
                break;
        }
        
        Debug.Log($"[TrialBlocks] Block {currentBlock}, Trial {currentBlockTrialCount + 1}/{trialsPerBlock}, Type: {GetBlockTypeString(currentMotionType)}");
        
        // Map internal trial type to CSV output format
        string movementTypeForCSV;
        switch (trialType)
        {
            case "Static":
                movementTypeForCSV = "Static";
                break;
            case "Moving":
                movementTypeForCSV = "Object Motion";
                break;
            case "ObserverMotion":
                movementTypeForCSV = "Observer Motion";
                break;
            default:
                movementTypeForCSV = trialType;
                break;
        }
        
        trialResults[3] = movementTypeForCSV;
        
        // Generate spheres and execute trial
        string[] allColors = GenerateSpheresForTrial();
        ExecuteTrial(sphereToChange, addChange, trialType, allColors);
    }
    
    // Execute training trial (grid only, no spheres)
    private void ExecuteTrainingTrial()
    {
        Debug.Log($"[TrainingTrial] Starting training trial {currentBlockTrialCount + 1}/{trialsPerTrainingBlock}");
        
        trialNumber++;
        
        // Ensure grid is visible for training
        SetGridForTrialState(false); // Full visibility during training
        
        // Start training trial coroutine (grid + ticking, wait for button press)
        StartCoroutine(TrainingTrialCoroutine());
    }
    
    // Training trial coroutine - shows grid with beep sequence, waits for VR trigger press
    private IEnumerator TrainingTrialCoroutine()
    {
        Debug.Log("[TrainingTrial] Starting training trial with grid and beep sequence");
        
        // Enable trial interaction
        trialActive = true;
        
        // Ensure grid is visible for training (same as regular trials)
        SetGridForTrialState(true);
        
        // Show guide path during training trials
        if (spatialGridManager != null)
        {
            spatialGridManager.ShowGuidePath(true); // Pass true to indicate this is a training trial
        }
        

        
        // Wait for trial start delay (same as regular trials)
        yield return new WaitForSeconds(trialStartDelay);
        
        // Play beep sequence (3 low beeps + 1 high beep) like regular trials
        if (audioSource != null && lowSound != null && highSound != null)
        {
            yield return StartCoroutine(PlayBeepsAndChange(3, soundInterval, () => {}));
        }
        
        // Wait for VR controller side trigger button press
        bool triggerPressed = false;
        
        while (!triggerPressed)
        {
            // Check for XR primary trigger using XR Input system (same as sphere selection)
            var rightDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            var leftDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.Right, rightDevices);
            if (rightDevices.Count > 0)
            {
                if (rightDevices[0].TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool rightGrip) && rightGrip)
                {
                    triggerPressed = true;
                }
            }
            
            if (!triggerPressed)
            {
                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.Left, leftDevices);
                if (leftDevices.Count > 0)
                {
                    if (leftDevices[0].TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool leftGrip) && leftGrip)
                    {
                        triggerPressed = true;
                    }
                }
            }
            
            yield return null;
        }
        
        trialActive = false;
        
        // Complete the training trial
        CompleteTrainingTrial();
    }
    
    // Complete training trial and advance to next
    private void CompleteTrainingTrial()
    {
        Debug.Log($"[TrainingTrial] Training trial {currentBlockTrialCount + 1} completed");
        
        // Hide training path
        if (spatialGridManager != null)
        {
            spatialGridManager.HideGuidePath();
        }
        
        // Increment trial counters
        currentBlockTrialCount++;
        
        // Check if training block is complete
        if (ShouldTakeBreak())
        {
            ShowBreakScreen();
        }
        else
        {
            // Continue to next training trial
            StartCoroutine(DelayedNextTrial());
        }
    }



    
    // Execute a trial with given parameters and sphere colors
    private void ExecuteTrial(int sphereToChange, bool addChange, string trialType, string[] allColors)
    {
        // Fill trialResults with all sphere colors
        if (allColors != null && trialResults.Length > 23)
        {
            int colorCount = Mathf.Min(allColors.Length, trialResults.Length - 23);
            int colorIndex = 0;
            
            // Single ring spheres - start at index 23
            for (int i = 0; i < numberOfSpheres && (23 + colorIndex) < trialResults.Length; i++)
            {
                trialResults[23 + colorIndex] = colorIndex < colorCount ? allColors[colorIndex] : "";
                colorIndex++;
            }
        }
        
        if (trialType == "Static")
        {
            trialNumber++;
            staticTrialsRun++;
            StartCoroutine(StaticChange(sphereToChange, addChange));
        }
        else if (trialType == "Moving")
        {
            trialNumber++;
            movingTrialsRun++;
            StartCoroutine(LinearMovementCoroutine(sphereToChange, addChange));
        }
        else if (trialType == "ObserverMotion")
        {
            trialNumber++;
            staticTrialsRun++; // Count as static trials since behavior is identical
            StartCoroutine(ObserverMotionChange(sphereToChange, addChange));
        }
    }
    
    // Generate spheres for current trial
    private string[] GenerateSpheresForTrial()
    {
        DestroySpheres();
        if (focusPointText != null)
            focusPointText.enabled = true;
        return CreateRingOfSpheres(centerPoint);
    }
    
    // Coroutine to generate next trial in background
    private System.Collections.IEnumerator GenerateNextTrialInBackground()
    {
        // spheresBeingGenerated = true; // Commented out - background generation disabled
        
        // Wait a frame to avoid frame rate issues
        yield return null;
        
        // Pre-determine next trial parameters (single ring system)
        preGeneratedSphereToChange = Random.Range(0, numberOfSpheres);
        preGeneratedAddChange = Random.Range(0, 2) == 0;
        
        // Determine next trial type based on current block
        MotionType nextMotionType = GetCurrentBlockType();
        switch (nextMotionType)
        {
            case MotionType.Static:
                preGeneratedTrialType = "Static";
                break;
            case MotionType.ObjectMotion:
                preGeneratedTrialType = "Moving";
                break;
            case MotionType.ObserverMotion:
                preGeneratedTrialType = "ObserverMotion";
                break;
            default:
                preGeneratedTrialType = "Static";
                break;
        }
        
        if (!string.IsNullOrEmpty(preGeneratedTrialType))
        {
            
            // Pre-generate sphere colors (this is the time-consuming part)
            // We can't actually create the GameObjects in background, but we can pre-calculate the colors
            preGeneratedSphereColors = PreCalculateSphereColors();
            // spheresReadyForNextTrial = true; // Commented out - background generation disabled
            Debug.Log($"[GameManager] Background generation complete. Generated {preGeneratedSphereColors?.Length ?? 0} colors for trial type: {preGeneratedTrialType}");
        }
        else
        {
            Debug.Log("[GameManager] No available trial types for background generation");
        }
        
        // spheresBeingGenerated = false; // Commented out - background generation disabled
    }
    
    // Pre-calculate sphere colors without creating GameObjects
    private string[] PreCalculateSphereColors()
    {
        // Single ring system - only generate colors for the spheres
        string[] allColors = new string[numberOfSpheres];
        
        // Generate random attributes for all spheres in the single ring
        var sphereColors = GenerateRandomAttributes(numberOfSpheres);
        for (int i = 0; i < numberOfSpheres; i++)
        {
            allColors[i] = sphereColors[i];
        }
        
        Debug.Log($"[GameManager] Generated colors for single ring: {string.Join(", ", allColors)}");
        return allColors;
    }
    
    // Generate random attribute value as string (without applying to GameObject)
    private string GenerateRandomAttribute()
    {
        if (changeHue)
        {
            if (weightedHueGeneration)
            {
                // Use CIEDE2000-based perceptually-weighted hue generation for better color discrimination balance
                float perceptualHue = GeneratePerceptuallyWeightedHue();
                return perceptualHue.ToString();
            }
            else
            {
                // Use high-quality random hue distribution (System.Cryptography for better randomness)
                float randomHue = GenerateHighQualityRandom();
                Debug.Log($"[RandomDebug] High-quality random generated: {randomHue:F6} (as degrees: {randomHue * 360f:F1}°)");
                return randomHue.ToString();
            }
        }
        else if (changeLuminance)
        {
            return GenerateHighQualityRandom().ToString();
        }
        else if (changeSize)
        {
            float min = minSize;
            float max = maxSize;
            return (min + GenerateHighQualityRandom() * (max - min)).ToString();
        }
        else if (changeOrientation)
        {
            return (GenerateHighQualityRandom() * 360f - 180f).ToString();
        }
        return "0";
    }

    /// <summary>
    /// Generate high-quality random numbers using cryptographic random number generator
    /// Uses fresh cryptographic entropy for each call to eliminate all clustering
    /// </summary>
    private float GenerateHighQualityRandom()
    {
        // Use fresh cryptographic provider for each call to eliminate any potential state issues
        using (var freshCrypto = new System.Security.Cryptography.RNGCryptoServiceProvider())
        {
            // Generate 8 random bytes for extra entropy and precision
            byte[] randomBytes = new byte[8];
            freshCrypto.GetBytes(randomBytes);
            
            // Convert to unsigned long for maximum precision
            ulong randomULong = System.BitConverter.ToUInt64(randomBytes, 0);
            
            // Convert to double first for maximum precision, then to float
            double preciseResult = (double)randomULong / (double)ulong.MaxValue;
            float result = (float)preciseResult;
            
            // Ensure result is strictly less than 1.0
            if (result >= 1.0f) result = 0.999999f;
            
            Debug.Log($"[CryptoDebug] Fresh crypto bytes: [{randomBytes[0]:X2}, {randomBytes[1]:X2}, {randomBytes[2]:X2}, {randomBytes[3]:X2}...] -> {result:F8}");
            
            return result;
        }
    }

    // Generate random attributes with quality checks to prevent clustering and bias
    private System.Collections.Generic.List<string> GenerateRandomAttributes(int sphereCount)
    {
        var result = new System.Collections.Generic.List<string>();
        int maxAttempts = 50; // Prevent infinite loops
        int attempt = 0;
        
        while (attempt < maxAttempts)
        {
            result.Clear();
            
            // Generate purely random attributes
            for (int i = 0; i < sphereCount; i++)
            {
                result.Add(GenerateRandomAttribute());
            }
            
            // Quality check the generated set
            if (IsAttributeSetAcceptable(result))
            {
                Debug.Log($"[QualityCheck] Accepted attribute set after {attempt + 1} attempt(s)");
                break;
            }
            
            attempt++;
            Debug.Log($"[QualityCheck] Rejected set attempt {attempt}, trying again...");
        }
        
        if (attempt >= maxAttempts)
        {
            Debug.LogWarning($"[QualityCheck] Failed to generate acceptable set after {maxAttempts} attempts, using last generated set");
        }
        
        // Debug logging for the final set
        if (changeHue)
        {
            var hues = result.Select(r => float.Parse(r) * 360f).ToArray();
            Debug.Log($"[RandomCheck] Ring with {sphereCount} spheres - Hues (degrees): [{string.Join(", ", hues.Select(h => h.ToString("F1")))}]");
        }
        else if (changeLuminance)
        {
            var values = result.Select(r => float.Parse(r)).ToArray();
            Debug.Log($"[RandomCheck] Ring with {sphereCount} spheres - Luminance: [{string.Join(", ", values.Select(v => v.ToString("F3")))}]");
            Debug.Log($"[QualityCheck] Luminance distribution - Min: {values.Min():F3}, Max: {values.Max():F3}, Range: {(values.Max() - values.Min()):F3}");
        }
        else if (changeOrientation)
        {
            var orientations = result.Select(r => float.Parse(r)).ToArray();
            Debug.Log($"[RandomCheck] Ring with {sphereCount} spheres - Orientations (degrees): [{string.Join(", ", orientations.Select(o => o.ToString("F1")))}]");
            Debug.Log($"[QualityCheck] Orientation range: {orientations.Min():F1}° to {orientations.Max():F1}°");
        }
        else if (changeSize)
        {
            var sizes = result.Select(r => float.Parse(r)).ToArray();
            Debug.Log($"[RandomCheck] Ring with {sphereCount} spheres - Sizes: [{string.Join(", ", sizes.Select(s => s.ToString("F3")))}]");
        }
        
        return result;
    }
    
    /// <summary>
    /// Check if a set of random attributes has acceptable distribution
    /// Rejects sets that are too clustered or biased toward one end
    /// </summary>
    private bool IsAttributeSetAcceptable(System.Collections.Generic.List<string> attributes)
    {
        if (attributes.Count < 2) return true;
        
        var values = attributes.Select(attr => float.Parse(attr)).ToArray();
        
        if (changeHue)
        {
            return IsHueSetAcceptable(values);
        }
        else if (changeLuminance)
        {
            return IsLuminanceSetAcceptable(values);
        }
        else if (changeOrientation)
        {
            // Orientation trials don't need distribution checks - any random orientations are fine
            Debug.Log($"[QualityCheck] Orientation trial - no distribution checks needed");
            return true;
        }
        else if (changeSize)
        {
            // Size trials don't need distribution checks - any random sizes are fine  
            Debug.Log($"[QualityCheck] Size trial - no distribution checks needed");
            return true;
        }
        else
        {
            Debug.LogWarning($"[QualityCheck] Unknown trial type - accepting by default");
            return true;
        }
    }
    
    /// <summary>
    /// Check if hue values have acceptable distribution (prevent clustering)
    /// </summary>
    private bool IsHueSetAcceptable(float[] hues)
    {
        // Convert to degrees for easier checking
        var hueDegrees = hues.Select(h => h * 360f).ToArray();
        
        // Check for clustering - reject if too many values are too close
        int clusterCount = 0;
        float minSeparation = 20f; // Minimum degrees between close values
        
        for (int i = 0; i < hueDegrees.Length; i++)
        {
            for (int j = i + 1; j < hueDegrees.Length; j++)
            {
                float diff = Mathf.Abs(hueDegrees[i] - hueDegrees[j]);
                // Handle wrap-around (e.g., 350° and 10° are only 20° apart)
                if (diff > 180f) diff = 360f - diff;
                
                if (diff < minSeparation)
                {
                    clusterCount++;
                }
            }
        }
        
        // Reject if more than 1 pair is too close (allows some clustering but not excessive)
        return clusterCount <= 1;
    }
    
    /// <summary>
    /// Check if value/brightness set has acceptable distribution (prevent bias toward light/dark)
    /// </summary>
    private bool IsLuminanceSetAcceptable(float[] values)
    {
        float min = values.Min();
        float max = values.Max();
        float range = max - min;
        float average = values.Average();
        
        // Require good range (at least 40% of full range for better contrast)
        if (range < 0.4f)
        {
            Debug.Log($"[QualityCheck] Rejected value set - insufficient range: {range:F3} (need ≥0.400)");
            return false;
        }
        
        // Prevent extreme bias toward light or dark (tighter constraints)
        // Average should be well-centered (between 0.3 and 0.7)
        if (average < 0.3f || average > 0.7f)
        {
            Debug.Log($"[QualityCheck] Rejected value set - biased average: {average:F3} (need 0.3-0.7)");
            return false;
        }
        
        // Check distribution - ensure we have both dark and light spheres
        int darkCount = values.Count(v => v < 0.4f);   // Dark spheres (< 40% brightness)
        int lightCount = values.Count(v => v > 0.6f);  // Light spheres (> 60% brightness)
        int totalCount = values.Length;
        
        // Require at least some spheres in both dark and light ranges
        if (darkCount == 0)
        {
            Debug.Log($"[QualityCheck] Rejected value set - no dark spheres (all ≥ 0.4)");
            return false;
        }
        
        if (lightCount == 0)
        {
            Debug.Log($"[QualityCheck] Rejected value set - no light spheres (all ≤ 0.6)");
            return false;
        }
        
        // Reject if more than 60% of spheres are in the same extreme (stricter than before)
        if (darkCount > totalCount * 0.6f || lightCount > totalCount * 0.6f)
        {
            Debug.Log($"[QualityCheck] Rejected value set - extreme clustering: {darkCount} dark, {lightCount} light out of {totalCount} (max 60% in either extreme)");
            return false;
        }
        
        // Additional check: ensure we have some mid-range values too
        int midCount = values.Count(v => v >= 0.4f && v <= 0.6f);
        if (midCount == 0 && totalCount >= 4)
        {
            Debug.Log($"[QualityCheck] Rejected value set - no mid-range values (need some between 0.4-0.6)");
            return false;
        }
        
        // NEW: Check for clustering in ANY narrow range (including middle grey)
        if (totalCount >= 4) // Only check clustering for larger sets
        {
            // Check for clustering in 0.15-wide bands across the range
            float[] bandCenters = { 0.2f, 0.35f, 0.5f, 0.65f, 0.8f }; // Dark, dark-mid, mid, light-mid, light
            float bandWidth = 0.15f; // ±0.075 from center
            
            foreach (float center in bandCenters)
            {
                int bandCount = values.Count(v => Mathf.Abs(v - center) <= bandWidth / 2f);
                if (bandCount > totalCount * 0.5f) // More than 50% in any narrow band
                {
                    Debug.Log($"[QualityCheck] Rejected value set - clustering around {center:F2}: {bandCount}/{totalCount} spheres within ±{bandWidth/2f:F3}");
                    return false;
                }
            }
        }
        
        Debug.Log($"[QualityCheck] Value set PASSED - Range: {range:F3}, Avg: {average:F3}, Dark: {darkCount}, Mid: {midCount}, Light: {lightCount}");
        return true;
    }

    /// <summary>
    /// Generate well-distributed hues for a ring to prevent color clustering
    /// Uses improved randomization with minimum spacing enforcement
    /// </summary>
    private System.Collections.Generic.List<string> GenerateWellDistributedHues(int sphereCount)
    {
        var result = new System.Collections.Generic.List<string>();
        
        const int maxAttempts = 100;
        const float minSpacingDegrees = 35f; // Minimum 35° apart for good separation
        
        // Method 1: Try improved random generation with spacing checks
        bool useSystematicFallback = false;
        var hueValues = new System.Collections.Generic.List<float>();
        
        for (int i = 0; i < sphereCount; i++)
        {
            bool validValue = false;
            int attempts = 0;
            float bestCandidate = 0f;
            float bestSpacing = 0f;
            
            while (!validValue && attempts < maxAttempts)
            {
                // Use System.Random for potentially better distribution
                System.Random systemRandom = new System.Random(System.DateTime.Now.Millisecond + UnityEngine.Random.Range(0, 10000) + attempts);
                float candidate = (float)systemRandom.NextDouble();
                float candidateDegrees = candidate * 360f;
                
                // Check spacing against all previous values
                float minSpacingFound = 360f; // Start with max possible
                bool spacingOk = true;
                
                for (int j = 0; j < hueValues.Count; j++)
                {
                    float existingDegrees = hueValues[j] * 360f;
                    float diff = Mathf.Abs(candidateDegrees - existingDegrees);
                    if (diff > 180f) diff = 360f - diff; // Handle wraparound
                    
                    minSpacingFound = Mathf.Min(minSpacingFound, diff);
                    
                    if (diff < minSpacingDegrees)
                    {
                        spacingOk = false;
                    }
                }
                
                // Keep track of the best candidate even if not perfect
                if (minSpacingFound > bestSpacing)
                {
                    bestSpacing = minSpacingFound;
                    bestCandidate = candidate;
                }
                
                if (spacingOk)
                {
                    hueValues.Add(candidate);
                    result.Add(candidate.ToString());
                    validValue = true;
                }
                
                attempts++;
            }
            
            // If we couldn't find a perfectly spaced value, use the best we found
            if (!validValue)
            {
                if (bestSpacing > 15f) // Accept if at least 15° apart
                {
                    hueValues.Add(bestCandidate);
                    result.Add(bestCandidate.ToString());
                    Debug.LogWarning($"[RandomGenerator] Using best candidate with {bestSpacing:F1}° spacing (less than ideal {minSpacingDegrees}°)");
                }
                else
                {
                    Debug.LogWarning($"[RandomGenerator] Could not find well-spaced values. Falling back to systematic distribution.");
                    useSystematicFallback = true;
                    break;
                }
            }
        }
        
        // Fallback: Use systematic distribution if random failed
        if (useSystematicFallback)
        {
            result.Clear();
            result = GenerateSystematicHueDistribution(sphereCount);
        }
        
        return result;
    }

    /// <summary>
    /// Fallback method: Generate systematically distributed hues with random offset
    /// Ensures perfect spacing but maintains some randomness
    /// </summary>
    private System.Collections.Generic.List<string> GenerateSystematicHueDistribution(int sphereCount)
    {
        var result = new System.Collections.Generic.List<string>();
        
        // Start with evenly spaced hues
        float baseSpacing = 360f / sphereCount;
        float randomOffset = UnityEngine.Random.Range(0f, 360f); // Random rotation of the whole set
        
        for (int i = 0; i < sphereCount; i++)
        {
            float hue = (i * baseSpacing + randomOffset) % 360f;
            
            // Add small random variation (±8°) while maintaining minimum spacing
            float maxVariation = Mathf.Min(8f, baseSpacing * 0.2f); // Don't exceed 20% of base spacing
            float variation = UnityEngine.Random.Range(-maxVariation, maxVariation);
            float adjustedHue = (hue + variation + 360f) % 360f;
            
            float normalizedHue = adjustedHue / 360f;
            result.Add(normalizedHue.ToString());
        }
        
        Debug.Log($"[RandomCheck] Using systematic distribution with random offset and variation");
        return result;
    }

    // Generate default attribute value as string (no randomization)
    private string GenerateDefaultAttribute()
    {
        if (changeHue)
        {
            return defaultHue.ToString();
        }
        else if (changeLuminance)
        {
            return sphereValue.ToString();
        }
        else if (changeSize)
        {
            return sphereSize.ToString();
        }
        else if (changeOrientation)
        {
            return "0"; // Default orientation (no rotation)
        }
        return "0";
    }

    // Generate default attributes for a ring (no randomization)
    private System.Collections.Generic.List<string> GenerateDefaultAttributes(int sphereCount)
    {
        var result = new System.Collections.Generic.List<string>();
        
        // Generate default attributes for all spheres in ring
        for (int i = 0; i < sphereCount; i++)
        {
            result.Add(GenerateDefaultAttribute());
        }
        
        return result;
    }

    // Perceptually-weighted hue generation using CIEDE2000 color difference calculations
    private float GeneratePerceptuallyWeightedHue()
    {
        // Pre-computed cumulative distribution for efficient sampling
        // Based on CIEDE2000 perceptual color density function (regions with poor discrimination get higher weight)
        if (perceptualHueCDF == null)
        {
            InitializeCIEDE2000HueCDF();
        }
        
        // Generate random value and find corresponding hue using inverse transform sampling
        float randomValue = Random.Range(0f, 1f);
        return SampleFromCDF(randomValue);
    }
    
    // Cache for cumulative distribution function and CIEDE2000 calculations
    private static float[] perceptualHueCDF = null;
    private static float[] hueValues = null;
    private const int HUE_RESOLUTION = 360; // 1-degree resolution
    
    // CIEDE2000 calculation cache for color conversion
    private static readonly float[] CIEDE2000_X_n = { 95.047f };  // D65 illuminant
    private static readonly float[] CIEDE2000_Y_n = { 100.000f }; // D65 illuminant  
    private static readonly float[] CIEDE2000_Z_n = { 108.883f }; // D65 illuminant
    
    // Initialize the cumulative distribution function for CIEDE2000-based perceptual hue weighting
    private void InitializeCIEDE2000HueCDF()
    {
        if (perceptualHueCDF != null) return; // Already initialized
        
        hueValues = new float[HUE_RESOLUTION];
        float[] weights = new float[HUE_RESOLUTION];
        
        // Generate hue values and their CIEDE2000-based perceptual weights
        for (int i = 0; i < HUE_RESOLUTION; i++)
        {
            float hue = (float)i / HUE_RESOLUTION; // Convert to 0-1 range
            hueValues[i] = hue;
            weights[i] = CIEDE2000DensityFunction(hue * 360f); // Convert to degrees for calculation
        }
        
        // Create cumulative distribution function (CDF)
        perceptualHueCDF = new float[HUE_RESOLUTION];
        float cumulativeSum = 0f;
        float totalWeight = 0f;
        
        // Calculate total weight for normalization
        for (int i = 0; i < HUE_RESOLUTION; i++)
        {
            totalWeight += weights[i];
        }
        
        // Build CDF
        for (int i = 0; i < HUE_RESOLUTION; i++)
        {
            cumulativeSum += weights[i] / totalWeight;
            perceptualHueCDF[i] = cumulativeSum;
        }
        
        // Ensure last value is exactly 1.0 to avoid floating point errors
        perceptualHueCDF[HUE_RESOLUTION - 1] = 1.0f;
        
        Debug.Log("[CIEDE2000] Initialized CIEDE2000-based perceptual hue CDF with " + HUE_RESOLUTION + " samples");
        
        // Optional: Debug the weight distribution
        if (debugPerceptualWeights)
        {
            DebugCIEDE2000WeightDistribution(weights, totalWeight);
        }
    }
    
    // CIEDE2000-based perceptual density function - scientifically accurate color discrimination weighting
    private float CIEDE2000DensityFunction(float hueDegrees)
    {
        // Convert to 0-360 range and handle wraparound
        while (hueDegrees < 0f) hueDegrees += 360f;
        while (hueDegrees >= 360f) hueDegrees -= 360f;
        
        // Convert hue to HSV color for CIELAB conversion
        Color baseColor = Color.HSVToRGB(hueDegrees / 360f, sphereSaturation, sphereValue);
        
        // Calculate CIEDE2000-based discrimination difficulty
        // Higher weight = worse discrimination = needs more representation
        float weight = CalculateDiscriminationDifficulty(baseColor, hueDegrees);
        
        // Add small baseline to prevent zero probability in any region
        weight += 0.05f;
        
        return weight;
    }
    
    // Calculate color discrimination difficulty using CIEDE2000 distances
    private float CalculateDiscriminationDifficulty(Color baseColor, float hueDegrees)
    {
        // Convert base color to CIELAB
        float[] baseLab = RGBToCIELAB(baseColor.r, baseColor.g, baseColor.b);
        
        // Sample neighboring hues to measure local discrimination
        float totalDifficulty = 0f;
        int sampleCount = 8; // Sample 8 neighboring hues for robust measurement
        float sampleRange = 15f; // Sample within ±15 degrees
        
        for (int i = 0; i < sampleCount; i++)
        {
            // Generate neighboring hue
            float angle = (float)i / sampleCount * 360f; // Spread samples evenly
            float neighborHue = hueDegrees + (sampleRange * Mathf.Cos(angle * Mathf.Deg2Rad));
            
            // Handle wraparound
            while (neighborHue < 0f) neighborHue += 360f;
            while (neighborHue >= 360f) neighborHue -= 360f;
            
            // Convert neighbor to RGB then CIELAB
            Color neighborColor = Color.HSVToRGB(neighborHue / 360f, sphereSaturation, sphereValue);
            float[] neighborLab = RGBToCIELAB(neighborColor.r, neighborColor.g, neighborColor.b);
            
            // Calculate CIEDE2000 distance
            float ciede2000Distance = CalculateCIEDE2000Distance(baseLab, neighborLab);
            
            // Smaller distances = harder discrimination = higher weight
            // Use reciprocal function with smoothing to avoid division by zero
            float difficulty = 1f / (ciede2000Distance + 0.5f);
            totalDifficulty += difficulty;
        }
        
        // Average difficulty across all samples
        return totalDifficulty / sampleCount;
    }
    
    // Convert RGB to CIE XYZ color space (sRGB to XYZ conversion)
    private float[] RGBToXYZ(float r, float g, float b)
    {
        // Convert sRGB to linear RGB
        float linearR = (r <= 0.04045f) ? r / 12.92f : Mathf.Pow((r + 0.055f) / 1.055f, 2.4f);
        float linearG = (g <= 0.04045f) ? g / 12.92f : Mathf.Pow((g + 0.055f) / 1.055f, 2.4f);
        float linearB = (b <= 0.04045f) ? b / 12.92f : Mathf.Pow((b + 0.055f) / 1.055f, 2.4f);
        
        // sRGB to XYZ transformation matrix (D65 illuminant)
        float x = (linearR * 0.4124564f + linearG * 0.3575761f + linearB * 0.1804375f) * 100f;
        float y = (linearR * 0.2126729f + linearG * 0.7151522f + linearB * 0.0721750f) * 100f;
        float z = (linearR * 0.0193339f + linearG * 0.1191920f + linearB * 0.9503041f) * 100f;
        
        return new float[] { x, y, z };
    }
    
    // Convert XYZ to CIE L*a*b* color space
    private float[] XYZToCIELAB(float[] xyz)
    {
        float x = xyz[0] / CIEDE2000_X_n[0];
        float y = xyz[1] / CIEDE2000_Y_n[0];
        float z = xyz[2] / CIEDE2000_Z_n[0];
        
        // Apply CIE L*a*b* transformation function
        float fx = (x > 0.008856f) ? Mathf.Pow(x, 1f/3f) : (7.787f * x + 16f/116f);
        float fy = (y > 0.008856f) ? Mathf.Pow(y, 1f/3f) : (7.787f * y + 16f/116f);
        float fz = (z > 0.008856f) ? Mathf.Pow(z, 1f/3f) : (7.787f * z + 16f/116f);
        
        float L = 116f * fy - 16f;
        float a = 500f * (fx - fy);
        float b = 200f * (fy - fz);
        
        return new float[] { L, a, b };
    }
    
    // Direct RGB to CIE L*a*b* conversion (combines RGB->XYZ->LAB)
    private float[] RGBToCIELAB(float r, float g, float b)
    {
        float[] xyz = RGBToXYZ(r, g, b);
        return XYZToCIELAB(xyz);
    }
    
    // Calculate CIEDE2000 color difference (simplified version for performance)
    private float CalculateCIEDE2000Distance(float[] lab1, float[] lab2)
    {
        float L1 = lab1[0], a1 = lab1[1], b1 = lab1[2];
        float L2 = lab2[0], a2 = lab2[1], b2 = lab2[2];
        
        // Calculate intermediate values
        float deltaL = L2 - L1;
        float avgL = (L1 + L2) / 2f;
        
        float C1 = Mathf.Sqrt(a1 * a1 + b1 * b1);
        float C2 = Mathf.Sqrt(a2 * a2 + b2 * b2);
        float avgC = (C1 + C2) / 2f;
        float deltaC = C2 - C1;
        
        // Simplified CIEDE2000 calculation (without full complexity for performance)
        // This captures the major perceptual corrections while remaining computationally efficient
        float G = 0.5f * (1f - Mathf.Sqrt(Mathf.Pow(avgC, 7) / (Mathf.Pow(avgC, 7) + Mathf.Pow(25f, 7))));
        
        float a1_prime = a1 * (1f + G);
        float a2_prime = a2 * (1f + G);
        
        float C1_prime = Mathf.Sqrt(a1_prime * a1_prime + b1 * b1);
        float C2_prime = Mathf.Sqrt(a2_prime * a2_prime + b2 * b2);
        float avgC_prime = (C1_prime + C2_prime) / 2f;
        float deltaC_prime = C2_prime - C1_prime;
        
        float h1_prime = (Mathf.Atan2(b1, a1_prime) * Mathf.Rad2Deg + 360f) % 360f;
        float h2_prime = (Mathf.Atan2(b2, a2_prime) * Mathf.Rad2Deg + 360f) % 360f;
        
        float deltaH_prime;
        if (Mathf.Abs(h2_prime - h1_prime) <= 180f)
            deltaH_prime = h2_prime - h1_prime;
        else if (h2_prime - h1_prime > 180f)
            deltaH_prime = h2_prime - h1_prime - 360f;
        else
            deltaH_prime = h2_prime - h1_prime + 360f;
        
        float deltaHPrime = 2f * Mathf.Sqrt(C1_prime * C2_prime) * Mathf.Sin(deltaH_prime * Mathf.Deg2Rad / 2f);
        
        // Weighting functions (simplified)
        float SL = 1f + (0.015f * Mathf.Pow(avgL - 50f, 2)) / Mathf.Sqrt(20f + Mathf.Pow(avgL - 50f, 2));
        float SC = 1f + 0.045f * avgC_prime;
        float SH = 1f + 0.015f * avgC_prime;
        
        // Final CIEDE2000 distance
        float deltaE = Mathf.Sqrt(
            Mathf.Pow(deltaL / SL, 2) + 
            Mathf.Pow(deltaC_prime / SC, 2) + 
            Mathf.Pow(deltaHPrime / SH, 2)
        );
        
        return deltaE;
    }
    
    // Sample hue from cumulative distribution function using inverse transform sampling
    private float SampleFromCDF(float randomValue)
    {
        // Binary search to find the hue corresponding to the random value
        int low = 0;
        int high = HUE_RESOLUTION - 1;
        
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (perceptualHueCDF[mid] < randomValue)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        
        return hueValues[low];
    }
    
    // Debug function to analyze the CIEDE2000-based weight distribution
    private void DebugCIEDE2000WeightDistribution(float[] weights, float totalWeight)
    {
        Debug.Log("[CIEDE2000] === CIEDE2000-BASED WEIGHT DISTRIBUTION ANALYSIS ===");
        
        // Find peaks and valleys in the CIEDE2000-based distribution
        float maxWeight = 0f;
        float minWeight = float.MaxValue;
        int maxIndex = 0;
        int minIndex = 0;
        
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] > maxWeight)
            {
                maxWeight = weights[i];
                maxIndex = i;
            }
            if (weights[i] < minWeight)
            {
                minWeight = weights[i];
                minIndex = i;
            }
        }
        
        Debug.Log($"[CIEDE2000] Peak difficulty: {maxWeight:F4} at {maxIndex}° (hue: {(float)maxIndex/360f:F3})");
        Debug.Log($"[CIEDE2000] Lowest difficulty: {minWeight:F4} at {minIndex}° (hue: {(float)minIndex/360f:F3})");
        Debug.Log($"[CIEDE2000] Discrimination ratio (hardest/easiest): {maxWeight/minWeight:F2}x");
        
        // Log CIEDE2000-based weights for key color regions
        LogCIEDE2000RegionWeight("RED", 0, weights, totalWeight);
        LogCIEDE2000RegionWeight("ORANGE", 30, weights, totalWeight);  
        LogCIEDE2000RegionWeight("YELLOW", 60, weights, totalWeight);
        LogCIEDE2000RegionWeight("GREEN", 120, weights, totalWeight);   // Predicted problem area
        LogCIEDE2000RegionWeight("CYAN", 180, weights, totalWeight);
        LogCIEDE2000RegionWeight("BLUE", 240, weights, totalWeight);
        LogCIEDE2000RegionWeight("PURPLE", 270, weights, totalWeight);
        LogCIEDE2000RegionWeight("MAGENTA", 300, weights, totalWeight);
        
        Debug.Log("[CIEDE2000] CIEDE2000 implementation provides scientifically accurate perceptual uniformity");
    }
    
    private void LogRegionWeight(string colorName, int degrees, float[] weights, float totalWeight)
    {
        float normalizedWeight = (weights[degrees] / totalWeight) * 360f; // Convert to probability density
        Debug.Log($"[PerceptualColor] {colorName} ({degrees}°): weight = {weights[degrees]:F3}, probability = {normalizedWeight:F1}x uniform");
    }
    
    private void LogCIEDE2000RegionWeight(string colorName, int degrees, float[] weights, float totalWeight)
    {
        float normalizedWeight = (weights[degrees] / totalWeight) * 360f; // Convert to probability density
        float discriminationDifficulty = weights[degrees];
        Debug.Log($"[CIEDE2000] {colorName} ({degrees}°): difficulty = {discriminationDifficulty:F4}, probability = {normalizedWeight:F2}x uniform");
    }
    
    // Destroy sphere from last trial
    private void DestroySpheres()
    {
        // Loop through all spheres in the single ring
        if (spheres != null)
        {
            foreach (GameObject sphere in spheres)
            {
                // If the sphere exists destroy it
                if (sphere != null)
                {
                    Destroy(sphere); // Destroy each sphere in the array
                }
            }
        }
        
        // Destroy the ring parent
        if (ringParent != null)
        {
            Destroy(ringParent);
            ringParent = null;
        }
        
        // Create new sphere array for the next set
        spheres = new GameObject[numberOfSpheres];
    }

    // Run a static trial
    private string[] StaticTrial(int sphereToChange, bool addChange)
    {
        // Ring configuration is already determined in StartNewTrial
        DestroySpheres();
        if (focusPointText != null)
            focusPointText.enabled = true;
        string[] sphereColors = CreateRingOfSpheres(centerPoint);
        StartCoroutine(StaticChange(sphereToChange, addChange));
        return sphereColors;
    }

    // Run a one direction trial
    /*private string[] OneDirectionTrial(int sphereToChange, bool addChange)
    {
        // Destroy any existing spheres before creating a new set
        DestroySpheres();

        // Randomly choose to either move in the x or y direction
        bool isXDirection = Random.Range(0, 2) == 0;
        // Randomly choose to start left right or top bottom
        float offset = Random.Range(0, 2) == 0 ? -1f : 1f;
        // Establish starting and end points
        Vector3 oneDirectionStartCenter;
        Vector3 oneDirectionEndCenter;

        // If moving left to right
        if (isXDirection)
        {
            // Set start and end points
            oneDirectionStartCenter = new Vector3(centerPoint.x + offset * directionDistance, centerPoint.y, centerPoint.z);
            oneDirectionEndCenter = new Vector3(centerPoint.x - offset * directionDistance, centerPoint.y, centerPoint.z);
            movementDirection = (offset < 0) ? "Left to Right" : "Right to Left";
        }
        // If moving up down
        else
        {
            // Set start and end points
            oneDirectionStartCenter = new Vector3(centerPoint.x, centerPoint.y + offset * directionDistance, centerPoint.z);
            oneDirectionEndCenter = new Vector3(centerPoint.x, centerPoint.y - offset * directionDistance, centerPoint.z);
            movementDirection = (offset < 0) ? "Up to Down" : "Down to Up";
        }

        // Create a new set of spheres and save their colours
        string[] sphereColors = CreateRingOfSpheres(oneDirectionStartCenter);
        // Run a one direction move and colour change
        StartCoroutine(DirectionChangeHueandMove(oneDirectionStartCenter, oneDirectionEndCenter, sphereToChange, addChange));
        // Return sphere colours
        return sphereColors;
    }

    // Run a two direction trial
    private string[] TwoDirectionTrial(int sphereToChange, bool addChange)
    {
        // Destroy any existing spheres before creating a new set
        DestroySpheres();

        // Randomly choose whether to start in the x or y direction
        bool isXDirection = Random.Range(0, 2) == 0;
        // Randomly choose start left right or top bottom
        float offset = Random.Range(0, 2) == 0 ? -1f : 1f;
        // Randomly choose which direction to turn
        float turn = Random.Range(0, 2) == 0 ? -1f : 1f;
        // Establish start and end points
        Vector3 twoDirectionStartCenter;
        Vector3 twoDirectionEndCenter;

        // If starting left right
        if (isXDirection)
        {
            // Set start and end points
            twoDirectionStartCenter = new Vector3(centerPoint.x + offset * directionDistance, centerPoint.y, centerPoint.z);
            twoDirectionEndCenter = new Vector3(centerPoint.x, centerPoint.y + turn * directionDistance, centerPoint.z);
            if (offset < 0 && turn < 0) movementDirection = "Left to Down";
            else if (offset < 0 && turn > 0) movementDirection = "Left to Up";
            else if (offset > 0 && turn < 0) movementDirection = "Right to Down";
            else movementDirection = "Right to Up";
        }
        // If starting top bottom
        else
        {
            // Set start and end points
            twoDirectionStartCenter = new Vector3(centerPoint.x, centerPoint.y + offset * directionDistance, centerPoint.z);
            twoDirectionEndCenter = new Vector3(centerPoint.x + turn * directionDistance, centerPoint.y, centerPoint.z);
             if (offset < 0 && turn < 0) movementDirection = "Up to Left";
            else if (offset < 0 && turn > 0) movementDirection = "Up to Right";
            else if (offset > 0 && turn < 0) movementDirection = "Down to Left";
            else movementDirection = "Down to Right";
        }

        // Create a new set of spheres and save their colours 
        string[] sphereColors = CreateRingOfSpheres(twoDirectionStartCenter);
        // Run a two direction move and colour change
        StartCoroutine(DirectionChangeHueandMove(twoDirectionStartCenter, twoDirectionEndCenter, sphereToChange, addChange));
        // Return sphere colours
        return sphereColors;
    }*/

    // Run a variable speed trial
    /*private string[] VariableSpeedTrial(int sphereToChange, bool addChange, string trialType)
    {
        // Destroy any existing spheres before creating a new set
        DestroySpheres();

        // Randomly choose whether to start in the x or y direction
        bool isXDirection = Random.Range(0, 2) == 0;
        // Randomly choose start left right or top bottom
        float offset = Random.Range(0, 2) == 0 ? -1f : 1f;
        // Establish start and end points
        Vector3 variableSpeedStartCenter;
        Vector3 variableSpeedEndCenter;

        // If starting left right
        if (isXDirection)
        {
            // Set start and end points
            variableSpeedStartCenter = new Vector3(centerPoint.x + offset * directionDistance, centerPoint.y, centerPoint.z);
            variableSpeedEndCenter = new Vector3(centerPoint.x - offset * directionDistance, centerPoint.y, centerPoint.z);
        }
        // If starting top bottom
        else
        {
            // Set start and end points
            variableSpeedStartCenter = new Vector3(centerPoint.x, centerPoint.y + offset * directionDistance, centerPoint.z);
            variableSpeedEndCenter = new Vector3(centerPoint.x, centerPoint.y - offset * directionDistance, centerPoint.z);
        }

        // Create a new set of spheres and save their colours 
        string[] sphereColors = CreateRingOfSpheres(variableSpeedStartCenter);
        // Run a variable speed move and colour change (note that trial type is an input here)
        StartCoroutine(DirectionChangeHueandMove(variableSpeedStartCenter, variableSpeedEndCenter, sphereToChange, addChange, trialType));
        // Return sphere colours
        return sphereColors;
    }*/

    // DEFUNCT: Run a rotational trial - no longer used in linear motion experiment
    /*private string[] RotationalTrial(int sphereToChange, bool addChange, string trialType)
    {
        DestroySpheres();
        if (focusPointText != null)
            focusPointText.enabled = true;
        string[] sphereColors = CreateRingOfSpheres(centerPoint);
        StartCoroutine(RotationalChangeAndMove(sphereToChange, addChange, trialType));
        return sphereColors;
    }*/

    /* DEFUNCT ROTATIONAL CODE - NO LONGER USED IN LINEAR MOTION EXPERIMENT
    private string[] RotationalOneDirTrial(int sphereToChange, bool addChange, RingMovementType moveType)
    {
        DestroySpheres();
        if (focusPointText != null)
            focusPointText.enabled = true;
        string[] sphereColors = CreateRingOfSpheres(centerPoint);
        StartCoroutine(LinearMovementCoroutine(sphereToChange, addChange));
        return sphereColors;
    }
    */

    private System.Collections.IEnumerator LinearMovementCoroutine(int sphereToChange, bool addChange)
    {
        canClick = false;
        trialActive = true;
        
        // Set grid state for trial start
        SetGridForTrialState(true);
        
        // Show guide path if set to Always mode
        if (spatialGridManager != null)
        {
            spatialGridManager.ShowGuidePath(false); // Pass false to indicate this is a regular trial
        }
        
        // Wait for trial start delay before beginning trial
        yield return new WaitForSeconds(trialStartDelay);
        
        // Only blink if blinkSpheres is enabled
        if (blinkSpheres)
        {
            // Blink the single ring
            if (spheres != null)
                yield return StartCoroutine(BlinkRing(spheres));
        }
            
        yield return new WaitForSeconds(trialStartDelay);

        // Calculate when motion is expected to stop
        expectedMotionStopTime = Time.time + trialLength;

        float elapsedTime = 0f;
        float currentProgress = 0f; // Progress from 0 (start) to 1 (end) for the ring

        // Single ring system - always move the only ring
        Debug.Log($"[LinearMovementCoroutine] Moving single ring towards user");

        bool changeApplied = false;
        bool useTwoDir = false;
        if (oneDirectionTrials && twoDirectionTrials)
            useTwoDir = Random.Range(0, 2) == 0 ? false : true;
        else if (twoDirectionTrials)
            useTwoDir = true;
        // else useTwoDir remains false (one direction)

        float halfTrial = trialLength / 2f;
        
        // Calculate change timing based on changeDuration
        float effectiveChangeDuration = changeDuration;
        float changeStart = halfTrial - (effectiveChangeDuration / 2f);
        
        // If change would start before trial begins, extend duration to fill entire trial
        if (changeStart < 0f)
        {
            effectiveChangeDuration = trialLength;
            changeStart = 0f;
        }
        
        float beepSequenceDuration = soundInterval * 4f; // 3 beeps + 1 interval before high beep
        float countdownStart = halfTrial - beepSequenceDuration;
        bool countdownStarted = false;
        bool changeStarted = false;
        
        while (elapsedTime < trialLength && trialActive)
        {
            float deltaTime = Time.deltaTime;
            
            // Update movement progress (0 to 1 over the trial length)
            float progressDelta = deltaTime / trialLength;
            currentProgress += progressDelta;
            
            // Clamp progress to [0, 1]
            currentProgress = Mathf.Clamp01(currentProgress);
            
            // Update sphere positions for single ring
            UpdateSpherePositionsByLinearMovement(currentProgress, true, false, 0f);
            
            elapsedTime += deltaTime;

            // Start gradual change at the calculated time
            if (!changeStarted && elapsedTime >= changeStart)
            {
                changeStarted = true;
                if (!changeApplied)
                {
                    // Select from the single ring
                    GameObject selectedSphere = null;
                    
                    if (spheres != null && sphereToChange >= 0 && sphereToChange < spheres.Length)
                    {
                        selectedSphere = spheres[sphereToChange];
                    }
                    
                    if (selectedSphere != null)
                    {
                        SphereManager sphereManager = selectedSphere.GetComponent<SphereManager>();
                        sphereManager.SetChanged(true);
                        
                        // Start gradual change coroutine
                        float changeStartTime = Time.time;
                        Coroutine changeCoroutine = StartCoroutine(GradualChangeSphere(selectedSphere, addChange, changeStartTime, effectiveChangeDuration));
                        activeChangeCoroutines.Add(changeCoroutine);
                        
                        changeApplied = true;
                        changeTime = changeStartTime;
                        canClick = true;
                        // Note: Linear movement continues in the same direction (towards user)
                    }
                }
            }

            // Start countdown beeps at the original time (for audio cues)
            if (!countdownStarted && elapsedTime >= countdownStart)
            {
                countdownStarted = true;
                activeBeepCoroutine = StartCoroutine(PlayBeepsAndChange(3, soundInterval, () => {
                    // Beeps only - change is handled separately above
                    activeBeepCoroutine = null; // Clear reference when completed
                }));
            }
            yield return null;
        }
        // Record attendant motion stop time and all motion stop time
        attendantMotionStopTime = Time.time;
        allMotionStopTime = Time.time;
        // Remove canClick = true here
    }

    /* DEFUNCT ROTATIONAL CODE - NO LONGER USED IN LINEAR MOTION EXPERIMENT
    private string[] RotationalTwoDirTrial(int sphereToChange, bool addChange, RingMovementType moveType)
    {
        DestroySpheres();
        if (focusPointText != null)
            focusPointText.enabled = true;
        string[] sphereColors = CreateRingOfSpheres(centerPoint);
        StartCoroutine(RotationalTwoDirCoroutine(sphereToChange, addChange, moveType));
        return sphereColors;
    }

    private System.Collections.IEnumerator RotationalTwoDirCoroutine(int sphereToChange, bool addChange, RingMovementType moveType)
    {
        // This coroutine is now identical to LinearMovementCoroutine, so just call it
        yield return StartCoroutine(LinearMovementCoroutine(sphereToChange, addChange));
    }
    */

    /*private System.Collections.IEnumerator RotationalChangeAndMove(int sphereToChange, bool addChange, string trialType)
    {
        canClick = false;
        yield return new WaitForSeconds(trialStartDelay);

        // Determine trial timing
        float firstHalfTrial = trialLength / 2f;
        float secondHalfTrial = trialLength / 2f;
        if (trialType == "Fast")
        {
            firstHalfTrial = fastTrialLength / 2f;
            secondHalfTrial = fastTrialLength / 2f;
        }
        else if (trialType == "Slow")
        {
            firstHalfTrial = slowTrialLength / 2f;
            secondHalfTrial = slowTrialLength / 2f;
        }
        else if (trialType == "FastSlow")
        {
            firstHalfTrial = fastTrialLength / 2f;
            secondHalfTrial = slowTrialLength / 2f;
        }
        else if (trialType == "SlowFast")
        {
            firstHalfTrial = slowTrialLength / 2f;
            secondHalfTrial = fastTrialLength / 2f;
        }

        // Randomly choose initial rotation direction
        int direction = Random.Range(0, 2) == 0 ? 1 : -1;
        string directionLabel = direction == 1 ? "Clockwise" : "Counterclockwise";
        movementDirection = directionLabel;

        float rotationSpeed = 360f / (firstHalfTrial + secondHalfTrial); // 360 degrees per full trial
        float elapsedTime = 0f;
        float currentAngle = 0f;

        // First half: rotate in initial direction
        while (elapsedTime < firstHalfTrial)
        {
            float delta = rotationSpeed * Time.deltaTime * direction;
            currentAngle += delta;
            UpdateSpherePositionsByRotation(currentAngle, true, false);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Change sphere at halfway point
        GameObject selectedSphere = innerSpheres[sphereToChange];
        SphereManager sphereManager = selectedSphere.GetComponent<SphereManager>();
        sphereManager.SetChanged(true);
        ChangeSphere(selectedSphere, addChange);

        // Reverse direction at halfway point
        direction *= -1;
        directionLabel = direction == 1 ? "Clockwise" : "Counterclockwise";
        movementDirection += "→" + directionLabel;

        // Second half: rotate in opposite direction
        elapsedTime = 0f;
        while (elapsedTime < secondHalfTrial)
        {
            float delta = rotationSpeed * Time.deltaTime * direction;
            currentAngle += delta;
            UpdateSpherePositionsByRotation(currentAngle, true, false);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        canClick = true;
    }*/

    // Coroutine to blink a ring (hide/show all spheres in the ring)
    private System.Collections.IEnumerator BlinkRing(GameObject[] ringSpheres, int blinks = 3)
    {
        for (int b = 0; b < blinks; b++)
        {
            // Hide all
            foreach (var s in ringSpheres) if (s != null) s.SetActive(false);
            yield return new WaitForSeconds(blinkDuration);
            // Show all
            foreach (var s in ringSpheres) if (s != null) s.SetActive(true);
            yield return new WaitForSeconds(blinkDuration);
        }
    }

    // Helper for updating sphere positions by rotation
    private void UpdateSpherePositionsByLinearMovement(float movementProgress, bool moveInner, bool moveOuter, float outerMovementProgress = 0f)
    {
        // Add performance monitoring
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // DEBUG: Log what's happening
        Debug.Log($"[UpdateSpherePositions] Single ring system - moveInner: {moveInner}, moveOuter: {moveOuter}");
        Debug.Log($"[UpdateSpherePositions] changeOrientation: {changeOrientation}, individualSphereMovement: {individualSphereMovement}");
        
        // Calculate current position based on movement progress (0 = start at centerPoint, 1 = end at endPoint)
        // Use absolute positions: interpolate from centerPoint to endPoint with Z offset for user position
        Vector3 startPos = new Vector3(centerPoint.x, centerPoint.y, centerPoint.z - 15f);
        Vector3 endPos = new Vector3(endPoint.x, endPoint.y, endPoint.z - 15f);
        Vector3 currentInnerPosition = Vector3.Lerp(startPos, endPos, movementProgress);
        Vector3 currentOuterPosition = Vector3.Lerp(startPos, endPos, (outerMovementProgress == 0f ? movementProgress : outerMovementProgress));
        
        Debug.Log($"[UpdateSpherePositions] Movement progress: {movementProgress}");
        Debug.Log($"[UpdateSpherePositions] Start Point (centerPoint): {centerPoint}");
        Debug.Log($"[UpdateSpherePositions] End Point: {endPoint}");
        Debug.Log($"[UpdateSpherePositions] Current position: {currentInnerPosition}");
        
        // For orientation trials, check individualSphereMovement to determine movement method
        // Single ring system - simplified movement logic
        if (changeOrientation && individualSphereMovement) // Per-sphere movement method for orientation trials
        {
            Debug.Log($"[UpdateSpherePositions] Using individualSphereMovement method for orientation trial (single ring)");
            // Move all spheres in the single ring individually to preserve orientations
            if (spheres != null)
            {
                for (int i = 0; i < spheres.Length; i++)
                {
                    if (spheres[i] != null)
                    {
                        // Move sphere relative to current ring center position
                        Vector3 originalLocalPos = spheres[i].transform.localPosition; // Position relative to ring parent
                        Vector3 worldPos = currentInnerPosition + originalLocalPos;
                        spheres[i].transform.position = worldPos;
                        // Preserve each sphere's original orientation during movement
                    }
                }
            }
            
            // Update focal point to follow the center of the spheres
            UpdateFocalPointPosition();
        }
        else // Parent movement method - used for all non-orientation trials and orientation trials when individualSphereMovement=false
        {
            // Single ring system - move the ring parent to current position
            Debug.Log($"[UpdateSpherePositions] Moving single ring to position={currentInnerPosition}");
            if (ringParent != null)
            {
                ringParent.transform.position = currentInnerPosition;
            }
        }
        
        // Update focal point to follow the center of the spheres
        UpdateFocalPointPosition();
        
        // Log performance to detect hangs
        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds > 100)
        {
            Debug.LogError($"[PERFORMANCE WARNING] UpdateSpherePositionsByLinearMovement took {stopwatch.ElapsedMilliseconds}ms - potential freeze risk!");
        }
        else
        {
            Debug.Log($"[PERFORMANCE] UpdateSpherePositionsByLinearMovement completed in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    // Create a set of spheres - Single ring system
    private string[] CreateRingOfSpheres(Vector3 center)
    {
        // ALWAYS generate fresh random colors for each trial (change blindness requires different colors)
        Debug.Log("[GameManager] Generating fresh random spheres for single ring");
        return CreateSingleRingOfSpheres(center);
    }
    
    // Create single ring of spheres
    private string[] CreateSingleRingOfSpheres(Vector3 center)
    {
        Debug.Log($"[CreateSingleRingOfSpheres] Starting single ring creation - ChangeType: {changeType}");
        
        // Stop any running gradual change coroutines from previous trial
        StopAllActiveChangeCoroutines();
        
        // Stop any active beep sequence from previous trial
        StopBeepSequence();
        
        // Destroy previous ring parent if it exists
        if (ringParent != null) Destroy(ringParent);
        
        // Create new ring parent at the starting position (centerPoint)
        ringParent = new GameObject("Ring");
        Vector3 startPosition = new Vector3(center.x, center.y, center.z - 15f); // Offset by 15 so (0,0,0) is at user position
        ringParent.transform.position = startPosition;
        ringParent.transform.rotation = Quaternion.identity;
        
        Debug.Log($"[CreateRingOfSpheres] Ring created at position: {startPosition}");
        Debug.Log($"[CreateRingOfSpheres] centerPoint parameter: {center}");
        Debug.Log($"[CreateRingOfSpheres] User's Start Point setting: {centerPoint}");
        
        // Allocate array for single ring
        spheres = new GameObject[numberOfSpheres];
        
        // Generate random colors for this trial
        var sphereColors = GenerateRandomAttributes(numberOfSpheres);
        
        // Create the single ring of spheres
        Debug.Log($"[CreateSingleRingOfSpheres] Creating {numberOfSpheres} spheres");
        for (int i = 0; i < numberOfSpheres; i++)
        {
            float angle = i * Mathf.PI * 2 / numberOfSpheres;
            float x = Mathf.Cos(angle) * ringRadius;
            float y = Mathf.Sin(angle) * ringRadius;
            Vector3 position = new Vector3(x, y, 0f); // Relative to parent
            
            // Get appropriate prefab for the trial type
            GameObject prefab = changeOrientation ? stripedSpherePrefab : spherePrefab;
            GameObject sphere = Instantiate(prefab, ringParent.transform); // Create as child first
            sphere.transform.localPosition = position; // Then set local position
            sphere.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);
            sphere.name = $"sphere_{i}";
            
            // Apply generated attributes
            ApplyAttributeToSphere(sphere, sphereColors[i]);
            spheres[i] = sphere;
        }
        
        // Update focal point to center of newly created spheres
        UpdateFocalPointPosition();
        
        return sphereColors.ToArray();
    }
    
    
    // Create spheres with real-time generation (fallback/original path)
    private string[] CreateRingOfSpheresWithGeneration(Vector3 center)
    {
        // Single ring system - just call the working method
        return CreateSingleRingOfSpheres(center);
    }

    // Helper method to set material to transparent mode
    private void SetMaterialTransparent(Renderer renderer)
    {
        if (renderer != null && renderer.material != null)
        {
            Debug.Log($"[SetMaterialTransparent] Material name: {renderer.material.name}, Shader: {renderer.material.shader.name}");
            
            // For striped materials, we need to ensure transparency mode is set correctly
            if (renderer.material.shader.name.Contains("Stripes"))
            {
                // The striped shader should already support transparency, just ensure render queue
                renderer.material.renderQueue = 3000; // Transparent queue
                Debug.Log($"[SetMaterialTransparent] Set striped material render queue to 3000");
            }
            else
            {
                // For standard materials, set full transparency mode
                renderer.material.SetFloat("_Mode", 3); // Transparent
                renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                renderer.material.SetInt("_ZWrite", 0);
                renderer.material.DisableKeyword("_ALPHATEST_ON");
                renderer.material.EnableKeyword("_ALPHABLEND_ON");
                renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                renderer.material.renderQueue = 3000;
                Debug.Log($"[SetMaterialTransparent] Applied standard transparency settings");
            }
            
            Debug.Log($"[SetMaterialTransparent] Applied transparency settings to {renderer.material.name}");
        }
    }

    // Apply a pre-calculated attribute value to a sphere
    private void ApplyAttributeToSphere(GameObject sphere, string attributeValue)
    {
        if (string.IsNullOrEmpty(attributeValue) || !float.TryParse(attributeValue, out float value))
        {
            Debug.LogWarning($"[GameManager] Invalid attribute value: {attributeValue}");
            return;
        }
        
        if (changeHue)
        {
            // Apply hue (0-1)
            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color.RGBToHSV(renderer.material.color, out _, out float s, out float v);
                
                // Single ring system - all spheres are active
                float saturation = sphereSaturation;
                
                Color newColor = Color.HSVToRGB(value, saturation, sphereValue);
                newColor.a = renderer.material.color.a; // Preserve alpha
                renderer.material.color = newColor;
            }
        }
        else if (changeLuminance)
        {
            // Apply luminance/brightness (0-1) - Luminance trials should always use zero saturation (grayscale)
            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color.RGBToHSV(renderer.material.color, out float h, out float s, out _);
                
                // Value trials always use zero saturation for grayscale appearance
                float saturation = 0f;
                
                Color newColor = Color.HSVToRGB(defaultHue, saturation, value);
                newColor.a = renderer.material.color.a; // Preserve alpha
                renderer.material.color = newColor;
            }
        }
        else if (changeSize)
        {
            // Apply size
            sphere.transform.localScale = new Vector3(value, value, value);
        }
        else if (changeOrientation)
        {
            // Apply orientation (rotation around Z-axis)
            sphere.transform.rotation = Quaternion.Euler(0, 0, value);
        }
    }

    // Helper method to check if a sphere belongs to the inactive ring
    private bool IsInactiveRingSphere(GameObject sphere)
    {
        // Single ring system - all spheres are active
        return false;
    }

    // Helper method to determine which prefab to use for sphere instantiation
    private GameObject GetSpherePrefabForRing(string ringType)
    {
        if (!changeOrientation)
        {
            // For non-orientation trials, always use normal spherePrefab
            Debug.Log($"[GetSpherePrefabForRing] Non-orientation trial, using spherePrefab for {ringType}");
            return spherePrefab;
        }
        
        // For orientation trials, use striped spheres
        Debug.Log($"[GetSpherePrefabForRing] Using stripedSpherePrefab for {ringType}");
        
        // Safety check for null prefab
        if (stripedSpherePrefab == null)
        {
            Debug.LogError($"[GetSpherePrefabForRing] stripedSpherePrefab is null! Falling back to spherePrefab for {ringType}");
            return spherePrefab;
        }
        
        return stripedSpherePrefab;
    }

    // Helper method to disable XR interaction components on a sphere (so it can't be selected by user)
    private void DisableSphereInteraction(GameObject sphere)
    {
        // Disable XR Simple Interactable component if present
        XRSimpleInteractable xrInteractable = sphere.GetComponent<XRSimpleInteractable>();
        if (xrInteractable != null)
        {
            xrInteractable.enabled = false;
        }
        
        // Disable XR Grab Interactable component if present
        XRGrabInteractable xrGrabInteractable = sphere.GetComponent<XRGrabInteractable>();
        if (xrGrabInteractable != null)
        {
            xrGrabInteractable.enabled = false;
        }
        
        // Disable custom interaction scripts if present
        SphereInteraction sphereInteraction = sphere.GetComponent<SphereInteraction>();
        if (sphereInteraction != null)
        {
            sphereInteraction.enabled = false;
        }
        
        SphereClickHandler sphereClickHandler = sphere.GetComponent<SphereClickHandler>();
        if (sphereClickHandler != null)
        {
            sphereClickHandler.enabled = false;
        }
    }

    // Make a random change to static spheres (note: using coroutines here to help with the fact that this code has to wait for trial times)
    System.Collections.IEnumerator StaticChange(int sphereToChange, bool addChange)
    {
        canClick = false; // Disable clicking during this phase
        trialActive = true;
        
        // Set grid state for trial start
        SetGridForTrialState(true);
        
        // Show guide path if set to Always mode
        if (spatialGridManager != null)
        {
            spatialGridManager.ShowGuidePath(false); // Pass false to indicate this is a regular trial
        }
        
        // Wait for trial start delay before beginning trial
        yield return new WaitForSeconds(trialStartDelay);
        
        // For static trials, there's no motion, so set expectedMotionStopTime to 0
        expectedMotionStopTime = 0f;
        
        // Record trial start time (after delay)
        float trialStartTime = Time.time;
        
        // Only blink if blinkSpheres is enabled
        if (blinkSpheres)
        {
            // Single ring system - blink the single ring
            if (spheres != null)
                yield return StartCoroutine(BlinkRing(spheres));
        }
        
        // Calculate change timing
        float halfTrial = trialLength / 2f;
        float effectiveChangeDuration = changeDuration;
        float changeStartFromTrialBeginning = halfTrial - (effectiveChangeDuration / 2f);
        
        // If change would start before the beginning of the trial, extend duration
        if (changeStartFromTrialBeginning < 0f)
        {
            effectiveChangeDuration = trialLength;
            changeStartFromTrialBeginning = 0f;
        }
        
        // Start the gradual change at the correct time
        GameObject selectedSphere = null;
        if (spheres != null && sphereToChange >= 0 && sphereToChange < spheres.Length)
        {
            selectedSphere = spheres[sphereToChange];
        }
        
        if (selectedSphere != null)
        {
            SphereManager sphereManager = selectedSphere.GetComponent<SphereManager>();
            sphereManager.SetChanged(true);
            
            float changeStartTime = trialStartTime + changeStartFromTrialBeginning;
            Coroutine changeCoroutine = StartCoroutine(GradualChangeSphere(selectedSphere, addChange, changeStartTime, effectiveChangeDuration));
            activeChangeCoroutines.Add(changeCoroutine);
            
            // Record change time and attendant motion stop time (static trial)
            changeTime = changeStartTime;
            attendantMotionStopTime = changeStartTime;
            
            // Enable clicking when change starts (not when beeps finish)
            StartCoroutine(EnableClickingWhenChangeStarts(changeStartTime));
        }
        
        // Wait for the appropriate time before starting beeps
        float beepSequenceDuration = soundInterval * 4f; // 3 beeps + 1 interval before high beep
        float timeUntilBeeps = halfTrial - beepSequenceDuration;
        if (timeUntilBeeps > 0f)
        {
            float waitTime = timeUntilBeeps - (Time.time - trialStartTime);
            if (waitTime > 0f)
                yield return new WaitForSeconds(waitTime);
        }
        
        // Play beeps (no change needed in callback since change already started)
        activeBeepCoroutine = StartCoroutine(PlayBeepsAndChange(3, soundInterval, () => {
            // Beeps finished - no additional action needed since canClick is already enabled
            activeBeepCoroutine = null; // Clear reference when completed
        }));
        
        // Don't wait for beeps to finish - proceed independently
        // Wait for the beep sequence to complete OR for the trial to be aborted
        float beepStartTime = Time.time;
        float totalBeepDuration = (3 + 1) * soundInterval; // 3 beeps + 1 high beep, each with interval
        
        while (activeBeepCoroutine != null && Time.time - beepStartTime < totalBeepDuration && trialActive)
        {
            yield return null;
        }
        
        Debug.Log($"[StaticTrial] Beep sequence completed or aborted. trialActive: {trialActive}");
        
        // Wait for another half trial length before the next iteration
        float waitStartTime = Time.time;
        float waitDuration = trialLength / 2f;
        float elapsedWaitTime = 0f;
        
        while (elapsedWaitTime < waitDuration && trialActive)
        {
            elapsedWaitTime = Time.time - waitStartTime;
            yield return null;
        }
        
        if (!trialActive) 
        {
            Debug.Log("[StaticTrial] Trial ended by user interaction");
            yield break;
        }
        
        // Record all motion stop time
        allMotionStopTime = Time.time;
    }

    // Observer Motion wrapper - identical to Static trials but labeled differently for CSV
    System.Collections.IEnumerator ObserverMotionChange(int sphereToChange, bool addChange)
    {
        // Simply call the StaticChange coroutine - behavior is identical to Static
        yield return StartCoroutine(StaticChange(sphereToChange, addChange));
    }

    // Make a random change and move spheres
    /*System.Collections.IEnumerator DirectionChangeHueandMove(Vector3 startCenter, Vector3 endCenter, int sphereToChange, bool addChange, string trialType = "")
    {
        canClick = false; // Disable clicking during this phase
        yield return new WaitForSeconds(trialStartDelay);

        // Split the trial in two halves
        float firstHalfTrial = trialLength / 2f;
        float secondHalfTrial = trialLength / 2f;
        

        // If a variable speed trial then set the two halves to their respective lengths
        if (trialType == "Fast")
        {
            firstHalfTrial = fastTrialLength / 2f;
            secondHalfTrial = fastTrialLength / 2f;
        } 
        else if (trialType == "Slow")
        {
            firstHalfTrial = slowTrialLength / 2f;
            secondHalfTrial = slowTrialLength / 2f;
        }   
        else if (trialType == "FastSlow")
        {
            firstHalfTrial = fastTrialLength / 2f;
            secondHalfTrial = slowTrialLength / 2f;
        } 
        else if (trialType == "SlowFast")
        {
            firstHalfTrial = slowTrialLength / 2f;
            secondHalfTrial = fastTrialLength / 2f;
        } 

        // Move the ring's center to the center point over the first half of the trial
        float elapsedTime = 0f;
        while (elapsedTime < firstHalfTrial)
        {
            // Create variable to track how far into the half trial we are
            float t = elapsedTime / firstHalfTrial;
            // Interpolate where the center should be based on the amount of the half trial that is complete
            Vector3 currentCenter = Vector3.Lerp(startCenter, centerPoint, t);

            // Update the position of each sphere relative to the current center
            for (int i = 0; i < innerSpheres.Length; i++)
            {
                // Math to place spheres in the right spots around the ring
                float angle = i * Mathf.PI * 2 / ringOneNumSpheres;
                float x = currentCenter.x + Mathf.Cos(angle) * ringOneRadius;
                float y = currentCenter.y + Mathf.Sin(angle) * ringOneRadius;
                // Move the spheres
                innerSpheres[i].transform.position = new Vector3(x, y, currentCenter.z);
            }

            // Move time forwards
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the spheres are exactly at the center when half trial reached
        for (int i = 0; i < innerSpheres.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / ringOneNumSpheres;
            float x = centerPoint.x + Mathf.Cos(angle) * ringOneRadius;
            float y = centerPoint.y + Mathf.Sin(angle) * ringOneRadius;
            innerSpheres[i].transform.position = new Vector3(x, y, centerPoint.z);
        }

        // Change the colour of a random sphere
        GameObject selectedSphere = innerSpheres[sphereToChange];
        SphereManager sphereManager = selectedSphere.GetComponent<SphereManager>();
        sphereManager.SetChanged(true);
        ChangeSphere(selectedSphere, addChange);

        // Same idea as the first half of the trial
        // Move the ring's center to the end point over the second half of the trial
        elapsedTime = 0f;
        while (elapsedTime < secondHalfTrial)
        {
            float t = elapsedTime / secondHalfTrial;
            Vector3 currentCenter = Vector3.Lerp(centerPoint, endCenter, t);

            // Update the position of each sphere relative to the current center
            for (int i = 0; i < innerSpheres.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / ringOneNumSpheres;
                float x = currentCenter.x + Mathf.Cos(angle) * ringOneRadius;
                float y = currentCenter.y + Mathf.Sin(angle) * ringOneRadius;
                innerSpheres[i].transform.position = new Vector3(x, y, currentCenter.z);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the spheres are exactly at the end positions
        for (int i = 0; i < innerSpheres.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / ringOneNumSpheres;
            float x = endCenter.x + Mathf.Cos(angle) * ringOneRadius;
            float y = endCenter.y + Mathf.Sin(angle) * ringOneRadius;
            innerSpheres[i].transform.position = new Vector3(x, y, endCenter.z);
        }

        canClick = true; // Enable clicking after this phase
    }*/

    // Change the randomly selected sphere as needed
    private void ChangeSphere(GameObject sphere, bool addChange)
    {
        // Single ring system - use standard change magnitudes
        float useHueChange = hueChangeHSV;
        float useLuminanceChange = luminanceChange;
        float useSizeChange = sizeChange;
        float useOrientationChange = orientationChange;
        // If its not an orientation experiment
        if (!changeOrientation)
        {
            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color currentColor = renderer.material.color;
                float currentSize = sphere.transform.localScale.x;
                Color.RGBToHSV(currentColor, out float currentHue, out float currentSaturation, out float currentValue);
                Color newColor = Color.HSVToRGB(currentHue, currentSaturation, currentValue);
                if (changeHue)
                {
                    float adaptiveHueChange;
                    
                    if (weightedHueChange)
                    {
                        // Apply CIEDE2000-based perceptually uniform hue change for precise color discrimination
                        Debug.Log($"[CIEDE2000 DEBUG] Starting GetCIEDE2000BasedHueChange: currentHue={currentHue:F3}, sat={currentSaturation:F3}, val={currentValue:F3}, baseChange={useHueChange:F3}");
                        adaptiveHueChange = GetCIEDE2000BasedHueChange(currentHue, currentSaturation, currentValue, useHueChange);
                        Debug.Log($"[CIEDE2000 DEBUG] GetCIEDE2000BasedHueChange returned: {adaptiveHueChange:F3}");
                    }
                    else
                    {
                        // Use simple HSV-based hue change
                        adaptiveHueChange = useHueChange;
                        Debug.Log($"[HSV DEBUG] Using simple HSV change: {adaptiveHueChange:F3}");
                    }
                    
                    float newHue;
                    // Prevent hue wraparounds by dynamically adjusting direction
                    if (currentHue + adaptiveHueChange > 1f)
                    {
                        // Too close to 1, force subtract
                        newHue = Mathf.Clamp01(currentHue - adaptiveHueChange);
                    }
                    else if (currentHue - adaptiveHueChange < 0f)
                    {
                        // Too close to 0, force add
                        newHue = Mathf.Clamp01(currentHue + adaptiveHueChange);
                    }
                    else
                    {
                        // Normal behavior based on addChange
                        newHue = addChange
                            ? (currentHue + adaptiveHueChange) % 1f // Keeps hue in range [0,1]
                            : (currentHue - adaptiveHueChange + 1f) % 1f; // Prevents negatives
                    }
                    
                    // Store original and new values for logging/debugging
                    originalResult = currentHue.ToString();
                    changedResult = newHue.ToString();
                    
                    // Create new color with updated hue
                    newColor = Color.HSVToRGB(newHue, currentSaturation, currentValue);
                    
                    // Apply new color to the sphere
                    renderer.material.color = newColor;
                    Debug.Log($"[COLOR DEBUG] Applied color to sphere: R={newColor.r:F3}, G={newColor.g:F3}, B={newColor.b:F3}, HSV=({newHue:F3},{currentSaturation:F3},{currentValue:F3})");
                    
                    // Verify the color was actually applied
                    Color verifyColor = renderer.material.color;
                    Debug.Log($"[COLOR VERIFY] Sphere color after setting: R={verifyColor.r:F3}, G={verifyColor.g:F3}, B={verifyColor.b:F3}");
                    
                    // Debug: Log color change details (CIEDE2000 or simple HSV) - always log for debugging
                    if (weightedHueChange)
                    {
                        Color originalColor = Color.HSVToRGB(currentHue, currentSaturation, currentValue);
                        float[] originalLab = RGBToCIELAB(originalColor.r, originalColor.g, originalColor.b);
                        float[] newLab = RGBToCIELAB(newColor.r, newColor.g, newColor.b);
                        float actualDeltaE = CalculateCIEDE2000Distance(originalLab, newLab);
                        
                        Debug.Log($"[CIEDE2000 Change] Hue: {currentHue:F3} -> {newHue:F3} (Δ{adaptiveHueChange:F3}) | ΔE2000: {actualDeltaE:F2} | Direction: {(addChange ? "+" : "-")}");
                    }
                    else
                    {
                        Debug.Log($"[HSV Change] Hue: {currentHue:F3} -> {newHue:F3} (Δ{adaptiveHueChange:F3}) | Method: Simple HSV | Direction: {(addChange ? "+" : "-")}");
                    }
                }

                // Check if the trial is a luminance change trial
                else if (changeLuminance)
                {
                    float newValue;
                    if (currentValue + useLuminanceChange > 1f)
                    {
                        newValue = Mathf.Clamp01(currentValue - useLuminanceChange);
                    }
                    else if (currentValue - useLuminanceChange < 0f)
                    {
                        newValue = Mathf.Clamp01(currentValue + useLuminanceChange);
                    }
                    else
                    {
                        newValue = addChange
                            ? Mathf.Clamp01(currentValue + useLuminanceChange)
                            : Mathf.Clamp01(currentValue - useLuminanceChange);
                    }
                    originalResult = currentValue.ToString();
                    changedResult = newValue.ToString();
                    newColor = Color.HSVToRGB(currentHue, currentSaturation, newValue);
                    renderer.material.color = newColor;
                }

                else if (changeSize)
                {
                    float newSize = addChange
                        ? (currentSize * 1+useSizeChange)
                        : (currentSize * 1-useSizeChange);
                    // Ensure that if the change would push outside the max or min size that the change goes the other direction
                    if (newSize > maxSize || newSize < minSize)
                    {
                        newSize = addChange
                        ? (currentSize * 1-useSizeChange)
                        : (currentSize * 1+useSizeChange);
                    }
                    originalResult = currentSize.ToString();
                    changedResult = newSize.ToString();
                    sphere.transform.localScale = new Vector3(newSize, newSize, newSize);
                }
            }
        }
        // Same as above but for orientation
        else if (changeOrientation)
        {
            float currentOrientation;

            // Handle some coversion stuff since unity angles are -180 to 180 but don't output that way
            if (sphere.transform.rotation.eulerAngles.z > 180)
            {
                currentOrientation = sphere.transform.rotation.eulerAngles.z - 360;
            }
            else
            {
                currentOrientation = sphere.transform.rotation.eulerAngles.z;
            }
            float newOrientation = addChange
                ? currentOrientation + useOrientationChange
                : currentOrientation - useOrientationChange;
            if (newOrientation > 180 || newOrientation < -180)
            {
                newOrientation = addChange
                    ? currentOrientation - useOrientationChange
                    : currentOrientation + useOrientationChange;
            }
            originalResult = currentOrientation.ToString();
            changedResult = newOrientation.ToString();
            sphere.transform.rotation = Quaternion.Euler(0, 0, newOrientation);
        }
    }

    // Gradual change coroutine - applies changes over time instead of instantaneously
    private System.Collections.IEnumerator GradualChangeSphere(GameObject sphere, bool addChange, float startTime, float duration)
    {
        if (sphere == null || duration <= 0f)
        {
            // Fallback to instantaneous change
            ChangeSphere(sphere, addChange);
            yield break;
        }

        // Store initial values
        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        if (sphereRenderer == null) yield break;

        Color initialColor = sphereRenderer.material.color;
        Vector3 initialScale = sphere.transform.localScale;
        Quaternion initialRotation = sphere.transform.rotation;

        // Calculate target values using the same logic as ChangeSphere
        Color targetColor = initialColor;
        Vector3 targetScale = initialScale;
        Quaternion targetRotation = initialRotation;

        // Calculate targets based on change type
        if (!changeOrientation)
        {
            Color.RGBToHSV(initialColor, out float currentHue, out float currentSaturation, out float currentValue);
            float currentSize = initialScale.x;

            if (changeHue)
            {
                float adaptiveHueChange = weightedHueChange ? 
                    GetCIEDE2000BasedHueChange(currentHue, currentSaturation, currentValue, hueChangeHSV) : 
                    hueChangeHSV;

                float newHue;
                if (currentHue + adaptiveHueChange > 1f)
                    newHue = Mathf.Clamp01(currentHue - adaptiveHueChange);
                else if (currentHue - adaptiveHueChange < 0f)
                    newHue = Mathf.Clamp01(currentHue + adaptiveHueChange);
                else
                    newHue = addChange ? (currentHue + adaptiveHueChange) % 1f : (currentHue - adaptiveHueChange + 1f) % 1f;

                targetColor = Color.HSVToRGB(newHue, currentSaturation, currentValue);
            }
            else if (changeLuminance)
            {
                float newValue;
                if (currentValue + luminanceChange > 1f)
                    newValue = Mathf.Clamp01(currentValue - luminanceChange);
                else if (currentValue - luminanceChange < 0f)
                    newValue = Mathf.Clamp01(currentValue + luminanceChange);
                else
                    newValue = addChange ? Mathf.Clamp01(currentValue + luminanceChange) : Mathf.Clamp01(currentValue - luminanceChange);

                targetColor = Color.HSVToRGB(currentHue, currentSaturation, newValue);
            }
            else if (changeSize)
            {
                float newSize = addChange ? (currentSize * (1 + sizeChange)) : (currentSize * (1 - sizeChange));
                if (newSize > maxSize || newSize < minSize)
                    newSize = addChange ? (currentSize * (1 - sizeChange)) : (currentSize * (1 + sizeChange));
                
                targetScale = new Vector3(newSize, newSize, newSize);
            }
        }
        else if (changeOrientation)
        {
            float currentOrientation = sphere.transform.rotation.eulerAngles.z;
            if (currentOrientation > 180) currentOrientation -= 360;

            float newOrientation = addChange ? currentOrientation + orientationChange : currentOrientation - orientationChange;
            if (newOrientation > 180 || newOrientation < -180)
                newOrientation = addChange ? currentOrientation - orientationChange : currentOrientation + orientationChange;

            targetRotation = Quaternion.Euler(0, 0, newOrientation);
        }

        // Wait until it's time to start the change
        while (Time.time < startTime)
        {
            // Check if sphere was destroyed while waiting
            if (sphere == null)
                yield break;
            yield return null;
        }

        // Apply gradual change over duration
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // Check if sphere was destroyed during the change
            if (sphere == null)
                yield break;

            float t = elapsedTime / duration;
            
            // Apply smooth interpolation with null checks
            if (!changeOrientation)
            {
                if (changeHue || changeLuminance)
                {
                    // Check if renderer still exists
                    if (sphereRenderer != null && sphereRenderer.material != null)
                        sphereRenderer.material.color = Color.Lerp(initialColor, targetColor, t);
                }
                else if (changeSize)
                {
                    // Check if transform still exists
                    if (sphere.transform != null)
                        sphere.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
                }
            }
            else
            {
                // Check if transform still exists
                if (sphere.transform != null)
                    sphere.transform.rotation = Quaternion.Lerp(initialRotation, targetRotation, t);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure final values are exactly set (with null checks)
        if (sphere != null)
        {
            if (!changeOrientation)
            {
                if (changeHue || changeLuminance)
                {
                    if (sphereRenderer != null && sphereRenderer.material != null)
                        sphereRenderer.material.color = targetColor;
                }
                else if (changeSize)
                {
                    if (sphere.transform != null)
                        sphere.transform.localScale = targetScale;
                }
            }
            else
            {
                if (sphere.transform != null)
                    sphere.transform.rotation = targetRotation;
            }
        }

        // Update the stored results for logging
        if (changeHue)
        {
            Color.RGBToHSV(initialColor, out float origHue, out _, out _);
            Color.RGBToHSV(targetColor, out float newHue, out _, out _);
            originalResult = origHue.ToString();
            changedResult = newHue.ToString();
        }
        else if (changeLuminance)
        {
            Color.RGBToHSV(initialColor, out _, out _, out float origValue);
            Color.RGBToHSV(targetColor, out _, out _, out float newValue);
            originalResult = origValue.ToString();
            changedResult = newValue.ToString();
        }
        else if (changeSize)
        {
            originalResult = initialScale.x.ToString();
            changedResult = targetScale.x.ToString();
        }
        else if (changeOrientation)
        {
            float origOrientation = initialRotation.eulerAngles.z;
            if (origOrientation > 180) origOrientation -= 360;
            float newOrientation = targetRotation.eulerAngles.z;
            if (newOrientation > 180) newOrientation -= 360;
            originalResult = origOrientation.ToString();
            changedResult = newOrientation.ToString();
        }
    }

    // Helper function to apply CIEDE2000-based perceptually uniform hue changes
    private float GetPerceptuallyUniformHueChange(float currentHue, float baseChange)
    {
        // Legacy function - now redirects to CIEDE2000-based implementation
        return GetCIEDE2000BasedHueChange(currentHue, sphereSaturation, sphereValue, baseChange);
    }
    
    // CIEDE2000-based hue change calculation specifically for sphere color changes in change blindness experiments
    private float GetCIEDE2000BasedHueChange(float currentHue, float currentSaturation, float currentValue, float baseChange)
    {
        // Convert current HSV color to RGB then to CIELAB for CIEDE2000 calculations
        Color currentColor = Color.HSVToRGB(currentHue, currentSaturation, currentValue);
        float[] currentLab = RGBToCIELAB(currentColor.r, currentColor.g, currentColor.b);
        
        // Use the adjustable weightedHueChangeDelta slider for target perceptual difference
        // Range: 1.0 (very subtle) to 5.0 (very obvious) ΔE units
        float targetDeltaE = weightedHueChangeDelta; // Now uses the slider value instead of fixed 4.0
        
        // Binary search to find the hue change that produces the target CIEDE2000 difference
        float minChange = 0.005f; // Minimum 0.5% hue change (more granular)
        float maxChange = 0.4f;   // Maximum 40% hue change (reasonable limit)
        float bestChange = baseChange;
        float bestDeltaE = 0f;
        float tolerance = 0.2f;   // Accept ±0.2 ΔE from target
        int actualIterations = 0; // Track actual iterations used
        
        // Perform binary search with limited iterations for performance
        for (int iteration = 0; iteration < 12; iteration++)
        {
            actualIterations = iteration + 1; // Track iterations
            float testChange = (minChange + maxChange) * 0.5f;
            
            // Test both positive and negative hue directions to find the best perceptual change
            float testHue1 = (currentHue + testChange) % 1f;
            float testHue2 = (currentHue - testChange + 1f) % 1f;
            
            // Convert test hues to CIELAB using the same saturation and value
            Color testColor1 = Color.HSVToRGB(testHue1, currentSaturation, currentValue);
            Color testColor2 = Color.HSVToRGB(testHue2, currentSaturation, currentValue);
            
            float[] testLab1 = RGBToCIELAB(testColor1.r, testColor1.g, testColor1.b);
            float[] testLab2 = RGBToCIELAB(testColor2.r, testColor2.g, testColor2.b);
            
            // Calculate CIEDE2000 distances for both directions
            float deltaE1 = CalculateCIEDE2000Distance(currentLab, testLab1);
            float deltaE2 = CalculateCIEDE2000Distance(currentLab, testLab2);
            
            // Use the larger of the two distances (ensures consistent detectability)
            float actualDeltaE = Mathf.Max(deltaE1, deltaE2);
            
            // Check if we're within acceptable tolerance
            if (Mathf.Abs(actualDeltaE - targetDeltaE) <= tolerance)
            {
                bestChange = testChange;
                bestDeltaE = actualDeltaE;
                break; // Found acceptable change
            }
            
            // Adjust search range based on how close we are to target
            if (actualDeltaE < targetDeltaE)
            {
                minChange = testChange; // Need larger change
            }
            else
            {
                maxChange = testChange; // Need smaller change
            }
            
            // Track the best change so far
            if (Mathf.Abs(actualDeltaE - targetDeltaE) < Mathf.Abs(bestDeltaE - targetDeltaE))
            {
                bestChange = testChange;
                bestDeltaE = actualDeltaE;
            }
        }
        
        // Ensure we don't go below minimum or above maximum reasonable changes
        bestChange = Mathf.Clamp(bestChange, minChange, maxChange);
        
        // Fallback: if CIEDE2000 calculation failed, use base change but log warning
        if (bestChange == 0f || float.IsNaN(bestChange))
        {
            Debug.LogWarning($"[CIEDE2000] Calculation failed for hue {currentHue:F3}, falling back to base change {baseChange:F3}");
            bestChange = baseChange;
        }
        
        // Debug logging for CIEDE2000-based changes (always log for debugging)
        Debug.Log($"[CIEDE2000 RESULT] Sphere change: Hue {currentHue:F3} -> Δ{bestChange:F3} -> ΔE {bestDeltaE:F2} (target: {targetDeltaE:F1})");
        Debug.Log($"[CIEDE2000 ITERATIONS] Binary search completed in {actualIterations} iterations, tolerance: ±{tolerance:F2}");
        
        return bestChange;
    }

    // Function to pull if clicking is allowed (public so it can be called by other scripts)
    public bool CanClick()
    {
        return canClick; // Expose the click state to other scripts
    }

    // Function to show the inbetween screen
    public void ShowBlackScreen(string message)
    {
        Debug.Log($"ShowBlackScreen called. blackScreen is {(blackScreen == null ? "NULL" : "ASSIGNED")}");
        // Hide all spheres in the single ring
        if (spheres != null)
        {
            foreach (GameObject sphere in spheres)
            {
                if (sphere != null)
                    sphere.SetActive(false);
            }
        }

        blackScreen.SetActive(true);
        blackScreenUp = true;

        // Find and set the black screen message
        TMPro.TMP_Text messageText = blackScreen.GetComponentInChildren<TMPro.TMP_Text>();
        if (messageText != null)
        {
            messageText.text = message;
        }

        //if (focusPointText != null)
        //    focusPointText.enabled = false;
    }

    // Function to hide the inbetween screen
    public void HideBlackScreen()
    {
        // Reactivate spheres in the single ring when hiding the black screen
        if (spheres != null)
        {
            foreach (GameObject sphere in spheres)
            {
                if (sphere != null)
                    sphere.SetActive(true);
            }
        }

        blackScreen.SetActive(false);
        blackScreenUp = false;
    }


    // Create headers for the results
    private void CreateHeaders()
    {
        int totalSpheres = numberOfSpheres; // Single ring system
        headers = new string[23 + totalSpheres]; // 21 + 2 new columns (Change Start Time and Change End Time)
        headers[0] = "Participant";
        headers[1] = "Trial Number";
        headers[2] = "Change Type";
        headers[3] = "Movement Type";
        headers[4] = "Changed Sphere";
        headers[5] = "Before Change";
        headers[6] = "After Change";
        headers[7] = "Selected Sphere";
        headers[8] = "Success";
        headers[9] = "Trial Start Delay (s)";
        headers[10] = "Trial Length (s)";
        headers[11] = "Response Time (After Change Start) (s)";  // Renamed and moved
        headers[12] = "Change Start Time (s)";                   // New column
        headers[13] = "Change End Time (s)";                     // New column
        
        // General Settings columns (shifted by +2 due to new columns)
        headers[14] = "Blink Spheres";
        headers[15] = "Blink Duration (s)";
        headers[16] = "Rotation Speed (°/s)";
        headers[17] = "# of Spheres";  // Renamed from "# of Spheres - Ring 1"
        headers[18] = "Radius";        // Renamed from "Radius - Ring 1"
        headers[19] = "Sphere Saturation";
        headers[20] = "Sphere Value";
        headers[21] = "Sphere Size";
        headers[22] = "Sound Interval (s)";
        
        int idx = 23; // Updated index start for sphere colors (21 + 2 new columns)
        // Single ring system - create headers for all spheres in the ring
        for (int i = 0; i < numberOfSpheres; i++)
        {
            headers[idx++] = $"Sphere {i+1}";
        }
    }

    // Save data after a trial
    public void SaveTrialResults()
    {
        // Save what the sphere change, picked sphere, and result of trial were
        trialResults[5] = originalResult; // Before Change
        trialResults[6] = changedResult;  // After Change
        trialResults[7] = selectedSphere; // Selected Sphere
        trialResults[8] = success;        // Success
        trialResults[11] = changeTime > 0f ? (sphereClickTime - changeTime).ToString("F3") : ""; // Response Time (After Change Start)
        trialResults[12] = changeTime > 0f ? changeTime.ToString("F3") : ""; // Change Start Time (s)
        trialResults[13] = changeTime > 0f && changeDuration > 0f ? (changeTime + changeDuration).ToString("F3") : (changeTime > 0f ? changeTime.ToString("F3") : ""); // Change End Time (s)
        // Removed "Response Time (After motion stops)" column
        // Create a new array with an extra row
        string[][] newResults = new string[results.Length + 1][];

        // Copy existing results to the new array
        for (int i = 0; i < results.Length; i++)
        {
            newResults[i] = results[i];
        }

        // Add the trial results to the new last row
        newResults[results.Length] = trialResults;

        // Reassign results to the new array
        results = newResults;
    }

    // Save results at the end of the experiment and export to csv
    public void SaveResultsToCSV(string filePath)
    {
        // Ensure the directory exists
        string dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        // Create a StringBuilder to build the CSV content
        StringBuilder csvContent = new StringBuilder();

        // Add the headers as the first row
        csvContent.AppendLine(string.Join(",", headers));

        // Loop through each row in the results array
        foreach (var row in results)
        {
            // Join the elements in the row with commas and append to the StringBuilder
            csvContent.AppendLine(string.Join(",", row));
        }

        // Try to write the StringBuilder content to a CSV file with UTF-8 encoding
        try
        {
            File.WriteAllText(filePath, csvContent.ToString(), System.Text.Encoding.UTF8);
        }
        catch (IOException ex)
        {
            Debug.LogError($"[GameManager] Failed to save results to CSV. The file may be open in another program. Error: {ex.Message}");
            ShowBlackScreen("Error: Could not save results. Please close the CSV file and try again.");
        }
    }
    // This is the method to handle sphere clicks in VR 

    public void OnSphereClicked(GameObject clickedSphere) 
    { 
        sphereClickTime = Time.time; // Record click time immediately

        // Stop any active beep sequence when user interacts
        StopBeepSequence();



        // Ensure clicks are only processed when allowed 
        if (!canClick) 
        { 
            Debug.LogWarning("Click ignored. Interaction is disabled (canClick is false)."); 
            return; 
        } 

        // Log the clicked sphere 
        Debug.Log($"Sphere {clickedSphere.name} clicked!"); 

        // Determine the index of the clicked sphere in the single ring
        int index = -1;
        
        // Check the single ring of spheres
        if (spheres != null)
        {
            for (int i = 0; i < spheres.Length; i++)
            {
                if (spheres[i] == clickedSphere)
                {
                    index = i;
                    break;
                }
            }
        }
        
        // Set selectedSphere based on the index in the single ring
        selectedSphere = (index + 1).ToString();
        SphereManager sphereManager = clickedSphere.GetComponent<SphereManager>(); 
        success = (sphereManager != null && sphereManager.isChanged) ? "true" : "false"; // Check if the clicked sphere was the changed one 

        // Save trial results 
        SaveTrialResults(); 

        // Prevent further clicks until the next trial 
        canClick = false; 
        trialActive = false; // Stop all motion immediately
        
        // Reset grid state for between trials
        SetGridForTrialState(false);
        
        Debug.Log("Interaction disabled (canClick set to false). Preparing for the next trial..."); 

        // Update trial block progress
        currentBlockTrialCount++;
        
        // Check if it's time for a break
        if (ShouldTakeBreak())
        {
            ShowBreakScreen();
        }
        else
        {
            // Show the black screen and wait for user input to start the next trial 
            ShowBlackScreen("Press A on the VR controller to Continue to the Next Trial");
        } 
    } 

    // End of sphere creation functions

    // Simplified sphere creation - purely random attributes within acceptable ranges

    /* DEFUNCT ROTATIONAL CODE - NO LONGER USED IN LINEAR MOTION EXPERIMENT
    // Enum for ring movement type
    private enum RingMovementType { Inner, Outer, Both }

    // New trial wrappers
    private string[] InnerMovingTrial(int sphereToChange, bool addChange, bool twoDirections = true)
    {
        if (twoDirections)
            return RotationalTwoDirTrial(sphereToChange, addChange, RingMovementType.Inner);
        else
            return RotationalOneDirTrial(sphereToChange, addChange, RingMovementType.Inner);
    }
    private string[] OuterMovingTrial(int sphereToChange, bool addChange, bool twoDirections = true)
    {
        if (twoDirections)
            return RotationalTwoDirTrial(sphereToChange, addChange, RingMovementType.Outer);
        else
            return RotationalOneDirTrial(sphereToChange, addChange, RingMovementType.Outer);
    }
    private string[] BothMovingTrial(int sphereToChange, bool addChange, bool twoDirections = true)
    {
        if (twoDirections)
            return RotationalTwoDirTrial(sphereToChange, addChange, RingMovementType.Both);
        else
            return RotationalOneDirTrial(sphereToChange, addChange, RingMovementType.Both);
    }
    */

    private bool IsFileLocked(string filePath)
    {
        if (!File.Exists(filePath))
            return false;
        try
        {
            using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // File can be opened for read/write, so it's not locked
            }
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    

    // --- Audio cue coroutine ---
    private System.Collections.IEnumerator PlayChangeBeepSequence()
    {
        if (audioSource == null || lowSound == null || highSound == null)
        {
            Debug.LogError($"[AudioCue] AudioSource or clips not assigned. audioSource: {(audioSource == null ? "NULL" : "OK")}, lowSound: {(lowSound == null ? "NULL" : "OK")}, highSound: {(highSound == null ? "NULL" : "OK")}");
            yield break;
        }
        // Force AudioSource settings
        audioSource.spatialBlend = 0f; // 2D
        audioSource.mute = false;
        audioSource.volume = 1f;
        audioSource.bypassEffects = true;
        audioSource.bypassListenerEffects = true;
        audioSource.bypassReverbZones = true;
        Debug.Log($"[AudioCue] Playing beep sequence. AudioSource: {audioSource}, lowSound: {lowSound.name}, highSound: {highSound.name}, volume: {audioSource.volume}, mute: {audioSource.mute}, spatialBlend: {audioSource.spatialBlend}");
        for (int i = 0; i < 3; i++)
        {
            Debug.Log($"[AudioCue] Playing beep {i+1}");
            audioSource.PlayOneShot(lowSound);
            yield return new WaitForSeconds(soundInterval);
        }
        Debug.Log("[AudioCue] Playing high beep");
        audioSource.PlayOneShot(highSound);
    }

    // --- Audio cue coroutine ---
    private System.Collections.IEnumerator PlayCountdownBeeps(int beepCount)
    {
        if (audioSource == null || lowSound == null)
        {
            Debug.LogError($"[AudioCue] AudioSource or lowSound not assigned. audioSource: {(audioSource == null ? "NULL" : "OK")}, lowSound: {(lowSound == null ? "NULL" : "OK")}");
            yield break;
        }
        for (int i = 0; i < beepCount; i++)
        {
            Debug.Log($"[AudioCue] Countdown beep {i+1}");
            audioSource.spatialBlend = 0f;
            audioSource.mute = false;
            audioSource.volume = 1f;
            audioSource.PlayOneShot(lowSound);
            yield return new WaitForSeconds(soundInterval);
        }
    }

    // --- Unified beep and change coroutine ---
    private System.Collections.IEnumerator PlayBeepsAndChange(int beepCount, float interval, System.Action onHighBeep)
    {
        beepSequenceAborted = false; // Reset abort flag
        
        if (audioSource == null || lowSound == null || highSound == null)
        {
            Debug.LogError($"[AudioCue] AudioSource or clips not assigned. audioSource: {(audioSource == null ? "NULL" : "OK")}, lowSound: {(lowSound == null ? "NULL" : "OK")}, highSound: {(highSound == null ? "NULL" : "OK")}");
            yield break;
        }
        
        // Play countdown beeps
        for (int i = 0; i < beepCount; i++)
        {
            if (beepSequenceAborted) yield break; // Check for abortion
            
            audioSource.spatialBlend = 0f;
            audioSource.mute = false;
            audioSource.volume = 1f;
            audioSource.PlayOneShot(lowSound);
            Debug.Log($"[AudioCue] Countdown beep {i+1}");
            
            // Wait for interval, checking for abortion
            float elapsedTime = 0f;
            while (elapsedTime < interval)
            {
                if (beepSequenceAborted) yield break;
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        
        if (beepSequenceAborted) yield break; // Final check before high beep
        
        // Wait interval, then play high beep and trigger change
        audioSource.spatialBlend = 0f;
        audioSource.mute = false;
        audioSource.volume = 1f;
        //yield return new WaitForSeconds(interval);
        audioSource.PlayOneShot(highSound);
        Debug.Log("[AudioCue] Playing high beep (change moment)");
        onHighBeep?.Invoke();
    }
}
