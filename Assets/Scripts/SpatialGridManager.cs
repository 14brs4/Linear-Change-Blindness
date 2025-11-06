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
    
    [Header("Wireframe Grid Appearance")]
    [Tooltip("Background color for the grid surfaces")]
    public Color backgroundColor = new Color(0.192f, 0.192f, 0.192f, 1.0f); // Unity's default fog color
    
    [Tooltip("Enable background fill (uncheck for transparent wireframe only)")]
    public bool enableBackground = true;
    
    [Tooltip("Grid line color")]
    public Color lineColor = new Color(0.13f, 0.13f, 0.13f, 1.0f); // Darker grey for lines
    
    [Tooltip("Width of grid lines in texture pixels (higher = thicker lines)")]
    [Range(1, 32)]
    public int lineWidth = 8;
    
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
    /// Create materials for the wireframe grid pattern
    /// </summary>
    void CreateMaterials()
    {
        // Create wireframe grid texture
        Texture2D gridTexture = CreateWireframeTexture();
        
        // Create the wireframe material
        if (enableBackground)
        {
            // Opaque material with background
            baseMaterial = new Material(Shader.Find("Standard"));
            baseMaterial.mainTexture = gridTexture;
            baseMaterial.color = Color.white; // Texture handles coloring
        }
        else
        {
            // Transparent material (wireframe only)
            baseMaterial = new Material(Shader.Find("Standard"));
            baseMaterial.mainTexture = gridTexture;
            baseMaterial.color = Color.white;
            
            // Set up transparency
            baseMaterial.SetFloat("_Mode", 3); // Transparent mode
            baseMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            baseMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            baseMaterial.SetInt("_ZWrite", 0);
            baseMaterial.DisableKeyword("_ALPHATEST_ON");
            baseMaterial.EnableKeyword("_ALPHABLEND_ON");
            baseMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            baseMaterial.renderQueue = 3000;
        }
        
        baseMaterial.SetFloat("_Metallic", 0f);
        baseMaterial.SetFloat("_Glossiness", 0.1f);
        baseMaterial.name = "WireframeGrid_Material";
        
        // Use the same material for both (no more checkerboard pattern)
        darkMaterial = baseMaterial;
        
        Debug.Log($"[SpatialGrid] Wireframe material created - Background: {(enableBackground ? "Enabled" : "Transparent")}, Line width: {lineWidth}");
    }

    /// <summary>
    /// Create wireframe grid texture
    /// </summary>
    Texture2D CreateWireframeTexture()
    {
        int textureSize = 256;
        Texture2D texture = new Texture2D(textureSize, textureSize);
        Color[] pixels = new Color[textureSize * textureSize];
        
        // Fill with background color or transparent
        Color bgColor = enableBackground ? backgroundColor : new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0f);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = bgColor;
        }
        
        // Draw grid lines
        // Top horizontal line
        for (int y = 0; y < lineWidth && y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                pixels[y * textureSize + x] = lineColor;
            }
        }
        
        // Left vertical line
        for (int x = 0; x < lineWidth && x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                pixels[y * textureSize + x] = lineColor;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point; // Sharp lines
        
        return texture;
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
                // All squares use the same wireframe material
                Material material = baseMaterial;
                
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
                // All squares use the same wireframe material
                Material material = baseMaterial;
                
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
                // All squares use the same wireframe material
                Material material = baseMaterial;
                
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
            Color materialColor = baseMaterial.color;
            materialColor.a = alpha;
            baseMaterial.color = materialColor;
        }
    }

    /// <summary>
    /// Reset grid opacity to full visibility while preserving current colors
    /// </summary>
    public void ResetGridOpacity()
    {
        if (baseMaterial != null)
        {
            Color materialColor = baseMaterial.color;
            materialColor.a = 1.0f;
            baseMaterial.color = materialColor;
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
    /// Update grid colors and regenerate wireframe texture
    /// </summary>
    public void UpdateGridColors(Color newBackgroundColor, Color newLineColor)
    {
        backgroundColor = newBackgroundColor;
        lineColor = newLineColor;
        
        // Regenerate texture with new colors
        if (baseMaterial != null)
        {
            Texture2D newTexture = CreateWireframeTexture();
            baseMaterial.mainTexture = newTexture;
            
            Debug.Log($"[SpatialGrid] Wireframe colors updated - Background: {backgroundColor}, Lines: {lineColor}");
        }
    }

    /// <summary>
    /// Update line width and regenerate texture
    /// </summary>
    public void UpdateLineWidth(int newLineWidth)
    {
        lineWidth = Mathf.Clamp(newLineWidth, 1, 32);
        
        // Regenerate texture with new line width
        if (baseMaterial != null)
        {
            Texture2D newTexture = CreateWireframeTexture();
            baseMaterial.mainTexture = newTexture;
            
            Debug.Log($"[SpatialGrid] Line width updated to {lineWidth}");
        }
    }

    /// <summary>
    /// Toggle background transparency
    /// </summary>
    public void SetBackgroundEnabled(bool enabled)
    {
        enableBackground = enabled;
        
        // Recreate materials to handle transparency change
        CreateMaterials();
        
        // Update all existing squares with new material
        if (Application.isPlaying)
        {
            RegenerateGrid();
        }
        
        Debug.Log($"[SpatialGrid] Background {(enabled ? "enabled" : "disabled (transparent)")}");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor helper to regenerate grid when values change
    /// </summary>
    void OnValidate()
    {
        // Clamp line width
        lineWidth = Mathf.Clamp(lineWidth, 1, 32);
        
        // Only regenerate in play mode to avoid editor issues
        if (Application.isPlaying && gridParent != null)
        {
            CreateMaterials(); // Recreate materials with new settings
            RegenerateGrid();  // Regenerate grid with new materials
        }
    }
#endif
}