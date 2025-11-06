using UnityEngine;
using UnityEditor;

/// <summary>
/// Unity Editor utility to easily set up the spatial grid system in a VR scene.
/// This creates the necessary GameObjects and configures the spatial grid with sensible defaults.
/// </summary>
public class SpatialGridSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Setup Spatial Grid System")]
    [Tooltip("Click to automatically set up the spatial grid system in your scene")]
    public bool setupSpatialGrid = false;
    
    [Header("Grid Configuration")]
    [Tooltip("Size of each grid square in meters")]
    public float gridSquareSize = 1.0f;
    
    [Tooltip("Grid extent along X-axis (left/right from center)")]
    public float gridExtentX = 15.0f;
    
    [Tooltip("Grid extent along Z-axis (forward/back from center)")]
    public float gridExtentZ = 25.0f;
    
    [Tooltip("Height of wall grids (in meters)")]
    public float wallHeight = 4.0f;
    
    [Tooltip("Y position of floor (should be at ground level)")]
    public float floorY = -1.5f;
    
    [Tooltip("Y position of ceiling")]
    public float ceilingY = 3.0f;

    void OnValidate()
    {
        if (setupSpatialGrid)
        {
            setupSpatialGrid = false;
            SetupGrid();
        }
    }

    void SetupGrid()
    {
        Debug.Log("[SPATIAL GRID SETUP] Setting up spatial grid system...");

        // Check if SpatialGridManager already exists
        SpatialGridManager existingManager = FindObjectOfType<SpatialGridManager>();
        if (existingManager != null)
        {
            Debug.LogWarning("[SPATIAL GRID SETUP] SpatialGridManager already exists in scene. Skipping setup.");
            return;
        }

        // Create the spatial grid manager GameObject
        GameObject gridManagerObject = new GameObject("Spatial Grid Manager");
        SpatialGridManager gridManager = gridManagerObject.AddComponent<SpatialGridManager>();

        // Configure the grid manager with our settings
        gridManager.gridSquareSize = this.gridSquareSize;
        gridManager.gridExtentX = this.gridExtentX;
        gridManager.gridExtentZ = this.gridExtentZ;
        gridManager.wallHeight = this.wallHeight;
        gridManager.floorY = this.floorY;
        gridManager.ceilingY = this.ceilingY;

        // Set Unity's default void colors for wireframe
        gridManager.backgroundColor = new Color(0.192f, 0.192f, 0.192f, 1.0f); // Unity's default fog color
        gridManager.lineColor = new Color(0.13f, 0.13f, 0.13f, 1.0f); // Darker lines
        gridManager.enableBackground = true; // Enable background by default
        gridManager.lineWidth = 8; // Medium line width

        Debug.Log("[SPATIAL GRID SETUP] Spatial grid system created successfully!");
        Debug.Log($"[SPATIAL GRID SETUP] Grid square size: {gridSquareSize}m");
        Debug.Log($"[SPATIAL GRID SETUP] Grid extent X: {gridExtentX}m, Z: {gridExtentZ}m");
        Debug.Log($"[SPATIAL GRID SETUP] Wall height: {wallHeight}m");
        Debug.Log($"[SPATIAL GRID SETUP] Floor Y: {floorY}m");

        // Try to find GameManager and connect the spatial grid manager
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            // Use reflection to set the spatialGridManager field since it's marked as HideInInspector
            var field = typeof(GameManager).GetField("spatialGridManager");
            if (field != null)
            {
                field.SetValue(gameManager, gridManager);
                Debug.Log("[SPATIAL GRID SETUP] Connected SpatialGridManager to GameManager");
            }
        }
        else
        {
            Debug.LogWarning("[SPATIAL GRID SETUP] GameManager not found. You'll need to manually connect the SpatialGridManager.");
        }

        // Select the created object in the hierarchy
        Selection.activeGameObject = gridManagerObject;
        
        Debug.Log("[SPATIAL GRID SETUP] Setup complete! The grid will be generated when you play the scene.");
    }

    // Method to regenerate grid at runtime for testing
    [ContextMenu("Regenerate Grid (Runtime Only)")]
    void RegenerateGridTest()
    {
        if (Application.isPlaying)
        {
            SpatialGridManager gridManager = FindObjectOfType<SpatialGridManager>();
            if (gridManager != null)
            {
                gridManager.RegenerateGrid();
                Debug.Log("[SPATIAL GRID SETUP] Grid regenerated!");
            }
            else
            {
                Debug.LogWarning("[SPATIAL GRID SETUP] No SpatialGridManager found in scene!");
            }
        }
        else
        {
            Debug.LogWarning("[SPATIAL GRID SETUP] Grid regeneration is only available during play mode!");
        }
    }
#endif
}