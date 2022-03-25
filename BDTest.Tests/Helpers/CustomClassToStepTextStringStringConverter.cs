using BDTest.Interfaces;

namespace BDTest.Tests.Helpers;

public class CustomClassToStepTextStringStringConverter : IStepTextStringConverter<CustomClassToStepTextString>
{
    public string ConvertToString(CustomClassToStepTextString t)
    {
        return t.One + t.Two + t.Three + ".................." + t.Four + "...................................." +
               t.Five;
    }
}