public class CompanyTestBase { }

public sealed class BasketContext
{
    private decimal _total;
    public void Empty() => _total = 0;
    public void Add(string item, decimal price) => _total += price;
    public void VerifyTotal(decimal expected)
    {
        if (_total != expected) throw new InvalidOperationException("Unexpected basket total.");
    }
}

public sealed class BasketSteps(BasketContext context)
{
    public void IsEmpty() => context.Empty();
    public void Add(string item, decimal price) => context.Add(item, price);
    public void TheTotalIs(decimal expected) => context.VerifyTotal(expected);
}
