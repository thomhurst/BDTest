using System;

namespace BDTest.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class ScenarioTextAttribute : Attribute
{
    public string Text { get; }

    public ScenarioTextAttribute(string stepText)
    {
        Text = stepText;
    }
}