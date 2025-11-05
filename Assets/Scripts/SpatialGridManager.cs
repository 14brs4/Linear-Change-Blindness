using UnityEngine;

/// <summary>
/// Spatial Grid Manager for VR Environment
/// 
/// Creates a grey checkerboard pattern on floor, ceiling, and walls to provide spatial reference.
/// Each square is 1 square meter to help users understand scale and position in the virtual space.
/// Uses Unity's default void grey color with a slightly darker grey for the checkerboard pattern.
/// </summary>
public class SpatialGridManager : MonoBehaviour
{
    [Header("Grid Configuration")]
    [Tooltip("Size of each grid square in meters (recommended: 1.0 for 1 square meter)")]
    public float gridSquareSize = 1.0f;
    
    [Tooltip("Grid size along X-axis (how far the grid extends left/right from center)")]
    public float gridExtentX = 15.0f;
    
    [Tooltip("Grid size along Z-axis (how far the grid extends forward/back from center)")]
    public float gridExtentZ = 25.0f;
    
    [Tooltip("Height of the walls in meters")]
    public float wallHeight = 4.0f;
    
    [Header("Grid Colors")]
    [Tooltip("First checkerboard color - customize as needed")]
    public Color checkerColor1 = new Color(0.192f, 0.192f, 0.192f, 1.0f); // Unity's default fog color
    
    [Tooltip("Second checkerboard color - customize as needed")]
    public Color checkerColor2 = new Color(0.13f, 0.13f, 0.13f, 1.0f); // Slightly darker
    
    [Header("Grid Components")]
    [Tooltip("Enable floor grid")]
    public bool enableFloorGrid = true;
    
    [Tooltip("Enable ceiling grid")]
    public bool enableCeilingGrid = true;
    
    [Tooltip("Enable wall grids")]
    public bool enableWallGrids = true;
    
    [Header("Trial Behavior")]
    [Tooltip("Grid opacity during active trials (0.0 = invisible, 1.0 = full visibility)")]
    [Range(0.0f, 1.0f)]
    public float trialOpacity = 0.3f;
    
    [Tooltip("Hide grid completely during trials")]
    public bool hideGridDuringTrials = false;
    
    [Tooltip("Show grid between trials (black screens, break screens)")]
    public bool showGridBetweenTrials = false;
    
    [Header("Grid Position")]
    [Tooltip("Y position of the floor grid (should be at ground level for VR user)")]
    public float floorY = -1.5f;
    
    [Tooltip("Y position of the ceiling grid")]
    public float ceilingY = 3.0f;
    
    // Materials
    private Material baseMaterial;
    private Material darkMaterial;
    
    // Parent objects for organization
    private GameObject gridParent;
    private GameObject floorParent;
    private GameObject ceilingParent;
    private GameObject wallsParent;

    void Start()
    {
        CreateMaterials();
        CreateGridSystem();
        
        // Initialize grid state (start between trials)
        SetTrialState(false);
    }

    /// <summary>
    /// Create materials for the grid pattern
    /// </summary>
    void CreateMaterials()
    {
        // Create material for first checkerboard color
        baseMaterial = new Material(Shader.Find("Standard"));
        baseMaterial.color = checkerColor1;
        baseMaterial.SetFloat("_Metallic", 0f);
        baseMaterial.SetFloat("_Glossiness", 0.1f); // Slight roughness for better depth perception
        baseMaterial.name = "GridChecker1_Material";

        // Create material for second checkerboard color
        darkMaterial = new Material(Shader.Find("Standard"));
        darkMaterial.color = checkerColor2;
        darkMaterial.SetFloat("_Metallic", 0f);
        darkMaterial.SetFloat("_Glossiness", 0.1f);
        darkMaterial.name = "GridChecker2_Material";
    }

    /// <summary>
    /// Create the complete grid system
    /// </summary>
    void CreateGridSystem()
    {
        // Create parent objects for organization
        gridParent = new GameObject("Spatial Grid System");
        gridParent.transform.SetParent(transform);
        
        if (enableFloorGrid)
        {
            floorParent = new GameObject("Floor Grid");
            floorParent.transform.SetParent(gridParent.transform);
            CreateFloorGrid();
        }
        
        if (enableCeilingGrid)
        {
            ceilingParent = new GameObject("Ceiling Grid");
            ceilingParent.transform.SetParent(gridParent.transform);
            CreateCeilingGrid();
        }
        
        if (enableWallGrids)
        {
            wallsParent = new GameObject("Wall Grids");
            wallsParent.transform.SetParent(gridParent.transform);
            CreateWallGrids();
        }
    }

