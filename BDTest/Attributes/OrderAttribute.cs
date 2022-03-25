namespace BDTest.Attributes;

internal class EnumOrderAttribute : Attribute
{
    internal readonly int Order;

    internal EnumOrderAttribute(int order)
    {
        Order = order;
    }
}