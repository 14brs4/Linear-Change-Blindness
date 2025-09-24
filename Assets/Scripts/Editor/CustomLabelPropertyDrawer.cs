#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// Custom property drawer to show custom labels
[CustomPropertyDrawer(typeof(CustomLabelAttribute))]
public class CustomLabelPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CustomLabelAttribute customLabel = (CustomLabelAttribute)attribute;
        EditorGUI.PropertyField(position, property, new GUIContent(customLabel.label));
    }
}
#endif