    /// <summary>
    /// Create the floor grid with checkerboard pattern
    /// </summary>
    void CreateFloorGrid()
    {
        int gridCountX = Mathf.RoundToInt(gridExtentX * 2.0f / gridSquareSize);
        int gridCountZ = Mathf.RoundToInt(gridExtentZ * 2.0f / gridSquareSize);
        float startPosX = -gridExtentX;
        float startPosZ = -gridExtentZ;
        
        for (int x = 0; x < gridCountX; x++)
        {
            for (int z = 0; z < gridCountZ; z++)
            {
                // Determine checkerboard pattern
                bool useColor2 = (x + z) % 2 == 1;
                Material material = useColor2 ? darkMaterial : baseMaterial;
                
                // Create the grid square
                GameObject square = CreateGridSquare($"Floor_Square_{x}_{z}", material);
                square.transform.SetParent(floorParent.transform);
                
                // Position the square
                Vector3 position = new Vector3(
                    startPosX + (x * gridSquareSize) + (gridSquareSize * 0.5f),
                    floorY,
                    startPosZ + (z * gridSquareSize) + (gridSquareSize * 0.5f)
                );
                square.transform.position = position;
                
                // Rotate to lie flat on floor
                square.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
        }
    }

    /// <summary>
    /// Create the ceiling grid with checkerboard pattern
    /// </summary>
    void CreateCeilingGrid()
    {
        int gridCountX = Mathf.RoundToInt(gridExtentX * 2.0f / gridSquareSize);
        int gridCountZ = Mathf.RoundToInt(gridExtentZ * 2.0f / gridSquareSize);
        float startPosX = -gridExtentX;
        float startPosZ = -gridExtentZ;
        
        for (int x = 0; x < gridCountX; x++)
        {
            for (int z = 0; z < gridCountZ; z++)
            {
                // Determine checkerboard pattern
                bool useColor2 = (x + z) % 2 == 1;
                Material material = useColor2 ? darkMaterial : baseMaterial;
                
                // Create the grid square
                GameObject square = CreateGridSquare($"Ceiling_Square_{x}_{z}", material);
                square.transform.SetParent(ceilingParent.transform);
                
                // Position the square
                Vector3 position = new Vector3(
                    startPosX + (x * gridSquareSize) + (gridSquareSize * 0.5f),
                    ceilingY,
                    startPosZ + (z * gridSquareSize) + (gridSquareSize * 0.5f)
                );
                square.transform.position = position;
                
                // Rotate to face down from ceiling
                square.transform.rotation = Quaternion.Euler(180, 0, 0);
            }
        }
    }

    /// <summary>
    /// Create wall grids on all four sides
    /// </summary>
    void CreateWallGrids()
    {
        CreateWallGrid("North", Vector3.forward, Vector3.right, new Vector3(0, 0, gridExtentZ), gridExtentX * 2.0f);
        CreateWallGrid("South", Vector3.back, Vector3.left, new Vector3(0, 0, -gridExtentZ), gridExtentX * 2.0f);
        CreateWallGrid("East", Vector3.right, Vector3.back, new Vector3(gridExtentX, 0, 0), gridExtentZ * 2.0f);
        CreateWallGrid("West", Vector3.left, Vector3.forward, new Vector3(-gridExtentX, 0, 0), gridExtentZ * 2.0f);
    }

    /// <summary>
    /// Create a single wall grid
    /// </summary>
    void CreateWallGrid(string wallName, Vector3 forward, Vector3 right, Vector3 basePosition, float wallWidth)
    {
        GameObject wallParent = new GameObject($"{wallName} Wall");
        wallParent.transform.SetParent(wallsParent.transform);
        
        int horizontalCount = Mathf.RoundToInt(wallWidth / gridSquareSize);
        int verticalCount = Mathf.RoundToInt(wallHeight / gridSquareSize);
        
        for (int h = 0; h < horizontalCount; h++)
        {
            for (int v = 0; v < verticalCount; v++)
            {
                // Determine checkerboard pattern
                bool useColor2 = (h + v) % 2 == 1;
                Material material = useColor2 ? darkMaterial : baseMaterial;
                
                // Create the grid square
                GameObject square = CreateGridSquare($"{wallName}_Square_{h}_{v}", material);
                square.transform.SetParent(wallParent.transform);
                
                // Calculate position
                Vector3 horizontalOffset = right * (-(wallWidth * 0.5f) + (h * gridSquareSize) + (gridSquareSize * 0.5f));
                Vector3 verticalOffset = Vector3.up * (floorY + (v * gridSquareSize) + (gridSquareSize * 0.5f));
                Vector3 position = basePosition + horizontalOffset + verticalOffset;
                
                square.transform.position = position;
                
                // Rotate to face inward
                square.transform.LookAt(square.transform.position - forward, Vector3.up);
            }
        }
    }

    /// <summary>
    /// Create a single grid square GameObject
    /// </summary>
    GameObject CreateGridSquare(string name, Material material)
    {
        // Create the GameObject
        GameObject square = GameObject.CreatePrimitive(PrimitiveType.Plane);
        square.name = name;
        
        // Scale to correct size (Unity plane is 10x10 units by default)
        Vector3 scale = Vector3.one * (gridSquareSize / 10.0f);
        square.transform.localScale = scale;
        
        // Apply material
        Renderer renderer = square.GetComponent<Renderer>();
        renderer.material = material;
        
        // Remove collider to avoid interference with experiment
        Collider collider = square.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }
        
        // Add to a specific layer if needed (optional)
        square.layer = LayerMask.NameToLayer("Default");
        
        return square;
    }

    /// <summary>
    /// Regenerate the grid system (useful for runtime changes)
    /// </summary>
    public void RegenerateGrid()
    {
        // Destroy existing grid
        if (gridParent != null)
        {
            DestroyImmediate(gridParent);
        }
        
        // Recreate materials and grid
        CreateMaterials();
        CreateGridSystem();
    }

    /// <summary>
    /// Toggle grid visibility
    /// </summary>
    public void SetGridVisibility(bool visible)
    {
        if (gridParent != null)
        {
            gridParent.SetActive(visible);
        }
    }

    /// <summary>
    /// Toggle individual grid components
    /// </summary>
    public void SetFloorVisibility(bool visible)
    {
        if (floorParent != null)
        {
            floorParent.SetActive(visible);
        }
    }

    public void SetCeilingVisibility(bool visible)
    {
        if (ceilingParent != null)
        {
            ceilingParent.SetActive(visible);
        }
    }

    public void SetWallsVisibility(bool visible)
    {
        if (wallsParent != null)
        {
            wallsParent.SetActive(visible);
        }
    }

    /// <summary>
    /// Fade the grid opacity (useful during trials to reduce distraction)
    /// </summary>
    public void SetGridOpacity(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        
        if (baseMaterial != null)
        {
            Color color1 = checkerColor1;
            color1.a = alpha;
            baseMaterial.color = color1;
        }
        
        if (darkMaterial != null)
        {
            Color color2 = checkerColor2;
            color2.a = alpha;
            darkMaterial.color = color2;
        }
    }

    /// <summary>
    /// Reset grid opacity to full visibility while preserving current colors
    /// </summary>
    public void ResetGridOpacity()
    {
        if (baseMaterial != null)
        {
            Color color1 = checkerColor1;
            color1.a = 1.0f;
            baseMaterial.color = color1;
        }
        
        if (darkMaterial != null)
        {
            Color color2 = checkerColor2;
            color2.a = 1.0f;
            darkMaterial.color = color2;
        }
    }

    /// <summary>
    /// Set trial state to control grid visibility and behavior
    /// Called by GameManager to indicate trial state changes
    /// </summary>
    public void SetTrialState(bool duringTrial)
    {
        if (duringTrial)
        {
            // During active trial (when spheres are visible)
            if (hideGridDuringTrials)
            {
                SetGridVisibility(false);
            }
            else
            {
                SetGridVisibility(true);
                SetGridOpacity(trialOpacity);
            }
            Debug.Log($"[SPATIAL GRID] Trial active - Grid visible: {!hideGridDuringTrials}, Opacity: {trialOpacity}");
        }
        else
        {
            // Between trials (black screens, break screens, etc.)
            if (showGridBetweenTrials)
            {
                SetGridVisibility(true);
                ResetGridOpacity();
            }
            else
            {
                SetGridVisibility(false);
            }
            Debug.Log($"[SPATIAL GRID] Between trials - Grid visible: {showGridBetweenTrials}");
        }
    }



    /// <summary>
    /// Update grid colors at runtime
    /// </summary>
    public void UpdateGridColors(Color newColor1, Color newColor2)
    {
        checkerColor1 = newColor1;
        checkerColor2 = newColor2;
        
        if (baseMaterial != null)
        {
            baseMaterial.color = checkerColor1;
        }
        
        if (darkMaterial != null)
        {
            darkMaterial.color = checkerColor2;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor helper to regenerate grid when values change
    /// </summary>
    void OnValidate()
    {
        // Only regenerate in play mode to avoid editor issues
        if (Application.isPlaying && gridParent != null)
        {
            RegenerateGrid();
        }
    }
#endif
}