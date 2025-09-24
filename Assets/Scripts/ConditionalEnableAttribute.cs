using UnityEngine;

/// <summary>
/// Attribute to conditionally enable/disable fields in the Unity Inspector based on boolean values.
/// Use this to grey out sliders or other fields when certain conditions are not met.
/// </summary>
public class ConditionalEnableAttribute : PropertyAttribute
{
    public string BooleanFieldName { get; }
    public bool EnableWhenTrue { get; }
    
    /// <summary>
    /// Creates a conditional enable attribute.
    /// </summary>
    /// <param name="booleanFieldName">The name of the boolean field to check</param>
    /// <param name="enableWhenTrue">If true, enables field when boolean is true. If false, enables when boolean is false.</param>
    public ConditionalEnableAttribute(string booleanFieldName, bool enableWhenTrue = true)
    {
        BooleanFieldName = booleanFieldName;
        EnableWhenTrue = enableWhenTrue;
    }
}
