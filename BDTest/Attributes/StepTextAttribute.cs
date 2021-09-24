using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BDTest.Exceptions;
// ReSharper disable UnusedMember.Global

namespace BDTest.Attributes
{
    /// <summary>
    /// Attribute <c>StepText</c> is used to customise the Gherkin Step Text output that BDDfy produces when running a scenario,
    /// as the default constructed text doesn't always read well and is often cluttered with junk.
    /// <para>Usage:</para>
    /// <para>[StepText("This is your custom step text. {0} is the first parameter, {1} is the second. etc.")]</para>
    /// <para>public void This_Is_My_Step_Method(string param1, string param2) { ... }</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class StepTextAttribute : Attribute
    {
        public string Text { get; }
        public List<string> StepTextParameterOverrides { get; } = new List<string>();

        public StepTextAttribute(string stepText)
        {
            Text = stepText;
        }

        public StepTextAttribute(string stepText, string parameterOverride)
        {
            Text = stepText;

            if (!string.IsNullOrEmpty(parameterOverride) && !Regex.IsMatch(parameterOverride, "^(\\d+):"))
            {
                throw new InvalidStepTextParameterException(
                    "The parameter override must begin with an index and a colon. E.g. \"0:Overriding Parameter 0\"");
            }

            StepTextParameterOverrides.Add(parameterOverride);
        }
        
        public StepTextAttribute(string stepText, string[] parameterOverrides)
        {
            Text = stepText;

            foreach (var parameterOverride in parameterOverrides)
            {
                if (!string.IsNullOrEmpty(parameterOverride) && Regex.IsMatch(parameterOverride, "^(\\d+):"))
                {
                    throw new InvalidStepTextParameterException(
                        "The parameter override must begin with an index and a colon. E.g. \"0:Overriding Parameter 0\"");
                }
            }

            StepTextParameterOverrides.AddRange(parameterOverrides);
        }
    }
}
