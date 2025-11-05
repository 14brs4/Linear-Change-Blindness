# Spatial Grid System Setup Instructions

## Overview
The Spatial Grid System creates a grey checkerboard pattern on the floor, ceiling, and walls to provide spatial reference for VR users. Each square is 1 square meter, helping users understand their position and scale in the virtual environment.

## Setup Instructions

### Method 1: Automatic Setup (Recommended)
1. Add a new empty GameObject to your scene
2. Add the `SpatialGridSetup` component to it
3. Configure the grid settings in the inspector:
   - **Grid Square Size**: Size of each grid square in meters (default: 1.0m)
   - **Grid Extent**: How far the grid extends from center (default: 10.0m)
   - **Wall Height**: Height of wall grids (default: 3.0m)
   - **Floor Y**: Y position of floor (default: 0.0m)
   - **Ceiling Y**: Y position of ceiling (default: 3.0m)
4. Check the "Setup Spatial Grid" checkbox - the system will automatically create and configure everything

### Method 2: Manual Setup
1. Add a new empty GameObject to your scene called "Spatial Grid Manager"
2. Add the `SpatialGridManager` component to it
3. Configure the grid settings in the inspector
4. Find your `GameManager` in the scene
5. In the GameManager inspector, enable "Enable Spatial Grid"
6. Drag the Spatial Grid Manager GameObject to the "Spatial Grid Manager" field in GameManager

## Configuration Options

### Grid Settings
- **Grid Square Size**: Size of each checkerboard square (1.0 = 1 square meter)
- **Grid Extent**: How far the grid extends in all directions from center
- **Wall Height**: Height of the wall grids
- **Floor Y / Ceiling Y**: Vertical positions of floor and ceiling grids

### Grid Colors
- **Base Grey Color**: Lighter grey color (Unity's default void grey: RGB 49, 49, 49)
- **Dark Grey Color**: Darker grey for checkerboard pattern (RGB 33, 33, 33)

### Grid Components
- **Enable Floor Grid**: Show/hide floor checkerboard
- **Enable Ceiling Grid**: Show/hide ceiling checkerboard  
- **Enable Wall Grids**: Show/hide wall checkerboards

### GameManager Integration
- **Enable Spatial Grid**: Master toggle for the entire grid system
- **Grid Opacity During Trials**: Reduce grid visibility during trials to minimize distraction (0.0 = invisible, 1.0 = full)
- **Hide Grid During Trials**: Completely hide grid during trials (overrides opacity setting)

## Runtime Controls

The system provides several methods for runtime control:

### Visibility Control
```csharp
spatialGridManager.SetGridVisibility(true/false);     // Show/hide entire grid
spatialGridManager.SetFloorVisibility(true/false);   // Show/hide just floor
spatialGridManager.SetCeilingVisibility(true/false); // Show/hide just ceiling
spatialGridManager.SetWallsVisibility(true/false);   // Show/hide just walls
```

### Opacity Control
```csharp
spatialGridManager.SetGridOpacity(0.5f);    // 50% opacity
spatialGridManager.ResetGridOpacity();      // Back to full opacity
```

### Grid Management
```csharp
spatialGridManager.RegenerateGrid();        // Recreate grid with new settings
```

## Integration with Experiment

The grid automatically adjusts its visibility during trials:
- **Between Trials**: Grid shows at full opacity for spatial reference
- **During Trials**: Grid opacity is reduced or hidden to minimize distraction
- **Break Screens**: Grid returns to full visibility

## Technical Details

### Performance
- Grid uses Unity's built-in plane primitives for efficiency
- Materials are shared across all grid squares to minimize draw calls
- Colliders are automatically removed to avoid interference with experiment mechanics

### Materials
- Creates two materials at runtime: base grey and dark grey
- Uses Unity's Standard shader with low metallic and slight roughness for better depth perception
- Colors match Unity's default void/fog colors for seamless integration

### Scene Organization
The system creates a hierarchical structure:
```
Spatial Grid System
├── Floor Grid
│   ├── Floor_Square_0_0
│   ├── Floor_Square_0_1
│   └── ...
├── Ceiling Grid
│   ├── Ceiling_Square_0_0
│   └── ...
└── Wall Grids
    ├── North Wall
    ├── South Wall
    ├── East Wall
    └── West Wall
```

## Troubleshooting

### Grid Not Appearing
1. Check that "Enable Spatial Grid" is checked in GameManager
2. Ensure the SpatialGridManager is properly assigned
3. Verify the grid is not hidden by opacity settings (try SetGridOpacity(1.0))

### Performance Issues
1. Reduce Grid Extent to cover smaller area
2. Increase Grid Square Size to use fewer squares
3. Disable wall grids if only floor reference is needed

### Grid Positioning Issues
1. Adjust Floor Y and Ceiling Y to match your scene
2. Check that the grid extent covers your play area
3. Ensure XR Origin is positioned correctly relative to the grid

## Customization

The system is designed to be easily customizable:
- Modify colors in the inspector or via code
- Add new grid patterns by modifying the checkerboard logic
- Create custom materials for different visual styles
- Add animation or dynamic effects to grid squares