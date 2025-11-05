using UnityEngine;

/// <summary>
/// Simple component that explains where to find all spatial grid settings
/// </summary>
public class SpatialGridInfo : MonoBehaviour
{
    [Header("Grid Configuration Guide")]
    [TextArea(8, 15)]
    public string configurationInfo = 
@"SPATIAL GRID CONFIGURATION:

BASIC SETTINGS (GameManager):
• Enable Spatial Grid - Master on/off switch
• Grid Opacity During Trials - Fade during trials
• Hide Grid During Trials - Complete hide option
• Quick Checker Color 1 & 2 - Basic color settings
• Quick Floor Y - Basic floor position

FULL SETTINGS (SpatialGridManager component):
• Grid Square Size - Size of each square (meters)
• Grid Extent X - Left/right coverage 
• Grid Extent Z - Forward/back coverage
• Wall Height - Height of wall grids
• Floor Y / Ceiling Y - Vertical positions
• Checker Color 1 & 2 - Full color control
• Enable Floor/Ceiling/Walls - Individual toggles

TO SETUP:
1. Use SpatialGridSetup component for auto setup
2. Or manually add SpatialGridManager component
3. Adjust settings in both GameManager and SpatialGridManager
4. Grid generates automatically at play time";

    [Header("Quick Actions")]
    [Tooltip("Click to find and select the SpatialGridManager in scene")]
    public bool findSpatialGridManager = false;
    
    [Tooltip("Click to find and select the GameManager in scene")]  
    public bool findGameManager = false;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (findSpatialGridManager)
        {
            findSpatialGridManager = false;
            SpatialGridManager gridManager = FindObjectOfType<SpatialGridManager>();
            if (gridManager != null)
            {
                UnityEditor.Selection.activeGameObject = gridManager.gameObject;
                Debug.Log("[GRID INFO] Selected SpatialGridManager in hierarchy");
            }
            else
            {
                Debug.LogWarning("[GRID INFO] No SpatialGridManager found in scene. Use SpatialGridSetup to create one.");
            }
        }
        
        if (findGameManager)
        {
            findGameManager = false;
            GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                UnityEditor.Selection.activeGameObject = gameManager.gameObject;
                Debug.Log("[GRID INFO] Selected GameManager in hierarchy");
            }
            else
            {
                Debug.LogWarning("[GRID INFO] No GameManager found in scene.");
            }
        }
    }
#endif
}