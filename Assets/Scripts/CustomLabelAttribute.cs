using UnityEngine;

// Custom attribute to display custom labels in Unity Inspector
public class CustomLabelAttribute : PropertyAttribute
{
    public string label;

    public CustomLabelAttribute(string label)
    {
        this.label = label;
    }
}
