using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; 

public enum ChangeType
{
    Hue,
    Value,
    Size,
    Orientation
}

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
    

    public int staticTrials = 20;
    public int innerMovingTrials = 20;
    public int outerMovingTrials = 20;
    public int bothMovingTrials = 20;
    public bool changeInnerRing = true;
    public bool changeOuterRing = true;
    
    private bool oneDirectionTrials = true;
    private bool twoDirectionTrials = false;

    // Helper properties for backward compatibility
    public bool changeHue => changeType == ChangeType.Hue;
    public bool changeValue => changeType == ChangeType.Value;
    public bool changeSize => changeType == ChangeType.Size;
    public bool changeOrientation => changeType == ChangeType.Orientation;
    private GameObject innerRingParent;
    private GameObject outerRingParent;
    private GameObject middleRingParent;
    
    // Trial length details
    [Header("General Settings")]
    [CustomLabel("Trial Length (s)")]
    public float trialLength = 4f;
    [CustomLabel("Movement Start Delay (s)")]
    public float movementStartDelay = 1f; // Delay before spheres start moving (in seconds)
    [Tooltip("Enable or disable sphere blinking visual cue at the start of trials.")]
    public bool blinkSpheres = true;
    [CustomLabel("Blink Duration (s)")]
    [ConditionalEnable("blinkSpheres", true)]
    public float blinkDuration = 0.3f; // Adjustable blink timing for ring cue
    [CustomLabel("Rotation Speed (°/s)")]
    public float rotationSpeed = 90f; // Speed of rotation in degrees per second

    private string participantFileName
    {
        get
        {
            if (changeHue) return $"{participantName}_hue.csv";
            if (changeValue) return $"{participantName}_value.csv";
            if (changeSize) return $"{participantName}_size.csv";
            if (changeOrientation) return $"{participantName}_orientation.csv";
            return $"{participantName}.csv";
        }
    }

    // Outer ring settings
    private int numberOfRings = 3;
    

    // Folder for saving results (always in the unity project folder)
    private string resultsFolder;

    // Sphere details
    
    [CustomLabel("# of Spheres - Ring 1")]
    public int ringOneNumSpheres = 6; // Number of spheres to create
    [CustomLabel("# of Spheres - Ring 2")]
    public int ringTwoNumSpheres = 6;
    [CustomLabel("# of Spheres - Ring 3")]
    public int ringThreeNumSpheres = 6;
    //public int numberOfPassiveSpheres = 0; // Set to >0 to enable passive ring
    [CustomLabel("Radius - Ring 1")]
    public float ringOneRadius = 1f; // Radius of the ring
    [CustomLabel("Radius - Ring 2")]
    public float ringTwoRadius = 2f;
    [CustomLabel("Radius - Ring 3")]
    public float ringThreeRadius = 3f;

    // --- Outer (third) ring settings ---
    //[Header("Outer Ring (Does Nothing)")]
    

    
    private bool genericInactiveRing = true;
    // Removed unused outerRingParentObject and outerRingSpheres; use outerRingParent and outerSpheres instead

    

    // Change details (note: to change details about orientation stripes go to the stripes material in the materials folder)
    [Range(0f, 1f)] private float defaultHue = 0f;

    [Range(0f, 1f)] public float sphereSaturation = 0.8f; // Fixed Saturation (0 to 1)
    [Range(0f, 1f)] public float sphereValue = 0.8f; // Fixed Value (0 to 1)
    public float sphereSize = 0.7f;
    [Range(0f, 1f)] public float inactiveRingTransparency = 0.5f;
    [Tooltip("When enabled, the inactive ring maintains default appearance (no randomization of hue, saturation, value, or size).")]
    public Vector3 centerPoint = Vector3.zero; // Center of the ring
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
    
    
    [Header("Value Change Settings")]
    [Range(0f, 0.5f)] 
    [Tooltip("Value (brightness) change amount for sphere modifications.")]
    public float valueChange = 0.2f;
    
    [Header("Size Change Settings")]
    [Tooltip("Size change amount for sphere scaling.")]
    public float sizeChange = 0.2f;
    [Tooltip("Minimum allowed sphere size.")]
    public float minSize = 0.5f;
    [Tooltip("Maximum allowed sphere size.")]
    public float maxSize = 1.5f;
    
    [Header("Orientation Change Settings")]
    [Tooltip("When enabled, rotates each sphere individually to preserve stripe orientations during ring movement. When disabled, uses more efficient parent rotation but sphere orientations may change. Only affects orientation trials.")]
    public bool ferrisWheelSpin = false; // Rotates all spheres individually instead of using parent rotation (less efficient but retains orientations)

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

    // Outer ring separate change magnitudes
    private bool separateOuterRingChangeMagnitudes = false;
    private float outerRingRotationSpeed = 90f;
    [Range(0f, 1f)] private float outerRingHueChange = 0.2f;
    [Range(0f, 0.5f)] private float outerRingValueChange = 0.2f;
    private float outerRingSizeChange = 0.2f;
    private float outerRingOrientationChange = 40f;



    
    
    // Ring configuration tracking for even distribution when both rings are allowed
    private int ringConfig1Trials = 0; // changeInnerRing = true, changeOuterRing = false behavior
    private int ringConfig2Trials = 0; // changeInnerRing = false, changeOuterRing = true behavior
    private enum RingConfiguration { Config1, Config2 } // Config1: Ring2&3 active, Config2: Ring1&2 active
    private RingConfiguration currentRingConfig;
    private bool currentRingConfigSet = false; // Track if configuration has been initialized
    //public int oneDirectionTrials = 24;
    //public int twoDirectionTrials = 24;

    // Tracking number of trial type run
    private int staticTrialsRun = 0;
    private int innerMovingTrialsRun = 0;
    private int outerMovingTrialsRun = 0;
    private int bothMovingTrialsRun = 0;
    //private int oneDirectionTrialsRun = 0;
    //private int twoDirectionTrialsRun = 0;
    
    // Tracking number of trials run
    private int trialNumber = 0;

    // Trial types for random selection
    private string[] trialTypes = { "Static", "InnerMoving", "OuterMoving", "BothMoving" };
    //private string[] staticSpeedTrialTypes = { "Static", "One", "Two" };
    //private string[] variableSpeedTrialTypes = { "Static", "Fast", "Slow", "FastSlow", "SlowFast" };
    //private string[] rotationalStaticSpeedTrialTypes = { "Static", "RotationalOneDir", "RotationalTwoDir" };
    //private string[] rotationalVariableSpeedTrialTypes = { "RotationalStatic", "RotationalFast", "RotationalSlow", "RotationalFastSlow", "RotationalSlowFast" };

    
    private bool changeOuterRingSphere = false;

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
    [HideInInspector] public GameObject[] innerSpheres; // Store references to the created spheres
    private GameObject[] outerSpheres;
    private GameObject[] middleSpheres;

    [HideInInspector] public TMPro.TextMeshProUGUI focusPointText; // Assign in Inspector

    


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
    private RingConfiguration preGeneratedRingConfig;
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

        // Reset ring configuration counters for a fresh experiment session
        ringConfig1Trials = 0;
        ringConfig2Trials = 0;
        Debug.Log("[GameManager] Ring configuration counters reset for new experiment session");

        // Show initial black screen with VR controller instruction
        Debug.Log("[GameManager] Calling ShowBlackScreen at Start()");
        ShowBlackScreen("Press A on the VR Controller Button to Begin");
        
        // Background generation disabled - each trial generates fresh random colors
        Debug.Log("[GameManager] Background generation disabled for fresh colors each trial");

        if (focusPointText != null)
        {
            focusPointText.enabled = false;
            focusPointText.rectTransform.position = centerPoint;
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
        if (Input.GetButtonDown("Submit") && blackScreenUp && experimentRunning)
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

    // Determine which ring configuration to use for this trial
    private RingConfiguration DetermineRingConfiguration(bool forPreGeneration = false)
    {
        Debug.Log($"[DetermineRingConfiguration] changeInnerRing: {changeInnerRing}, changeOuterRing: {changeOuterRing}");
        
        RingConfiguration selectedConfig;
        
        if (changeInnerRing && !changeOuterRing)
        {
            // Config2: Ring 1 & 2 active (Ring 3 inactive) - changeInnerRing only
            selectedConfig = RingConfiguration.Config2;
            Debug.Log("[DetermineRingConfiguration] Using Config2 (Ring 1 & 2 active, Ring 3 inactive)");
        }
        else if (!changeInnerRing && changeOuterRing)
        {
            // Config1: Ring 2 & 3 active (Ring 1 inactive) - changeOuterRing only
            selectedConfig = RingConfiguration.Config1;
            Debug.Log("[DetermineRingConfiguration] Using Config1 (Ring 2 & 3 active, Ring 1 inactive)");
        }
        else if (changeInnerRing && changeOuterRing)
        {
            // Both allowed - distribute evenly
            int totalTrials = staticTrials + innerMovingTrials + outerMovingTrials + bothMovingTrials;
            int halfTrials = totalTrials / 2;
            
            Debug.Log($"[DetermineRingConfiguration] Both rings allowed. Config1 trials: {ringConfig1Trials}, Config2 trials: {ringConfig2Trials}, Half trials: {halfTrials}");
            
            if (ringConfig1Trials < halfTrials && ringConfig2Trials < halfTrials)
            {
                // Both configs have room, choose randomly using high-quality RNG
                bool useConfig1 = GenerateHighQualityRandom() < 0.5f;
                selectedConfig = useConfig1 ? RingConfiguration.Config1 : RingConfiguration.Config2;
                Debug.Log($"[DetermineRingConfiguration] Both configs have room, randomly chose: {selectedConfig} (random value: {(useConfig1 ? "<0.5" : ">=0.5")})");
            }
            else if (ringConfig1Trials < halfTrials)
            {
                // Only Config1 has room
                selectedConfig = RingConfiguration.Config1;
                Debug.Log("[DetermineRingConfiguration] Only Config1 has room");
            }
            else
            {
                // Only Config2 has room (or both are full, default to Config2)
                selectedConfig = RingConfiguration.Config2;
                Debug.Log("[DetermineRingConfiguration] Only Config2 has room (or both full)");
            }
            
            // Increment counter for chosen config
            if (selectedConfig == RingConfiguration.Config1)
                ringConfig1Trials++;
            else
                ringConfig2Trials++;
                
            Debug.Log($"[DetermineRingConfiguration] After increment - Config1 trials: {ringConfig1Trials}, Config2 trials: {ringConfig2Trials}");
        }
        else
        {
            // Neither ring is allowed to change, log error and default to Config2
            Debug.LogError("[GameManager] Both changeInnerRing and changeOuterRing are false. Defaulting to Config2 (Ring 1 & 2 active).");
            selectedConfig = RingConfiguration.Config2;
        }
        
        // Only set currentRingConfig if this is for immediate use (not pre-generation)
        if (!forPreGeneration)
        {
            currentRingConfig = selectedConfig;
            currentRingConfigSet = true;
        }
        
        return selectedConfig;
    }


    // Running trials
    private void StartNewTrial()
    {
        // Reset ring configuration for each new trial
        currentRingConfigSet = false;
        Debug.Log("[GameManager] Ring configuration reset for new trial");
        
        int totalTrials = staticTrials + innerMovingTrials + outerMovingTrials + bothMovingTrials;
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
        
        // Only determine configuration if we don't already have one
        if (!currentRingConfigSet)
        {
            Debug.Log("[GameManager] No current ring configuration, determining new one");
            currentRingConfig = DetermineRingConfiguration();
            currentRingConfigSet = true;
        }
        else
        {
            Debug.Log($"[GameManager] Using existing ring configuration: {currentRingConfig}");
        }
        
        trialResults = new string[headers.Length];
        for (int i = 0; i < 29; i++)
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
        
        // Populate General Settings columns - all indices reduced by 1
        trialResults[15] = trialLength.ToString();
        trialResults[16] = movementStartDelay.ToString();
        trialResults[17] = blinkSpheres.ToString();
        trialResults[18] = blinkSpheres ? blinkDuration.ToString() : "";
        trialResults[19] = rotationSpeed.ToString();
        trialResults[20] = ringOneNumSpheres.ToString();
        trialResults[21] = ringTwoNumSpheres.ToString();
        trialResults[22] = ringThreeNumSpheres.ToString();
        trialResults[23] = ringOneRadius.ToString();
        trialResults[24] = ringTwoRadius.ToString();
        trialResults[25] = ringThreeRadius.ToString();
        // Removed Default Hue column
        trialResults[26] = sphereSaturation.ToString();
        trialResults[27] = sphereValue.ToString();
        trialResults[28] = sphereSize.ToString();
        trialResults[29] = inactiveRingTransparency.ToString();
        trialResults[30] = soundInterval.ToString();
        
        // Set Change Type
        if (changeHue) trialResults[2] = "Hue";
        else if (changeValue) trialResults[2] = "Value";
        else if (changeSize) trialResults[2] = "Size";
        else if (changeOrientation) trialResults[2] = "Orientation";
        else trialResults[2] = "";
        
        // Only Ring 2 spheres are selectable - always use middle ring
        int sphereToChange = Random.Range(0, ringTwoNumSpheres);
        
        // Set Attendant Ring - Ring 2 acts as either "Inner" or "Outer" based on configuration
        if (numberOfRings == 3)
        {
            if (currentRingConfig == RingConfiguration.Config1)
            {
                trialResults[4] = "Inner";
                changeOuterRingSphere = false;
            }
            else if (currentRingConfig == RingConfiguration.Config2)
            {
                trialResults[4] = "Outer";
                changeOuterRingSphere = true;
            }
        }
        else
        {
            trialResults[4] = "Inner";
            changeOuterRingSphere = false;
        }
        
        trialResults[9] = (sphereToChange + 1).ToString();
        bool addChange = Random.Range(0, 2) == 0;

        // Build a list of available trial types
        var availableTrialTypes = new System.Collections.Generic.List<string>();
        if (staticTrialsRun < staticTrials) availableTrialTypes.Add("Static");
        if (innerMovingTrialsRun < innerMovingTrials) availableTrialTypes.Add("InnerMoving");
        if (outerMovingTrialsRun < outerMovingTrials) availableTrialTypes.Add("OuterMoving");
        if (bothMovingTrialsRun < bothMovingTrials) availableTrialTypes.Add("BothMoving");

        if (availableTrialTypes.Count == 0)
        {
            experimentRunning = false;
            string filePath = $"{resultsFolder}{participantFileName}";
            SaveResultsToCSV(filePath);
            ShowBlackScreen("Experiment Complete");
            Debug.Log("[GameManager] No available trial types left. Experiment complete.");
            return;
        }

        string trialType = availableTrialTypes[Random.Range(0, availableTrialTypes.Count)];
        
        // Map internal trial type to CSV output format
        string movementTypeForCSV;
        switch (trialType)
        {
            case "Static":
                movementTypeForCSV = "Static";
                break;
            case "InnerMoving":
                movementTypeForCSV = "Inner Ring Only";
                break;
            case "OuterMoving":
                movementTypeForCSV = "Outer Ring Only";
                break;
            case "BothMoving":
                movementTypeForCSV = "Both Moving";
                break;
            default:
                movementTypeForCSV = trialType;
                break;
        }
        
        trialResults[3] = movementTypeForCSV;
        
        // Ring Configuration - Note: Output is reversed from internal enum
        // Config1 (Ring2&3 active) outputs as "2", Config2 (Ring1&2 active) outputs as "1"
        trialResults[5] = (currentRingConfig == RingConfiguration.Config1) ? "2" : "1";
        
        // Ring State columns - determine based on ring configuration and movement type
        string ring1State, ring2State, ring3State;
        
        if (currentRingConfig == RingConfiguration.Config1)
        {
            // Config1: Ring 2 & 3 active, Ring 1 inactive
            ring1State = "Inactive";
            if (trialType == "Static")
            {
                ring2State = "Static";
                ring3State = "Static";
            }
            else if (trialType == "InnerMoving")
            {
                ring2State = "Moving";
                ring3State = "Static";
            }
            else if (trialType == "OuterMoving")
            {
                ring2State = "Static";
                ring3State = "Moving";
            }
            else // BothMoving
            {
                ring2State = "Moving";
                ring3State = "Moving";
            }
        }
        else // Config2
        {
            // Config2: Ring 1 & 2 active, Ring 3 inactive
            ring3State = "Inactive";
            if (trialType == "Static")
            {
                ring1State = "Static";
                ring2State = "Static";
            }
            else if (trialType == "InnerMoving")
            {
                ring1State = "Moving";
                ring2State = "Static";
            }
            else if (trialType == "OuterMoving")
            {
                ring1State = "Static";
                ring2State = "Moving";
            }
            else // BothMoving
            {
                ring1State = "Moving";
                ring2State = "Moving";
            }
        }
        
        trialResults[6] = ring1State;
        trialResults[7] = ring2State;
        trialResults[8] = ring3State;
        
        // Generate spheres and execute trial
        string[] allColors = GenerateSpheresForTrial();
        ExecuteTrial(sphereToChange, addChange, trialType, allColors);
    }
    
    // Execute a trial with given parameters and sphere colors
    private void ExecuteTrial(int sphereToChange, bool addChange, string trialType, string[] allColors)
    {
        // Fill trialResults with all sphere colors, setting empty string for inactive rings
        if (allColors != null && trialResults.Length > 31)
        {
            int colorCount = Mathf.Min(allColors.Length, trialResults.Length - 31);
            int colorIndex = 0;
            
            // Ring 1 spheres
            for (int i = 0; i < ringOneNumSpheres && (31 + colorIndex) < trialResults.Length; i++)
            {
                bool isRing1Active = (currentRingConfig == RingConfiguration.Config2); // Ring 1 active in Config2
                trialResults[31 + colorIndex] = isRing1Active && colorIndex < colorCount ? allColors[colorIndex] : "";
                colorIndex++;
            }
            
            // Ring 2 spheres (middle ring - always active when present)
            if (numberOfRings == 3)
            {
                for (int i = 0; i < ringTwoNumSpheres && (31 + colorIndex) < trialResults.Length; i++)
                {
                    trialResults[31 + colorIndex] = colorIndex < colorCount ? allColors[colorIndex] : "";
                    colorIndex++;
                }
            }
            
            // Ring 3 spheres (outer ring)
            if (numberOfRings >= 2)
            {
                for (int i = 0; i < ringThreeNumSpheres && (31 + colorIndex) < trialResults.Length; i++)
                {
                    bool isRing3Active = (currentRingConfig == RingConfiguration.Config1); // Ring 3 active in Config1
                    trialResults[31 + colorIndex] = isRing3Active && colorIndex < colorCount ? allColors[colorIndex] : "";
                    colorIndex++;
                }
            }
        }
        
        if (trialType == "Static")
        {
            trialNumber++;
            staticTrialsRun++;
            StartCoroutine(StaticChange(sphereToChange, addChange));
        }
        else if (trialType == "InnerMoving")
        {
            trialNumber++;
            innerMovingTrialsRun++;
            StartCoroutine(RotationalOneDirCoroutine(sphereToChange, addChange, RingMovementType.Inner));
        }
        else if (trialType == "OuterMoving")
        {
            trialNumber++;
            outerMovingTrialsRun++;
            StartCoroutine(RotationalOneDirCoroutine(sphereToChange, addChange, RingMovementType.Outer));
        }
        else if (trialType == "BothMoving")
        {
            trialNumber++;
            bothMovingTrialsRun++;
            StartCoroutine(RotationalOneDirCoroutine(sphereToChange, addChange, RingMovementType.Both));
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
        
        // Pre-determine next trial parameters
        preGeneratedRingConfig = DetermineRingConfiguration(forPreGeneration: true);
        // preGeneratedRingConfigSet = true; // Commented out - background generation disabled
        preGeneratedSphereToChange = Random.Range(0, ringTwoNumSpheres);
        preGeneratedAddChange = Random.Range(0, 2) == 0;
        
        // Determine next trial type
        var availableTrialTypes = new System.Collections.Generic.List<string>();
        if (staticTrialsRun < staticTrials) availableTrialTypes.Add("Static");
        if (innerMovingTrialsRun < innerMovingTrials) availableTrialTypes.Add("InnerMoving");
        if (outerMovingTrialsRun < outerMovingTrials) availableTrialTypes.Add("OuterMoving");
        if (bothMovingTrialsRun < bothMovingTrials) availableTrialTypes.Add("BothMoving");
        
        if (availableTrialTypes.Count > 0)
        {
            preGeneratedTrialType = availableTrialTypes[Random.Range(0, availableTrialTypes.Count)];
            
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
        int totalSpheres = ringOneNumSpheres + (numberOfRings == 3 ? ringTwoNumSpheres : 0) + (numberOfRings >= 2 ? ringThreeNumSpheres : 0);
        string[] allColors = new string[totalSpheres];
        
        // Determine ring configuration for inactive ring detection
        RingConfiguration config = DetermineRingConfiguration(forPreGeneration: true);
        
        // Generate colors for each ring (random or default based on genericInactiveRing setting)
        // Inner ring (Ring 1)
        var innerColors = (genericInactiveRing && config == RingConfiguration.Config1) 
            ? GenerateDefaultAttributes(ringOneNumSpheres)  // Ring 1 is inactive in Config1
            : GenerateRandomAttributes(ringOneNumSpheres);
        for (int i = 0; i < ringOneNumSpheres; i++)
        {
            allColors[i] = innerColors[i];
        }
        
        // Middle ring (Ring 2, if 3 rings)
        if (numberOfRings == 3)
        {
            var middleColors = GenerateRandomAttributes(ringTwoNumSpheres); // Ring 2 is always active
            for (int i = 0; i < ringTwoNumSpheres; i++)
            {
                allColors[ringOneNumSpheres + i] = middleColors[i];
            }
        }
        
        // Outer ring (Ring 3)
        if (ringThreeNumSpheres > 0)
        {
            var outerColors = (genericInactiveRing && config == RingConfiguration.Config2)
                ? GenerateDefaultAttributes(ringThreeNumSpheres)  // Ring 3 is inactive in Config2
                : GenerateRandomAttributes(ringThreeNumSpheres);
            for (int i = 0; i < ringThreeNumSpheres; i++)
            {
                int idx = numberOfRings == 3 ? ringOneNumSpheres + ringTwoNumSpheres + i : ringOneNumSpheres + i;
                allColors[idx] = outerColors[i];
            }
        }
        
        if (genericInactiveRing)
        {
            Debug.Log($"[GameManager] Generated colors with generic inactive ring (Config: {config}): {string.Join(", ", allColors)}");
        }
        else
        {
            Debug.Log($"[GameManager] Generated random colors: {string.Join(", ", allColors)}");
        }
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
        else if (changeValue)
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
        else if (changeValue)
        {
            var values = result.Select(r => float.Parse(r)).ToArray();
            Debug.Log($"[RandomCheck] Ring with {sphereCount} spheres - Values: [{string.Join(", ", values.Select(v => v.ToString("F3")))}]");
            Debug.Log($"[QualityCheck] Value distribution - Min: {values.Min():F3}, Max: {values.Max():F3}, Range: {(values.Max() - values.Min()):F3}");
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
        else if (changeValue)
        {
            return IsValueSetAcceptable(values);
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
    private bool IsValueSetAcceptable(float[] values)
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
        else if (changeValue)
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
        // Loop through all inner spheres
        if (innerSpheres != null)
        {
            foreach (GameObject sphere in innerSpheres)
            {
                // If the sphere exists destroy it
                if (sphere != null)
                {
                    Destroy(sphere); // Destroy each sphere in the array
                }
            }
        }
        
        // Loop through all middle spheres
        if (middleSpheres != null)
        {
            foreach (GameObject sphere in middleSpheres)
            {
                // If the sphere exists destroy it
                if (sphere != null)
                {
                    Destroy(sphere); // Destroy each sphere in the array
                }
            }
        }
        
        // Loop through all outer spheres
        if (outerSpheres != null)
        {
            foreach (GameObject sphere in outerSpheres)
            {
                // If the sphere exists destroy it
                if (sphere != null)
                {
                    Destroy(sphere); // Destroy each sphere in the array
                }
            }
        }
        
        // Create new sphere arrays for the next set
        innerSpheres = new GameObject[ringOneNumSpheres];
        if (numberOfRings >= 3)
        {
            middleSpheres = new GameObject[ringTwoNumSpheres];
            outerSpheres = new GameObject[ringThreeNumSpheres];
        }
        else if (numberOfRings >= 2)
        {
            outerSpheres = new GameObject[ringThreeNumSpheres];
        }
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

    // Run a rotational trial
    /*private string[] RotationalTrial(int sphereToChange, bool addChange, string trialType)
    {
        DestroySpheres();
        if (focusPointText != null)
            focusPointText.enabled = true;
        string[] sphereColors = CreateRingOfSpheres(centerPoint);
        StartCoroutine(RotationalChangeAndMove(sphereToChange, addChange, trialType));
        return sphereColors;
    }*/

    private string[] RotationalOneDirTrial(int sphereToChange, bool addChange, RingMovementType moveType)
    {
        DestroySpheres();
        if (focusPointText != null)
            focusPointText.enabled = true;
        string[] sphereColors = CreateRingOfSpheres(centerPoint);
        StartCoroutine(RotationalOneDirCoroutine(sphereToChange, addChange, moveType));
        return sphereColors;
    }

    private System.Collections.IEnumerator RotationalOneDirCoroutine(int sphereToChange, bool addChange, RingMovementType moveType)
    {
        canClick = false;
        trialActive = true;
        // Only blink if blinkSpheres is enabled
        if (blinkSpheres)
        {
            // Always blink Ring 2 (middle ring) since only Ring 2 spheres are selectable
            if (numberOfRings == 3 && middleSpheres != null)
                yield return StartCoroutine(BlinkRing(middleSpheres));
            else if (numberOfRings < 3)
            {
                // Legacy fallback for 2-ring or 1-ring systems
                GameObject[] ringToBlink = changeOuterRingSphere ? outerSpheres : innerSpheres;
                if (ringToBlink != null)
                    yield return StartCoroutine(BlinkRing(ringToBlink));
            }
        }
            
        yield return new WaitForSeconds(movementStartDelay);

        // Calculate when motion is expected to stop
        expectedMotionStopTime = Time.time + trialLength;

        float elapsedTime = 0f;
        float currentAngleInner = 0f;
        float currentAngleOuter = 0f;
        int direction = Random.Range(0, 2) == 0 ? 1 : -1;
        float innerSpeed = rotationSpeed * direction;
        float outerSpeed = (separateOuterRingChangeMagnitudes ? outerRingRotationSpeed : rotationSpeed) * direction;

        // Configuration-aware movement logic
        bool moveInner = false;
        bool moveOuter = false;
        
        if (numberOfRings == 3)
        {
            if (currentRingConfig == RingConfiguration.Config1)
            {
                // Config1: Ring 2 & 3 active, Ring 1 inactive
                // "Inner" trials = Ring 2, "Outer" trials = Ring 3, "Both" trials = Ring 2 & 3
                switch (moveType)
                {
                    case RingMovementType.Inner:
                        moveInner = true;  // Ring 2 (middle) acts as "inner" in Config1
                        moveOuter = false;
                        break;
                    case RingMovementType.Outer:
                        moveInner = false;
                        moveOuter = true;  // Ring 3 (outer) acts as "outer" in Config1
                        break;
                    case RingMovementType.Both:
                        moveInner = true;  // Ring 2 (middle)
                        moveOuter = true;  // Ring 3 (outer)
                        break;
                }
            }
            else if (currentRingConfig == RingConfiguration.Config2)
            {
                // Config2: Ring 1 & 2 active, Ring 3 inactive
                // "Inner" trials = Ring 1, "Outer" trials = Ring 2, "Both" trials = Ring 1 & 2
                switch (moveType)
                {
                    case RingMovementType.Inner:
                        moveInner = true;  // Ring 1 (inner) acts as "inner" in Config2
                        moveOuter = false;
                        break;
                    case RingMovementType.Outer:
                        moveInner = false;
                        moveOuter = true;  // Ring 2 (middle) acts as "outer" in Config2
                        break;
                    case RingMovementType.Both:
                        moveInner = true;  // Ring 1 (inner)
                        moveOuter = true;  // Ring 2 (middle)
                        break;
                }
            }
        }
        else
        {
            // Legacy 2-ring or 1-ring system
            moveInner = (moveType == RingMovementType.Inner || moveType == RingMovementType.Both);
            moveOuter = (moveType == RingMovementType.Outer || moveType == RingMovementType.Both);
        }
        
        Debug.Log($"[RotationalOneDirCoroutine] moveType: {moveType}, Config: {currentRingConfig}, moveInner: {moveInner}, moveOuter: {moveOuter}");

        bool changeApplied = false;
        bool useTwoDir = false;
        if (oneDirectionTrials && twoDirectionTrials)
            useTwoDir = Random.Range(0, 2) == 0 ? false : true;
        else if (twoDirectionTrials)
            useTwoDir = true;
        // else useTwoDir remains false (one direction)

        float halfTrial = trialLength / 2f;
        float beepSequenceDuration = soundInterval * 4f; // 3 beeps + 1 interval before high beep
        float countdownStart = halfTrial - beepSequenceDuration;
        bool countdownStarted = false;
        while (elapsedTime < trialLength && trialActive)
        {
            float deltaTime = Time.deltaTime;
            currentAngleInner += moveInner ? innerSpeed * deltaTime : 0f;
            currentAngleOuter += moveOuter ? outerSpeed * deltaTime : 0f;
            
            // Update sphere positions based on ring configuration and movement type
            if (numberOfRings == 3)
            {
                if (currentRingConfig == RingConfiguration.Config1)
                {
                    // Config1: Ring 2 & 3 active, Ring 1 inactive
                    // For this config: Ring 2 acts as "inner", Ring 3 acts as "outer"
                    // Make single call to prevent inactive ring rotation
                    UpdateSpherePositionsByRotation(currentAngleInner, moveInner, moveOuter, currentAngleOuter);
                }
                else if (currentRingConfig == RingConfiguration.Config2)
                {
                    // Config2: Ring 1 & 2 active, Ring 3 inactive
                    // For this config: Ring 1 acts as "inner", Ring 2 acts as "outer"
                    // Make single call to prevent inactive ring rotation
                    UpdateSpherePositionsByRotation(currentAngleInner, moveInner, moveOuter, currentAngleOuter);
                }
            }
            else
            {
                // Legacy 2-ring or 1-ring system
                UpdateSpherePositionsByRotation(currentAngleInner, moveInner, moveOuter, currentAngleOuter);
            }
            
            elapsedTime += deltaTime;

            // Start countdown beeps and change at the right time
            if (!countdownStarted && elapsedTime >= countdownStart)
            {
                countdownStarted = true;
                StartCoroutine(PlayBeepsAndChange(3, soundInterval, () => {
                    if (!changeApplied)
                    {
                        // Always select from Ring 2 (middle ring) since only Ring 2 spheres are selectable
                        GameObject selectedSphere = null;
                        
                        if (numberOfRings == 3 && middleSpheres != null && sphereToChange >= 0 && sphereToChange < middleSpheres.Length)
                        {
                            selectedSphere = middleSpheres[sphereToChange];
                        }
                        else if (numberOfRings < 3)
                        {
                            // Legacy 2-ring or 1-ring system fallback
                            GameObject[] targetArray = changeOuterRingSphere ? outerSpheres : innerSpheres;
                            if (targetArray != null && sphereToChange >= 0 && sphereToChange < targetArray.Length)
                                selectedSphere = targetArray[sphereToChange];
                        }
                        
                        if (selectedSphere != null)
                        {
                            SphereManager sphereManager = selectedSphere.GetComponent<SphereManager>();
                            sphereManager.SetChanged(true);
                            ChangeSphere(selectedSphere, addChange);
                            changeApplied = true;
                            changeTime = Time.time;
                            canClick = true;
                            if (useTwoDir)
                            {
                                direction *= -1;
                                innerSpeed = rotationSpeed * direction;
                                outerSpeed = (separateOuterRingChangeMagnitudes ? outerRingRotationSpeed : rotationSpeed) * direction;
                            }
                        }
                    }
                }));
            }
            yield return null;
        }
        // Record attendant motion stop time and all motion stop time
        attendantMotionStopTime = Time.time;
        allMotionStopTime = Time.time;
        // Remove canClick = true here
    }

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
        // This coroutine is now identical to RotationalOneDirCoroutine, so just call it
        yield return StartCoroutine(RotationalOneDirCoroutine(sphereToChange, addChange, moveType));
    }

    /*private System.Collections.IEnumerator RotationalChangeAndMove(int sphereToChange, bool addChange, string trialType)
    {
        canClick = false;
        yield return new WaitForSeconds(movementStartDelay);

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
    private void UpdateSpherePositionsByRotation(float rotationAngle, bool rotateInner, bool rotateOuter, float outerRotationAngle = 0f)
    {
        // EMERGENCY: Add performance monitoring to prevent freezes
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // DEBUG: Log what's happening
        Debug.Log($"[UpdateSpherePositions] Config: {currentRingConfig}, rotateInner: {rotateInner}, rotateOuter: {rotateOuter}, numberOfRings: {numberOfRings}");
        Debug.Log($"[UpdateSpherePositions] changeOrientation: {changeOrientation}, ferrisWheelSpin: {ferrisWheelSpin}");
        
        // EMERGENCY: Force parent rotation method to avoid trigonometric hang
        /*if (changeOrientation && ferrisWheelSpin)
        {
            Debug.LogWarning("[UpdateSpherePositions] EMERGENCY: Forcing parent rotation method to prevent ferris wheel freeze!");
        }*/
        
        // For orientation trials, check ferrisWheelSpin to determine rotation method
        // For other trials (hue, value, size), always use parent rotation (more efficient)
        if (changeOrientation && ferrisWheelSpin) // Per-sphere trig math method for orientation trials
        {
            Debug.Log($"[UpdateSpherePositions] Using ferrisWheelSpin method for orientation trial");
            // Handle 3-ring system with different configurations
            if (numberOfRings == 3)
            {
                if (currentRingConfig == RingConfiguration.Config1)
                {
                    // Config1: Ring 2 & 3 active, Ring 1 inactive
                    // rotateInner (changeInnerRing trials) = rotate Ring 2 (middleSpheres)
                    // rotateOuter (changeOuterRing trials) = rotate Ring 3 (outerSpheres)
                    if (rotateInner && middleSpheres != null)
                    {
                        float middleOffset = Mathf.PI / ringTwoNumSpheres;
                        for (int i = 0; i < middleSpheres.Length; i++)
                        {
                            float angle = i * Mathf.PI * 2 / ringTwoNumSpheres + Mathf.Deg2Rad * rotationAngle + middleOffset;
                            float x = centerPoint.x + Mathf.Cos(angle) * ringTwoRadius;
                            float y = centerPoint.y + Mathf.Sin(angle) * ringTwoRadius;
                            middleSpheres[i].transform.position = new Vector3(x, y, centerPoint.z);
                            // For ferrisWheelSpin, preserve each sphere's original orientation (no rotation change)
                            // Each sphere keeps its initial rotation while only position moves around the ring
                        }
                    }
                    if (rotateOuter && outerSpheres != null)
                    {
                        float useAngle = outerRotationAngle == 0f ? rotationAngle : outerRotationAngle;
                        float outerOffset = Mathf.PI / ringThreeNumSpheres + (Mathf.PI / ringTwoNumSpheres);
                        for (int i = 0; i < outerSpheres.Length; i++)
                        {
                            if (outerSpheres[i] == null) continue;
                            float angle = i * Mathf.PI * 2 / ringThreeNumSpheres + Mathf.Deg2Rad * useAngle + outerOffset;
                            float x = centerPoint.x + Mathf.Cos(angle) * ringThreeRadius;
                            float y = centerPoint.y + Mathf.Sin(angle) * ringThreeRadius;
                            outerSpheres[i].transform.position = new Vector3(x, y, centerPoint.z);
                            // For ferrisWheelSpin, preserve each sphere's original orientation (no rotation change)
                            // Each sphere keeps its initial rotation while only position moves around the ring
                        }
                    }
                    // Ring 1 should NEVER rotate in Config1 - it's inactive
                }
                else if (currentRingConfig == RingConfiguration.Config2)
                {
                    // Config2: Ring 1 & 2 active, Ring 3 inactive
                    // Ring 1 = "inner", Ring 2 = "outer"
                    if (rotateInner && innerSpheres != null)
                    {
                        for (int i = 0; i < innerSpheres.Length; i++)
                        {
                            float angle = i * Mathf.PI * 2 / ringOneNumSpheres + Mathf.Deg2Rad * rotationAngle;
                            float x = centerPoint.x + Mathf.Cos(angle) * ringOneRadius;
                            float y = centerPoint.y + Mathf.Sin(angle) * ringOneRadius;
                            innerSpheres[i].transform.position = new Vector3(x, y, centerPoint.z);
                            // For ferrisWheelSpin, preserve each sphere's original orientation (no rotation change)
                            // Each sphere keeps its initial rotation while only position moves around the ring
                        }
                    }
                    if (rotateOuter && middleSpheres != null)
                    {
                        float useAngle = outerRotationAngle == 0f ? rotationAngle : outerRotationAngle;
                        float middleOffset = Mathf.PI / ringTwoNumSpheres;
                        for (int i = 0; i < middleSpheres.Length; i++)
                        {
                            if (middleSpheres[i] == null) continue;
                            float angle = i * Mathf.PI * 2 / ringTwoNumSpheres + Mathf.Deg2Rad * useAngle + middleOffset;
                            float x = centerPoint.x + Mathf.Cos(angle) * ringTwoRadius;
                            float y = centerPoint.y + Mathf.Sin(angle) * ringTwoRadius;
                            middleSpheres[i].transform.position = new Vector3(x, y, centerPoint.z);
                            // For ferrisWheelSpin, preserve each sphere's original orientation (no rotation change)
                            // Each sphere keeps its initial rotation while only position moves around the ring
                        }
                    }
                }
            }
            else
            {
                // Legacy 2-ring or 1-ring system
                if (rotateInner && innerSpheres != null)
                {
                    for (int i = 0; i < innerSpheres.Length; i++)
                    {
                        float angle = i * Mathf.PI * 2 / ringOneNumSpheres + Mathf.Deg2Rad * rotationAngle;
                        float x = centerPoint.x + Mathf.Cos(angle) * ringOneRadius;
                        float y = centerPoint.y + Mathf.Sin(angle) * ringOneRadius;
                        innerSpheres[i].transform.position = new Vector3(x, y, centerPoint.z);
                        // For ferrisWheelSpin, preserve each sphere's original orientation (no rotation change)
                        // Each sphere keeps its initial rotation while only position moves around the ring
                    }
                }
                if (rotateOuter && numberOfRings == 2 && outerSpheres != null)
                {
                    float useAngle = outerRotationAngle == 0f ? rotationAngle : outerRotationAngle;
                    float outerOffset = Mathf.PI / ringThreeNumSpheres; // Keep outer spheres offset during rotation
                    for (int i = 0; i < outerSpheres.Length; i++)
                    {
                        if (outerSpheres[i] == null) continue;
                        float angle = i * Mathf.PI * 2 / ringThreeNumSpheres + Mathf.Deg2Rad * useAngle + outerOffset;
                        float x = centerPoint.x + Mathf.Cos(angle) * ringThreeRadius;
                        float y = centerPoint.y + Mathf.Sin(angle) * ringThreeRadius;
                        outerSpheres[i].transform.position = new Vector3(x, y, centerPoint.z);
                        // For ferrisWheelSpin, preserve each sphere's original orientation (no rotation change)
                        // Each sphere keeps its initial rotation while only position moves around the ring
                    }
                }
            }
        }
        else // Parent rotation method - used for all non-orientation trials and orientation trials when ferrisWheelSpin=false
        {
            // Handle 3-ring system with different configurations
            if (numberOfRings == 3)
            {
                if (currentRingConfig == RingConfiguration.Config1)
                {
                    // Config1: Ring 2 & 3 active, Ring 1 inactive
                    // SAFETY CHECK: Ring 3 should NOT be transparent in Config1 (skip for orientation trials)
                    if (!changeOrientation && outerSpheres != null && outerSpheres.Length > 0 && outerSpheres[0] != null)
                    {
                        Renderer testRenderer = outerSpheres[0].GetComponent<Renderer>();
                        if (testRenderer != null && testRenderer.material.color.a < 0.9f)
                        {
                            Debug.LogError($"[MISMATCH DETECTED] Config1 but Ring 3 is transparent (alpha={testRenderer.material.color.a})! This should not happen.");
                        }
                    }
                    else if (changeOrientation)
                    {
                        Debug.Log($"[Config1] Skipping Ring 3 transparency check for orientation trial (striped materials don't have _Color property)");
                    }
                    
                    // rotateInner (changeInnerRing trials) = rotate Ring 2 (middleRingParent)
                    // rotateOuter (changeOuterRing trials) = rotate Ring 3 (outerRingParent)
                    Debug.Log($"[Config1] rotateInner: {rotateInner}, rotateOuter: {rotateOuter}");
                    
                    if (rotateInner && middleRingParent != null)
                    {
                        Debug.Log($"[Config1] Rotating Ring 2 (middleRingParent) as INNER by {rotationAngle} degrees");
                        middleRingParent.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
                    }
                    
                    if (rotateOuter && outerRingParent != null)
                    {
                        float useAngle = outerRotationAngle == 0f ? rotationAngle : outerRotationAngle;
                        Debug.Log($"[Config1] Rotating Ring 3 (outerRingParent) as OUTER by {useAngle} degrees");
                        outerRingParent.transform.rotation = Quaternion.Euler(0, 0, useAngle);
                    }
                    
                    // Ring 1 should NEVER rotate in Config1 - it's inactive
                    Debug.Log($"[Config1] Ring 1 (innerRingParent) intentionally NOT rotated - inactive ring");
                }
                else if (currentRingConfig == RingConfiguration.Config2)
                {
                    // Config2: Ring 1 & 2 active, Ring 3 inactive
                    // SAFETY CHECK: Ring 1 should NOT be transparent in Config2 (skip for orientation trials)
                    if (!changeOrientation && innerSpheres != null && innerSpheres.Length > 0 && innerSpheres[0] != null)
                    {
                        Renderer testRenderer = innerSpheres[0].GetComponent<Renderer>();
                        if (testRenderer != null && testRenderer.material.color.a < 0.9f)
                        {
                            Debug.LogError($"[MISMATCH DETECTED] Config2 but Ring 1 is transparent (alpha={testRenderer.material.color.a})! This should not happen.");
                        }
                    }
                    else if (changeOrientation)
                    {
                        Debug.Log($"[Config2] Skipping Ring 1 transparency check for orientation trial (striped materials don't have _Color property)");
                    }
                    
                    // For this config: Ring 1 acts as "inner", Ring 2 acts as "outer"
                    // rotateInner = should Ring 1 (inner) rotate?
                    // rotateOuter = should Ring 2 (middle) rotate?
                    Debug.Log($"[Config2] rotateInner: {rotateInner}, rotateOuter: {rotateOuter}");
                    if (rotateInner && innerRingParent != null)
                    {
                        Debug.Log($"[Config2] Rotating Ring 1 (innerRingParent) by {rotationAngle} degrees");
                        innerRingParent.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
                    }
                    if (rotateOuter && middleRingParent != null)
                    {
                        float useAngle = outerRotationAngle == 0f ? rotationAngle : outerRotationAngle;
                        Debug.Log($"[Config2] Rotating Ring 2 (middleRingParent) by {useAngle} degrees");
                        middleRingParent.transform.rotation = Quaternion.Euler(0, 0, useAngle);
                    }
                    // Ring 3 (outerRingParent) should NEVER rotate in Config2 - it's inactive
                    // Do not rotate outerRingParent under any circumstances in Config2
                    Debug.Log($"[Config2] Ring 3 (outerRingParent) intentionally NOT rotated - inactive ring");
                }
            }
            else
            {
                // Legacy 2-ring or 1-ring system
                if (rotateInner && innerRingParent != null)
                {
                    innerRingParent.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
                }
                if (rotateOuter && numberOfRings == 2 && outerRingParent != null)
                {
                    float useAngle = outerRotationAngle == 0f ? rotationAngle : outerRotationAngle;
                    outerRingParent.transform.rotation = Quaternion.Euler(0, 0, useAngle);
                }
            }
        }
        
        // EMERGENCY: Log performance to detect hangs
        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds > 100)
        {
            Debug.LogError($"[PERFORMANCE WARNING] UpdateSpherePositionsByRotation took {stopwatch.ElapsedMilliseconds}ms - potential freeze risk!");
        }
        else
        {
            Debug.Log($"[PERFORMANCE] UpdateSpherePositionsByRotation completed in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    // Create a set of spheres
    private string[] CreateRingOfSpheres(Vector3 center)
    {
        // ALWAYS generate fresh random colors for each trial (change blindness requires different colors)
        Debug.Log("[GameManager] Generating fresh random spheres for this trial");
        return CreateRingOfSpheresWithGeneration(center);
    }
    
    // Create spheres with pre-calculated colors (fast path)
    private string[] CreateRingOfSpheresWithColors(Vector3 center, string[] preCalculatedColors)
    {
        Debug.Log($"[CreateRingOfSpheres] Starting sphere creation - ChangeType: {changeType}, numberOfRings: {numberOfRings}, Config: {currentRingConfig}");
        
        // Destroy previous parents if they exist
        if (innerRingParent != null) Destroy(innerRingParent);
        if (outerRingParent != null) Destroy(outerRingParent);
        if (middleRingParent != null) Destroy(middleRingParent);
        
        // Create new ring parents with identity rotation to prevent jumps
        innerRingParent = new GameObject("InnerRing");
        innerRingParent.transform.position = center;
        innerRingParent.transform.rotation = Quaternion.identity;
        
        if (numberOfRings == 3)
        {
            middleRingParent = new GameObject("MiddleRing");
            middleRingParent.transform.position = center;
            middleRingParent.transform.rotation = Quaternion.identity;
        }
        if (numberOfRings >= 2)
        {
            outerRingParent = new GameObject("OuterRing");
            outerRingParent.transform.position = center;
            outerRingParent.transform.rotation = Quaternion.identity;
        }
        
        // Allocate arrays
        if (numberOfRings == 3)
        {
            innerSpheres = new GameObject[ringOneNumSpheres];
            middleSpheres = new GameObject[ringTwoNumSpheres];
            outerSpheres = new GameObject[ringThreeNumSpheres];
        }
        else if (numberOfRings == 2)
        {
            innerSpheres = new GameObject[ringOneNumSpheres];
            outerSpheres = new GameObject[ringThreeNumSpheres];
        }
        else
        {
            innerSpheres = new GameObject[ringOneNumSpheres];
        }
        
        // Create inner ring with pre-calculated colors
        Debug.Log($"[CreateRingOfSpheresWithColors] Creating Ring 1 (innerSpheres) with config {currentRingConfig}");
        for (int i = 0; i < ringOneNumSpheres; i++)
        {
            float angle = i * Mathf.PI * 2 / ringOneNumSpheres;
            float x = center.x + Mathf.Cos(angle) * ringOneRadius;
            float y = center.y + Mathf.Sin(angle) * ringOneRadius;
            Vector3 position = new Vector3(x, y, center.z);
            GameObject sphere = Instantiate(GetSpherePrefabForRing("inner"), position, Quaternion.identity);
            sphere.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);
            sphere.name = $"inner_{i}";
            
            // Apply pre-calculated attribute
            ApplyAttributeToSphere(sphere, preCalculatedColors[i]);
            
            // Handle inactive ring transparency and interaction
            if (numberOfRings == 3 && currentRingConfig == RingConfiguration.Config1)
            {
                // Ring 1 is inactive
                Debug.Log($"[CreateRingOfSpheres] Ring 1 sphere {i+1}: Setting transparency (Config1 - Ring 1 inactive)");
                Renderer renderer = sphere.GetComponent<Renderer>();
                if (renderer != null && !changeOrientation)
                {
                    // Only modify color for non-orientation trials (striped materials don't have _Color property)
                    Color c = renderer.material.color;
                    c.a = 1f - inactiveRingTransparency;
                    renderer.material.color = c;
                    SetMaterialTransparent(renderer);
                }
                else if (changeOrientation && renderer != null)
                {
                    // For orientation trials, use multiple approaches to indicate inactive state since striped material properties vary
                    Debug.Log($"[CreateRingOfSpheres] Ring 1 orientation sphere {i+1}: Setting transparency for orientation trial (Config1 - Ring 1 inactive)");
                    Debug.Log($"[CreateRingOfSpheres] inactiveRingTransparency = {inactiveRingTransparency}");
                    float alphaValue = 1f - inactiveRingTransparency;
                    Debug.Log($"[CreateRingOfSpheres] Calculated alphaValue = {alphaValue}");
                    
                    // Try multiple material properties that might control transparency
                    if (renderer.material.HasProperty("_Alpha"))
                    {
                        renderer.material.SetFloat("_Alpha", alphaValue);
                        Debug.Log($"[CreateRingOfSpheres] Set _Alpha to {alphaValue}");
                    }
                    else
                    {
                        Debug.Log($"[CreateRingOfSpheres] Material does NOT have _Alpha property");
                    }
                    
                    if (renderer.material.HasProperty("_Color"))
                    {
                        Color originalColor = renderer.material.color;
                        Debug.Log($"[CreateRingOfSpheres] Original _Color: {originalColor}");
                        Color c = renderer.material.color;
                        c.a = alphaValue;
                        renderer.material.color = c;
                        Debug.Log($"[CreateRingOfSpheres] Set _Color alpha to {alphaValue}, new color: {renderer.material.color}");
                    }
                    else
                    {
                        Debug.Log($"[CreateRingOfSpheres] Material does NOT have _Color property");
                    }
                    
                    SetMaterialTransparent(renderer);
                }
                Collider col = sphere.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            
            DisableSphereInteraction(sphere);
            sphere.transform.parent = innerRingParent.transform;
            innerSpheres[i] = sphere;
        }
        // --- Middle ring (if 3 rings) ---
        if (numberOfRings == 3)
        {
            Debug.Log($"[CreateRingOfSpheresWithColors] Creating Ring 2 (middleSpheres) with config {currentRingConfig}");
            float middleOffset = Mathf.PI / ringTwoNumSpheres;
            for (int i = 0; i < ringTwoNumSpheres; i++)
            {
                float angle = i * Mathf.PI * 2 / ringTwoNumSpheres + middleOffset;
                float x = center.x + Mathf.Cos(angle) * ringTwoRadius;
                float y = center.y + Mathf.Sin(angle) * ringTwoRadius;
                Vector3 position = new Vector3(x, y, center.z);
                GameObject sphere = Instantiate(GetSpherePrefabForRing("middle"), position, Quaternion.identity);
                sphere.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);
                sphere.name = $"middle_{i}";
                
                // Apply pre-calculated attribute
                ApplyAttributeToSphere(sphere, preCalculatedColors[ringOneNumSpheres + i]);
                
                // Ring 2 (middle) spheres are the only ones selectable by user - keep interactions enabled
                sphere.transform.parent = middleRingParent.transform;
                middleSpheres[i] = sphere;
            }
        }
        // --- Outer ring ---
        if (ringThreeNumSpheres > 0)
        {
            Debug.Log($"[CreateRingOfSpheresWithColors] Creating Ring 3 (outerSpheres) with config {currentRingConfig}");
            float outerOffset = Mathf.PI / ringThreeNumSpheres;
            if (numberOfRings == 3)
            {
                outerOffset += (Mathf.PI / ringTwoNumSpheres);
            }
            
            for (int i = 0; i < ringThreeNumSpheres; i++)
            {
                float angle = i * Mathf.PI * 2 / ringThreeNumSpheres + outerOffset;
                float x = center.x + Mathf.Cos(angle) * ringThreeRadius;
                float y = center.y + Mathf.Sin(angle) * ringThreeRadius;
                Vector3 position = new Vector3(x, y, center.z);
                GameObject sphere = Instantiate(GetSpherePrefabForRing("outer"), position, Quaternion.identity);
                sphere.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);
                sphere.name = $"outer_{i}";
                
                // Apply pre-calculated attribute
                int idx = numberOfRings == 3 ? ringOneNumSpheres + ringTwoNumSpheres + i : ringOneNumSpheres + i;
                ApplyAttributeToSphere(sphere, preCalculatedColors[idx]);
                
                // Check if Ring 3 should be inactive (Config2: Ring 1&2 active, Ring 3 inactive)
                if (numberOfRings == 3 && currentRingConfig == RingConfiguration.Config2)
                {
                    // Ring 3 is inactive - set transparency and disable collider
                    Debug.Log($"[CreateRingOfSpheres] Ring 3 sphere {i+1}: Setting transparency (Config2 - Ring 3 inactive)");
                    Renderer renderer = sphere.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Color c = renderer.material.color;
                        c.a = 1f - inactiveRingTransparency;
                        renderer.material.color = c;
                        SetMaterialTransparent(renderer);
                    }
                    Collider col = sphere.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                }
                
                // Ring 3 spheres are never selectable by user (only Ring 2 is selectable)
                DisableSphereInteraction(sphere);
                sphere.transform.parent = outerRingParent.transform;
                outerSpheres[i] = sphere;
            }
        }
        
        return preCalculatedColors;
    }
    
    // Create spheres with real-time generation (fallback/original path)
    private string[] CreateRingOfSpheresWithGeneration(Vector3 center)
    {
        // Destroy previous parents if they exist
        if (innerRingParent != null) Destroy(innerRingParent);
        if (outerRingParent != null) Destroy(outerRingParent);
        if (middleRingParent != null) Destroy(middleRingParent);
        
        // Create new ring parents with identity rotation to prevent jumps
        innerRingParent = new GameObject("InnerRing");
        innerRingParent.transform.position = center;
        innerRingParent.transform.rotation = Quaternion.identity;
        
        if (numberOfRings == 3)
        {
            middleRingParent = new GameObject("MiddleRing");
            middleRingParent.transform.position = center;
            middleRingParent.transform.rotation = Quaternion.identity;
        }
        if (numberOfRings >= 2)
        {
            outerRingParent = new GameObject("OuterRing");
            outerRingParent.transform.position = center;
            outerRingParent.transform.rotation = Quaternion.identity;
        }
        // Allocate arrays
        if (numberOfRings == 3)
        {
            innerSpheres = new GameObject[ringOneNumSpheres];
            middleSpheres = new GameObject[ringTwoNumSpheres];
            outerSpheres = new GameObject[ringThreeNumSpheres];
        }
        else if (numberOfRings == 2)
        {
            innerSpheres = new GameObject[ringOneNumSpheres];
            outerSpheres = new GameObject[ringThreeNumSpheres];
        }
        else
        {
            innerSpheres = new GameObject[ringOneNumSpheres];
        }
        // Build color array
        int totalSpheres = ringOneNumSpheres + (numberOfRings == 3 ? ringTwoNumSpheres : 0) + (numberOfRings >= 2 ? ringThreeNumSpheres : 0);
        string[] allColors = new string[totalSpheres];
        
        // Generate random attributes for each ring
        
        // Generate colors for inner ring
        var innerColors = (genericInactiveRing && currentRingConfig == RingConfiguration.Config1) 
            ? GenerateDefaultAttributes(ringOneNumSpheres)  // Ring 1 is inactive in Config1
            : GenerateRandomAttributes(ringOneNumSpheres);
        for (int i = 0; i < ringOneNumSpheres; i++)
        {
            allColors[i] = innerColors[i];
        }
        
        // Generate colors for middle ring (if exists)
        System.Collections.Generic.List<string> middleColors = null;
        if (numberOfRings == 3)
        {
            middleColors = GenerateRandomAttributes(ringTwoNumSpheres); // Ring 2 is always active
            for (int i = 0; i < ringTwoNumSpheres; i++)
            {
                allColors[ringOneNumSpheres + i] = middleColors[i];
            }
        }
        
        // Generate colors for outer ring
        System.Collections.Generic.List<string> outerColors = null;
        if (ringThreeNumSpheres > 0)
        {
            outerColors = (genericInactiveRing && currentRingConfig == RingConfiguration.Config2)
                ? GenerateDefaultAttributes(ringThreeNumSpheres)  // Ring 3 is inactive in Config2
                : GenerateRandomAttributes(ringThreeNumSpheres);
            for (int i = 0; i < ringThreeNumSpheres; i++)
            {
                int idx = numberOfRings == 3 ? ringOneNumSpheres + ringTwoNumSpheres + i : ringOneNumSpheres + i;
                allColors[idx] = outerColors[i];
            }
        }
        
        if (genericInactiveRing)
        {
            Debug.Log($"[GameManager] Generated colors with generic inactive ring (Config: {currentRingConfig}) - Inner: [{string.Join(", ", innerColors)}], Middle: [{(middleColors != null ? string.Join(", ", middleColors) : "none")}], Outer: [{(outerColors != null ? string.Join(", ", outerColors) : "none")}]");
        }
        else
        {
            Debug.Log($"[GameManager] Generated random colors - Inner: [{string.Join(", ", innerColors)}], Middle: [{(middleColors != null ? string.Join(", ", middleColors) : "none")}], Outer: [{(outerColors != null ? string.Join(", ", outerColors) : "none")}]");
        }
        
        // --- Inner ring ---
        for (int i = 0; i < ringOneNumSpheres; i++)
        {
            float angle = i * Mathf.PI * 2 / ringOneNumSpheres;
            float x = center.x + Mathf.Cos(angle) * ringOneRadius;
            float y = center.y + Mathf.Sin(angle) * ringOneRadius;
            Vector3 position = new Vector3(x, y, center.z);
            GameObject sphere = Instantiate(GetSpherePrefabForRing("inner"), position, Quaternion.identity);
            sphere.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);
            sphere.name = $"inner_{i}";
            
            // Apply the pre-generated attribute to this sphere
            ApplyAttributeToSphere(sphere, innerColors[i]);
            
            // Check if Ring 1 should be inactive (Config1: Ring 2&3 active, Ring 1 inactive)
            if (numberOfRings == 3 && currentRingConfig == RingConfiguration.Config1)
            {
                // Ring 1 is inactive - set transparency and disable collider
                Renderer renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color c = renderer.material.color;
                    c.a = 1f - inactiveRingTransparency;
                    renderer.material.color = c;
                    SetMaterialTransparent(renderer);
                }
                Collider col = sphere.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            
            // Ring 1 spheres are never selectable by user (only Ring 2 is selectable)
            DisableSphereInteraction(sphere);
            
            sphere.transform.parent = innerRingParent.transform;
            innerSpheres[i] = sphere;
        }
        // --- Middle ring (if 3 rings) ---
        if (numberOfRings == 3)
        {
            System.Collections.Generic.List<string> middleAssignedColors = new System.Collections.Generic.List<string>();
            float middleOffset = Mathf.PI / ringTwoNumSpheres; // Offset relative to inner ring
            for (int i = 0; i < ringTwoNumSpheres; i++)
            {
                float angle = i * Mathf.PI * 2 / ringTwoNumSpheres + middleOffset;
                float x = center.x + Mathf.Cos(angle) * ringTwoRadius;
                float y = center.y + Mathf.Sin(angle) * ringTwoRadius;
                Vector3 position = new Vector3(x, y, center.z);
                GameObject sphere = Instantiate(GetSpherePrefabForRing("middle"), position, Quaternion.identity);
                sphere.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);
                sphere.name = $"middle_{i}";
                // Use pre-generated color from random list
                string sphereColor = middleColors[i];
                ApplyAttributeToSphere(sphere, sphereColor);
                allColors[ringOneNumSpheres + i] = sphereColor;
                
                // Ring 2 (middle) spheres are the only ones selectable by user - keep interactions enabled
                // No need to disable interaction components for Ring 2
                sphere.transform.parent = middleRingParent.transform;
                middleSpheres[i] = sphere;
            }
        }
        // --- Outer ring (formerly passive ring, now always present if ringThreeNumSpheres > 0) ---
        if (ringThreeNumSpheres > 0)
        {
            float outerOffset = Mathf.PI / ringThreeNumSpheres; // Offset relative to middle ring
            if (numberOfRings == 3)
            {
                // Calculate additional offset so outer ring is offset from middle ring
                outerOffset += (Mathf.PI / ringTwoNumSpheres);
            }
            System.Collections.Generic.List<string> outerAssignedColors = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ringThreeNumSpheres; i++)
            {
                float angle = i * Mathf.PI * 2 / ringThreeNumSpheres + outerOffset;
                float x = center.x + Mathf.Cos(angle) * ringThreeRadius;
                float y = center.y + Mathf.Sin(angle) * ringThreeRadius;
                Vector3 position = new Vector3(x, y, center.z);
                GameObject sphere = Instantiate(GetSpherePrefabForRing("outer"), position, Quaternion.identity);
                sphere.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);
                sphere.name = $"outer_{i}";
                Renderer renderer = sphere.GetComponent<Renderer>();
                
                if (numberOfRings == 3)
                {
                    // Check if Ring 3 should be inactive (Config2: Ring 1&2 active, Ring 3 inactive)
                    if (currentRingConfig == RingConfiguration.Config2)
                    {
                        // Ring 3 is inactive - assign random attributes but set transparency and disable collider
                        string sphereColor = outerColors[i];
                        ApplyAttributeToSphere(sphere, sphereColor);
                        int idx = ringOneNumSpheres + ringTwoNumSpheres + i;
                        allColors[idx] = sphereColor;
                        
                        // Set transparency after assigning attributes
                        if (renderer != null && !changeOrientation)
                        {
                            // Only modify color for non-orientation trials (striped materials don't have _Color property)
                            Color c = renderer.material.color;
                            c.a = 1f - inactiveRingTransparency;
                            renderer.material.color = c;
                            SetMaterialTransparent(renderer);
                        }
                        else if (changeOrientation)
                        {
                            // For orientation trials, use multiple approaches to indicate inactive state since striped material properties vary
                            Debug.Log($"[CreateRingOfSpheres] Ring 3 sphere {i+1}: Setting transparency for orientation trial (Config2 - Ring 3 inactive)");
                            float alphaValue = 1f - inactiveRingTransparency;
                            
                            // Try multiple material properties that might control transparency
                            if (renderer.material.HasProperty("_Alpha"))
                            {
                                renderer.material.SetFloat("_Alpha", alphaValue);
                                Debug.Log($"[CreateRingOfSpheres] Set _Alpha to {alphaValue}");
                            }
                            if (renderer.material.HasProperty("_Color"))
                            {
                                Color c = renderer.material.color;
                                c.a = alphaValue;
                                renderer.material.color = c;
                                Debug.Log($"[CreateRingOfSpheres] Set _Color alpha to {alphaValue}");
                            }
                            
                            SetMaterialTransparent(renderer);
                        }
                        Collider col = sphere.GetComponent<Collider>();
                        if (col != null) col.enabled = false;
                    }
                    else
                    {
                        // Ring 3 is active - assign random color
                        string sphereColor = outerColors[i];
                        ApplyAttributeToSphere(sphere, sphereColor);
                        int idx = ringOneNumSpheres + ringTwoNumSpheres + i;
                        allColors[idx] = sphereColor;
                    }
                    
                    // Ring 3 spheres are never selectable by user (only Ring 2 is selectable)
                    DisableSphereInteraction(sphere);
                }
                else
                {
                    // For 2-ring mode, Ring 3 behavior depends on configuration  
                    // This should not normally happen with 3-ring system, but keeping for compatibility
                    if (renderer != null)
                    {
                        Color c = renderer.material.color;
                        c.a = 1f - inactiveRingTransparency;
                        renderer.material.color = c;
                        SetMaterialTransparent(renderer);
                    }
                    Collider col = sphere.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                    outerAssignedColors.Add("");
                    int idx = ringOneNumSpheres + i;
                    allColors[idx] = "";
                    
                    // Ring 3 spheres are never selectable by user (only Ring 2 is selectable)
                    DisableSphereInteraction(sphere);
                }
                sphere.transform.parent = outerRingParent.transform;
                outerSpheres[i] = sphere;
            }
        }
        
        // DEBUG: Verify all rings were created properly
        Debug.Log($"[CreateRingOfSpheres] FINAL VERIFICATION - Trial Type: {changeType}");
        Debug.Log($"[CreateRingOfSpheres] - innerSpheres: {(innerSpheres?.Length ?? 0)} spheres, middleSpheres: {(middleSpheres?.Length ?? 0)} spheres, outerSpheres: {(outerSpheres?.Length ?? 0)} spheres");
        Debug.Log($"[CreateRingOfSpheres] - Ring parents - Inner: {(innerRingParent != null ? "EXISTS" : "NULL")}, Middle: {(middleRingParent != null ? "EXISTS" : "NULL")}, Outer: {(outerRingParent != null ? "EXISTS" : "NULL")}");
        
        return allColors;
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
                
                // Check if this sphere is in the inactive ring and should have zero saturation
                float saturation = sphereSaturation;
                if (genericInactiveRing && IsInactiveRingSphere(sphere))
                {
                    saturation = 0f; // Zero saturation for inactive ring when genericInactiveRing is true
                }
                
                Color newColor = Color.HSVToRGB(value, saturation, sphereValue);
                newColor.a = renderer.material.color.a; // Preserve alpha
                renderer.material.color = newColor;
            }
        }
        else if (changeValue)
        {
            // Apply value/brightness (0-1) - Value trials should always use zero saturation (grayscale)
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
        if (!currentRingConfigSet || sphere == null) return false;
        
        string sphereName = sphere.name;
        
        // Config1: Ring 2 & 3 active (Ring 1 inactive)
        // Config2: Ring 1 & 2 active (Ring 3 inactive)
        if (currentRingConfig == RingConfiguration.Config1)
        {
            // Ring 1 (inner) is inactive in Config1
            return sphereName.StartsWith("inner_");
        }
        else if (currentRingConfig == RingConfiguration.Config2)
        {
            // Ring 3 (outer) is inactive in Config2
            return sphereName.StartsWith("outer_");
        }
        
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
        
        Debug.Log($"[GetSpherePrefabForRing] Orientation trial - ringType: {ringType}, genericInactiveRing: {genericInactiveRing}, currentRingConfig: {currentRingConfig}");
        
        // For orientation trials, check if genericInactiveRing is enabled and this is an inactive ring
        if (genericInactiveRing)
        {
            bool isInactiveRing = false;
            
            if (currentRingConfig == RingConfiguration.Config1)
            {
                // Ring 1 (inner) is inactive in Config1
                isInactiveRing = ringType == "inner";
            }
            else if (currentRingConfig == RingConfiguration.Config2)
            {
                // Ring 3 (outer) is inactive in Config2
                isInactiveRing = ringType == "outer";
            }
            
            Debug.Log($"[GetSpherePrefabForRing] genericInactiveRing=true, isInactiveRing: {isInactiveRing}");
            
            if (isInactiveRing)
            {
                // Use normal spheres for inactive ring when genericInactiveRing is true
                Debug.Log($"[GetSpherePrefabForRing] Using spherePrefab for inactive ring {ringType}");
                return spherePrefab;
            }
        }
        
        // Use striped spheres for active rings in orientation trials (or all rings when genericInactiveRing is false)
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
        
        // For static trials, there's no motion, so set expectedMotionStopTime to 0
        expectedMotionStopTime = 0f;
        
        // Only blink if blinkSpheres is enabled
        if (blinkSpheres)
        {
            // Always blink Ring 2 (middle ring) since only Ring 2 spheres are selectable
            if (numberOfRings == 3 && middleSpheres != null)
                yield return StartCoroutine(BlinkRing(middleSpheres));
            else if (numberOfRings < 3)
            {
                // Legacy fallback for 2-ring or 1-ring systems
                GameObject[] ringToBlink = changeOuterRingSphere ? outerSpheres : innerSpheres;
                if (ringToBlink != null)
                    yield return StartCoroutine(BlinkRing(ringToBlink));
            }
        }
            
        // Wait for half of trial length minus beep sequence duration
        float preChangeTime = trialLength / 2f;
        float beepSequenceDuration = soundInterval * 4f; // 3 beeps + 1 interval before high beep
        if (preChangeTime > beepSequenceDuration)
            yield return new WaitForSeconds(preChangeTime - beepSequenceDuration);
        // Play beeps and make the change at the high beep
        yield return StartCoroutine(PlayBeepsAndChange(3, soundInterval, () => {
            // Always select from Ring 2 (middle ring) since only Ring 2 spheres are selectable
            GameObject selectedSphere = null;
            
            if (numberOfRings == 3 && middleSpheres != null && sphereToChange >= 0 && sphereToChange < middleSpheres.Length)
            {
                selectedSphere = middleSpheres[sphereToChange];
            }
            else if (numberOfRings < 3)
            {
                // Legacy 2-ring or 1-ring system fallback
                GameObject[] targetArray = changeOuterRingSphere ? outerSpheres : innerSpheres;
                if (targetArray != null && sphereToChange >= 0 && sphereToChange < targetArray.Length)
                    selectedSphere = targetArray[sphereToChange];
            }
            
            if (selectedSphere != null)
            {
                SphereManager sphereManager = selectedSphere.GetComponent<SphereManager>();
                sphereManager.SetChanged(true);
                ChangeSphere(selectedSphere, addChange);
                // Record change time and attendant motion stop time (static trial)
                changeTime = Time.time;
                attendantMotionStopTime = changeTime;
                canClick = true; // Enable clicking immediately after change
            }
            else
            {
                Debug.LogError($"[StaticChange] Could not find selected sphere. sphereToChange={sphereToChange}, ring2 only selection, config={currentRingConfig}");
                canClick = true;
            }
        }));
        // Wait for another half trial length before the next iteration
        yield return new WaitForSeconds(trialLength / 2f);
        if (!trialActive) yield break;
        // Record all motion stop time
        allMotionStopTime = Time.time;
        // Remove canClick = true here
    }   

    // Make a random change and move spheres
    /*System.Collections.IEnumerator DirectionChangeHueandMove(Vector3 startCenter, Vector3 endCenter, int sphereToChange, bool addChange, string trialType = "")
    {
        canClick = false; // Disable clicking during this phase
        yield return new WaitForSeconds(movementStartDelay);

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
        // Determine if this is an outer ring sphere and if separate magnitudes are enabled
        bool isOuter = false;
        if (outerSpheres != null)
        {
            foreach (var s in outerSpheres)
            {
                if (s == sphere)
                {
                    isOuter = true;
                    break;
                }
            }
        }
        float useHueChange = (separateOuterRingChangeMagnitudes && isOuter) ? outerRingHueChange : hueChangeHSV;
        float useValueChange = (separateOuterRingChangeMagnitudes && isOuter) ? outerRingValueChange : valueChange;
        float useSizeChange = (separateOuterRingChangeMagnitudes && isOuter) ? outerRingSizeChange : sizeChange;
        float useOrientationChange = (separateOuterRingChangeMagnitudes && isOuter) ? outerRingOrientationChange : orientationChange;
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

                // Check if the trial is a value change trial
                else if (changeValue)
                {
                    float newValue;
                    if (currentValue + useValueChange > 1f)
                    {
                        newValue = Mathf.Clamp01(currentValue - useValueChange);
                    }
                    else if (currentValue - useValueChange < 0f)
                    {
                        newValue = Mathf.Clamp01(currentValue + useValueChange);
                    }
                    else
                    {
                        newValue = addChange
                            ? Mathf.Clamp01(currentValue + useValueChange)
                            : Mathf.Clamp01(currentValue - useValueChange);
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
        // Hide all inner spheres
        if (innerSpheres != null)
        {
            foreach (GameObject sphere in innerSpheres)
            {
                if (sphere != null)
                    sphere.SetActive(false);
            }
        }
        // Hide all outer spheres
        if (outerSpheres != null)
        {
            foreach (GameObject outer in outerSpheres)
            {
                if (outer != null)
                    outer.SetActive(false);
            }
        }
        // Hide all middle spheres
        if (middleSpheres != null)
        {
            foreach (GameObject middle in middleSpheres)
            {
                if (middle != null)
                    middle.SetActive(false);
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
        // Reactivate spheres when hiding the black screen
        foreach (GameObject sphere in innerSpheres)
        {
            if (sphere != null)
                sphere.SetActive(true);
        }
        // Reactivate outer ring spheres as well
        if (outerSpheres != null)
        {
            foreach (GameObject outer in outerSpheres)
            {
                if (outer != null)
                    outer.SetActive(true);
            }
        }
        // Reactivate middle ring spheres as well
        if (middleSpheres != null)
        {
            foreach (GameObject middle in middleSpheres)
            {
                if (middle != null)
                    middle.SetActive(true);
            }
        }

        blackScreen.SetActive(false);
        blackScreenUp = false;
    }


    // Create headers for the results
    private void CreateHeaders()
    {
        int totalSpheres = ringOneNumSpheres + (numberOfRings == 3 ? ringTwoNumSpheres : 0) + (numberOfRings >= 2 ? ringThreeNumSpheres : 0);
        headers = new string[31 + totalSpheres]; // Reduced by 1 for removed Default Hue column (32 - 1 = 31)
        headers[0] = "Participant";
        headers[1] = "Trial Number";
        headers[2] = "Change Type";
        headers[3] = "Movement Type";
        headers[4] = "Attendant Ring";
        headers[5] = "Ring Configuration";
        headers[6] = "Ring 1 State";
        headers[7] = "Ring 2 State";
        headers[8] = "Ring 3 State";
        headers[9] = "Changed Sphere";
        headers[10] = "Before Change";
        headers[11] = "After Change";
        headers[12] = "Selected Sphere";
        headers[13] = "Success";
        headers[14] = "Response Time (After change) (s)";
        // Removed "Response Time (After motion stops)" column
        
        // General Settings columns
        headers[15] = "Trial Length (s)";
        headers[16] = "Movement Start Delay (s)";
        headers[17] = "Blink Spheres";
        headers[18] = "Blink Duration (s)";
        headers[19] = "Rotation Speed (°/s)";
        headers[20] = "# of Spheres - Ring 1";
        headers[21] = "# of Spheres - Ring 2";
        headers[22] = "# of Spheres - Ring 3";
        headers[23] = "Radius - Ring 1";
        headers[24] = "Radius - Ring 2";
        headers[25] = "Radius - Ring 3";
        headers[26] = "Sphere Saturation";
        headers[27] = "Sphere Value";
        headers[28] = "Sphere Size";
        headers[29] = "Inactive Ring Transparency";
        headers[30] = "Sound Interval (s)";
        
        int idx = 31; // Updated index start for sphere colors
        for (int i = 0; i < ringOneNumSpheres; i++)
        {
            headers[idx++] = $"Ring 1 - Sphere {i+1}";
        }
        if (numberOfRings == 3)
        {
            for (int i = 0; i < ringTwoNumSpheres; i++)
            {
                headers[idx++] = $"Ring 2 - Sphere {i+1}";
            }
        }
        if (numberOfRings >= 2)
        {
            for (int i = 0; i < ringThreeNumSpheres; i++)
            {
                headers[idx++] = $"Ring 3 - Sphere {i+1}";
            }
        }
    }

    // Save data after a trial
    public void SaveTrialResults()
    {
        // Save what the sphere change, picked sphere, and result of trial were
        trialResults[10] = originalResult; // Before Change
        trialResults[11] = changedResult;  // After Change
        trialResults[12] = selectedSphere;
        trialResults[13] = success;
        trialResults[14] = changeTime > 0f ? (sphereClickTime - changeTime).ToString("F3") : "";
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

        // Ensure clicks are only processed when allowed 
        if (!canClick) 
        { 
            Debug.LogWarning("Click ignored. Interaction is disabled (canClick is false)."); 
            return; 
        } 

        // Log the clicked sphere 
        Debug.Log($"Sphere {clickedSphere.name} clicked!"); 

        // Determine if inner, middle, or outer, and index
        bool isMiddle = false;
        bool isOuter = false;
        int index = -1;
        
        // Check middle ring first (Ring 2 - the selectable ring)
        if (middleSpheres != null)
        {
            for (int i = 0; i < middleSpheres.Length; i++)
            {
                if (middleSpheres[i] == clickedSphere)
                {
                    isMiddle = true;
                    index = i;
                    break;
                }
            }
        }
        
        // If not found in middle, check inner ring
        if (index == -1)
        {
            for (int i = 0; i < innerSpheres.Length; i++)
            {
                if (innerSpheres[i] == clickedSphere)
                {
                    isOuter = false;
                    index = i;
                    break;
                }
            }
        }
        
        // If not found in inner, check outer ring
        if (index == -1 && outerSpheres != null)
        {
            for (int i = 0; i < outerSpheres.Length; i++)
            {
                if (outerSpheres[i] == clickedSphere)
                {
                    isOuter = true;
                    index = i;
                    break;
                }
            }
        }
        
        // Set selectedSphere based on which ring the clicked sphere belongs to
        if (isMiddle)
            selectedSphere = (index + 1).ToString();
        else
            selectedSphere = isOuter ? $"outer_{index+1}" : $"inner_{index+1}";
        SphereManager sphereManager = clickedSphere.GetComponent<SphereManager>(); 
        success = (sphereManager != null && sphereManager.isChanged) ? "true" : "false"; // Check if the clicked sphere was the changed one 

        // Save trial results 
        SaveTrialResults(); 

        // Prevent further clicks until the next trial 
        canClick = false; 
        trialActive = false; // Stop all motion immediately
        Debug.Log("Interaction disabled (canClick set to false). Preparing for the next trial..."); 

        // Show the black screen and wait for user input to start the next trial 
        ShowBlackScreen("Press A on the VR controller to Continue to the Next Trial"); 
    } 

    // End of sphere creation functions

    // Simplified sphere creation - purely random attributes within acceptable ranges

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
        if (audioSource == null || lowSound == null || highSound == null)
        {
            Debug.LogError($"[AudioCue] AudioSource or clips not assigned. audioSource: {(audioSource == null ? "NULL" : "OK")}, lowSound: {(lowSound == null ? "NULL" : "OK")}, highSound: {(highSound == null ? "NULL" : "OK")}");
            yield break;
        }
        // Play countdown beeps
        for (int i = 0; i < beepCount; i++)
        {
            audioSource.spatialBlend = 0f;
            audioSource.mute = false;
            audioSource.volume = 1f;
            audioSource.PlayOneShot(lowSound);
            Debug.Log($"[AudioCue] Countdown beep {i+1}");
            yield return new WaitForSeconds(interval);
        }
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
