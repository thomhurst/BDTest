namespace BDTest.Tests;

public class CustomClassToStepTextString
{
    public string One { get; set; } = "1";
    public string Two { get; set; } = "2";
    public string Three { get; set; } = "3";
    public string Four { get; set; } = "4";
    public string Five { get; set; } = "5";

    public override string ToString()
    {
        return "Out of the box ToString() called";
    }
}