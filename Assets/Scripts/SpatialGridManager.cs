using UnityEngine;

public enum GuidePathMode
{
    Always,
    TrainingBlockOnly,
    Never
}

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
    [HideInInspector] public float gridExtentX = 15.0f;
    
    [Tooltip("Grid size along Z-axis (how far the grid extends forward/back from center)")]
    [HideInInspector] public float gridExtentZ = 25.0f;
    
    [Tooltip("Height of the walls in meters")]
    [HideInInspector] public float wallHeight = 4.0f;
    
    [Header("Wireframe Grid Appearance")]
    [Tooltip("Background color for the grid surfaces")]
    [HideInInspector] public Color backgroundColor = new Color(0.192f, 0.192f, 0.192f, 1.0f); // Unity's default fog color
    
    [Tooltip("Enable background fill (uncheck for transparent wireframe only)")]
    [HideInInspector] public bool enableBackground = true;
    
    [Tooltip("Grid line color")]
    [HideInInspector] public Color lineColor = new Color(0.13f, 0.13f, 0.13f, 1.0f); // Darker grey for lines
    
    [Tooltip("Width of grid lines in texture pixels (higher = thicker lines)")]
    [Range(1, 32)]
    public int lineWidth = 8;
    
    [Header("Grid Components")]
    [Tooltip("Enable floor grid")]
    [HideInInspector] public bool enableFloorGrid = true;
    
    [Tooltip("Enable ceiling grid")]
    [HideInInspector] public bool enableCeilingGrid = true;
    
    [Tooltip("Enable wall grids")]
    [HideInInspector] public bool enableWallGrids = true;
    
    [Header("Guide Path")]
    [Tooltip("When to show the guide path")]
    public GuidePathMode showGuidePath = GuidePathMode.TrainingBlockOnly;

    [Tooltip("Color for highlighted path squares during training")]
    [HideInInspector] public Color guidePathColor = Color.red;
    private Material highlightMaterial;
    private System.Collections.Generic.List<Vector3> originalPositions = new System.Collections.Generic.List<Vector3>();

    [Header("Trial Behavior")]
    [Tooltip("Grid opacity during active trials (0.0 = invisible, 1.0 = full visibility)")]
    [Range(0.0f, 1.0f)]
    [HideInInspector] public float trialOpacity = 0.3f;
    
    [Tooltip("Hide grid completely during trials")]
    [HideInInspector] public bool hideGridDuringTrials = false;
    
    [Tooltip("Show grid between trials (black screens, break screens)")]
    [HideInInspector] public bool showGridBetweenTrials = false;
    
    [Header("Grid Position")]
    [Tooltip("Y position of the floor grid (should be at ground level for VR user)")]
    public float floorY = -1.5f;
    
    [Tooltip("Y position of the ceiling grid")]
    [HideInInspector] public float ceilingY = 3.0f;
    
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
    /// Create a wireframe material with red lines for highlighting
    /// </summary>
    Material CreateHighlightWireframeMaterial()
    {
        // Create red wireframe texture
        Texture2D redGridTexture = CreateRedWireframeTexture();
        
        // Create the highlight wireframe material using the same logic as regular material
        Material highlightMat;
        if (enableBackground)
        {
            // Opaque material with background
            highlightMat = new Material(Shader.Find("Standard"));
            highlightMat.mainTexture = redGridTexture;
            highlightMat.color = Color.white; // Texture handles coloring
        }
        else
        {
            // Transparent material (wireframe only)
            highlightMat = new Material(Shader.Find("Standard"));
            highlightMat.mainTexture = redGridTexture;
            highlightMat.color = Color.white;
            
            // Set up transparency
            highlightMat.SetFloat("_Mode", 3); // Transparent mode
            highlightMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            highlightMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            highlightMat.SetInt("_ZWrite", 0);
            highlightMat.DisableKeyword("_ALPHATEST_ON");
            highlightMat.EnableKeyword("_ALPHABLEND_ON");
            highlightMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            highlightMat.renderQueue = 3000;
        }
        
        highlightMat.SetFloat("_Metallic", 0f);
        highlightMat.SetFloat("_Glossiness", 0.1f);
        highlightMat.name = "RedWireframeGrid_Material";
        
        return highlightMat;
    }

    /// <summary>
    /// Create wireframe texture with red lines instead of normal line color
    /// </summary>
    Texture2D CreateRedWireframeTexture()
    {
        int textureSize = 256;
        Texture2D texture = new Texture2D(textureSize, textureSize);
        Color[] pixels = new Color[textureSize * textureSize];
        
        // Fill with background color or transparent (same as regular grid)
        Color bgColor = enableBackground ? backgroundColor : new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0f);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = bgColor;
        }
        
        // Draw ALL FOUR EDGES in RED for complete wireframe square
        Color redLineColor = guidePathColor;
        
        // Top horizontal line
        for (int y = 0; y < lineWidth && y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                pixels[y * textureSize + x] = redLineColor;
            }
        }
        
        // Bottom horizontal line
        for (int y = textureSize - lineWidth; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                pixels[y * textureSize + x] = redLineColor;
            }
        }
        
        // Left vertical line
        for (int x = 0; x < lineWidth && x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                pixels[y * textureSize + x] = redLineColor;
            }
        }
        
        // Right vertical line
        for (int x = textureSize - lineWidth; x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                pixels[y * textureSize + x] = redLineColor;
            }
        }
        
        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;
        texture.Apply();
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

    /// <summary>
    /// Show guide path by changing the color of existing grid squares
    /// </summary>
    public void ShowGuidePath(bool isTrainingTrial = false)
    {
        if (showGuidePath == GuidePathMode.Never) return;
        if (showGuidePath == GuidePathMode.TrainingBlockOnly && !isTrainingTrial) return;

        // Always hide any existing guide path first to ensure fresh positioning
        HideGuidePath();
        
        // Find XR Rig position and calculate which grid squares to highlight
        Vector3 userPosition = GetXRRigPosition();
        Debug.Log($"[GuidePath] === POSITION DETECTION at {System.DateTime.Now:HH:mm:ss.fff} ===");
        Debug.Log($"[GuidePath] Raw XR Rig position: {userPosition}");
        
        // Also get camera position for comparison
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"[GuidePath] Main Camera position: {mainCam.transform.position}");
            Debug.Log($"[GuidePath] Using camera position instead for more accuracy");
            userPosition = new Vector3(mainCam.transform.position.x, 0, mainCam.transform.position.z);
        }
        
        Debug.Log($"[GuidePath] Final user position for guide path: {userPosition}");
        
        // Calculate which grid square the user is in using the same logic as CreateFloorGrid
        float startPosX = -gridExtentX;
        float startPosZ = -gridExtentZ;
        
        // Find the grid indices that contain the user
        int userGridX = Mathf.FloorToInt((userPosition.x - startPosX) / gridSquareSize);
        int userGridZ = Mathf.FloorToInt((userPosition.z - startPosZ) / gridSquareSize);
        
        Debug.Log($"[GuidePath] Calculated grid indices: ({userGridX}, {userGridZ}) from position ({userPosition.x}, {userPosition.z})");
        
        // Define which grid squares to highlight (zigzag path forward from user, shifted left)
        Vector2Int[] pathSquares = new Vector2Int[5]
        {
            new Vector2Int(userGridX, userGridZ),         // User position at center (bottom square)
            new Vector2Int(userGridX - 1, userGridZ + 1), // Forward-left (X O pattern row 1)
            new Vector2Int(userGridX, userGridZ + 2),     // Forward-center (O X pattern row 2) 
            new Vector2Int(userGridX - 1, userGridZ + 3), // Forward-left (X O pattern row 3)
            new Vector2Int(userGridX, userGridZ + 4)      // Forward-center (O X pattern row 4)
        };

        
        // Create highlight material if not exists
        if (highlightMaterial == null)
        {
            highlightMaterial = CreateHighlightWireframeMaterial();
        }
        
        // Debug: Check how many floor squares exist and grid bounds
        int gridCountX = Mathf.RoundToInt(gridExtentX * 2.0f / gridSquareSize);
        int gridCountZ = Mathf.RoundToInt(gridExtentZ * 2.0f / gridSquareSize);
        Debug.Log($"[GuidePath] Floor parent has {floorParent.transform.childCount} children");
        Debug.Log($"[GuidePath] Grid bounds: X=0 to {gridCountX-1}, Z=0 to {gridCountZ-1}");
        

        
        // Then change materials for path squares only
        for (int i = 0; i < pathSquares.Length; i++)
        {
            Vector2Int square = pathSquares[i];
            string squareName = $"Floor_Square_{square.x}_{square.y}";
            
            // Check if indices are within valid grid bounds
            bool withinBounds = (square.x >= 0 && square.x < gridCountX && square.y >= 0 && square.y < gridCountZ);
            Debug.Log($"[GuidePath] Path Square {i}: indices ({square.x}, {square.y}) - Within bounds: {withinBounds}");
            Debug.Log($"[GuidePath] Looking for path square: {squareName}");
            
            // Find the existing grid square GameObject
            Transform squareTransform = floorParent.transform.Find(squareName);
            if (squareTransform != null)
            {
                Debug.Log($"[GuidePath] ✓ FOUND path square: {squareName}");
                
                // Change material and raise position for path squares
                Renderer renderer = squareTransform.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Debug.Log($"[GuidePath] ✓ CHANGING material for {squareName} to RED highlight");
                    renderer.material = highlightMaterial;
                    
                    // Store original position and raise the square slightly
                    Vector3 currentPos = squareTransform.position;
                    originalPositions.Add(currentPos);
                    squareTransform.position = new Vector3(currentPos.x, currentPos.y + 0.002f, currentPos.z);
                    
                    // Calculate distance from user to this square
                    float distance = Vector3.Distance(new Vector3(userPosition.x, 0, userPosition.z), new Vector3(currentPos.x, 0, currentPos.z));
                    Debug.Log($"[GuidePath] Square {squareName} is at world position {currentPos} - Distance from user: {distance:F2} units");
                    
                    pathHighlights.Add(squareTransform.gameObject);
                }
                else
                {
                    Debug.LogWarning($"[GuidePath] No Renderer component found on {squareName}");
                }
            }
            else
            {
                Debug.LogWarning($"[GuidePath] Could not find grid square: {squareName}");
                
                // Debug: List first few children to see naming pattern
                if (i == 0)
                {
                    for (int c = 0; c < Mathf.Min(5, floorParent.transform.childCount); c++)
                    {
                        Debug.Log($"[GuidePath] Child {c}: {floorParent.transform.GetChild(c).name}");
                    }
                }
            }
        }
    }    // Store highlighted path positions
    private Vector3[] highlightedPathPositions = null;

    /// <summary>
    /// Hide training path
    /// </summary>
    public void HideGuidePath()
    {
        // Restore materials and positions for path squares
        for (int i = 0; i < pathHighlights.Count; i++)
        {
            GameObject highlight = pathHighlights[i];
            if (highlight != null)
            {
                // Restore material
                Renderer renderer = highlight.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = baseMaterial; // Restore original grid material
                }
                
                // Restore original position
                if (i < originalPositions.Count && originalPositions[i] != Vector3.zero)
                {
                    highlight.transform.position = originalPositions[i];
                }
            }
        }
        pathHighlights.Clear();
        originalPositions.Clear();
    }

    /// <summary>
    /// Get XR Rig position to find user's actual location
    /// </summary>
    private Vector3 GetXRRigPosition()
    {
        // Try to find XR Origin or XR Rig in the scene
        GameObject xrRig = GameObject.Find("XR Origin") ?? GameObject.Find("XR Rig") ?? GameObject.Find("XRRig");
        
        if (xrRig != null)
        {
            Debug.Log($"[GuidePath] Found XR Rig: {xrRig.name} at position {xrRig.transform.position}");
            Debug.Log($"[GuidePath] XR Rig parent: {(xrRig.transform.parent != null ? xrRig.transform.parent.name : "null")}");
            return xrRig.transform.position;
        }
        
        // Fallback: look for main camera (should be part of XR system)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // Get the XR Origin by going up the hierarchy from the camera
            Transform current = mainCam.transform;
            while (current.parent != null)
            {
                current = current.parent;
                if (current.name.Contains("XR") && (current.name.Contains("Origin") || current.name.Contains("Rig")))
                {
                    return current.position;
                }
            }
            // If no XR parent found, use camera's position but at ground level
            return new Vector3(mainCam.transform.position.x, 0, mainCam.transform.position.z);
        }
        
        // Final fallback: origin
        return Vector3.zero;
    }

    // Store training path highlight objects
    private System.Collections.Generic.List<GameObject> pathHighlights = new System.Collections.Generic.List<GameObject>();

    /// <summary>
    /// Create grid with highlighted path integrated into wireframe
    /// </summary>
    private void CreateGridWithHighlightedPath(Vector3[] pathPositions)
    {
        highlightedPathPositions = pathPositions;
        
        // Create individual highlighted grid squares that match the wireframe pattern
        foreach (Vector3 pathPos in pathPositions)
        {
            CreateHighlightedGridSquare(pathPos);
        }
    }

    /// <summary>
    /// Create a single highlighted grid square using wireframe style
    /// </summary>
    private void CreateHighlightedGridSquare(Vector3 position)
    {
        // Position is already snapped in ShowGuidePath
        float snappedX = position.x;
        float snappedZ = position.z;
        
        // For training path, place squares at ground level (Y=0) so they're visible to the user
        float trainingSquareY = 0.002f; // Just above ground level for VR visibility
        
        Debug.Log($"[GuidePath] Creating square at position: ({snappedX}, {trainingSquareY}, {snappedZ})");
        
        // Create wireframe square using line renderers for each edge
        GameObject highlightSquare = new GameObject("PathHighlightSquare");
        highlightSquare.transform.position = new Vector3(snappedX, trainingSquareY, snappedZ);
        
        // Create 4 line renderers for the square edges
        Vector3[] corners = new Vector3[4]
        {
            new Vector3(-gridSquareSize/2, 0, -gridSquareSize/2), // Bottom-left
            new Vector3(gridSquareSize/2, 0, -gridSquareSize/2),  // Bottom-right  
            new Vector3(gridSquareSize/2, 0, gridSquareSize/2),   // Top-right
            new Vector3(-gridSquareSize/2, 0, gridSquareSize/2)   // Top-left
        };
        
        for (int i = 0; i < 4; i++)
        {
            GameObject line = new GameObject($"Edge_{i}");
            line.transform.SetParent(highlightSquare.transform);
            
            LineRenderer lr = line.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Unlit/Color"));
            lr.material.color = guidePathColor;
            
            // Use the same line width as the grid itself
            float gridLineWidth = lineWidth * 0.001f; // Convert to world units (adjust scale as needed)
            lr.startWidth = gridLineWidth;
            lr.endWidth = gridLineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = false; // Use local space relative to parent
            
            // Set line positions (current corner to next corner)
            lr.SetPosition(0, corners[i]);
            lr.SetPosition(1, corners[(i + 1) % 4]);
        }
        
        // Parent to grid for organization  
        if (gridParent != null)
        {
            highlightSquare.transform.SetParent(gridParent.transform);
        }
        
        pathHighlights.Add(highlightSquare);
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
