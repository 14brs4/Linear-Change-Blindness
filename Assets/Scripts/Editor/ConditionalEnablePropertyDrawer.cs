#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Property drawer for ConditionalEnableAttribute that handles enabling/disabling fields
/// in the Unity Inspector based on boolean conditions.
/// </summary>
[CustomPropertyDrawer(typeof(ConditionalEnableAttribute))]
public class ConditionalEnablePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Get the ConditionalEnable attribute
        ConditionalEnableAttribute conditionalAttribute = (ConditionalEnableAttribute)attribute;
        
        // Find the boolean property to check
        SerializedProperty booleanProperty = property.serializedObject.FindProperty(conditionalAttribute.BooleanFieldName);
        
        bool shouldEnable = true;
        
        if (booleanProperty != null)
        {
            // Determine if field should be enabled based on boolean value and attribute settings
            bool booleanValue = booleanProperty.boolValue;
            shouldEnable = conditionalAttribute.EnableWhenTrue ? booleanValue : !booleanValue;
        }
        else
        {
            // If boolean property not found, show warning and enable field
            Debug.LogWarning($"[ConditionalEnable] Boolean field '{conditionalAttribute.BooleanFieldName}' not found for property '{property.displayName}'");
        }
        
        // Store previous GUI enabled state
        bool previousGUIEnabled = GUI.enabled;
        
        // Set GUI enabled state based on condition
        GUI.enabled = shouldEnable;
        
        // Draw the property field
        EditorGUI.PropertyField(position, property, label, true);
        
        // Restore previous GUI enabled state
        GUI.enabled = previousGUIEnabled;
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Return standard property height
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif
