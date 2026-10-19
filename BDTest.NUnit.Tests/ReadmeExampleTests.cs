using System.Threading.Tasks;
using BDTest;
using NUnit.Framework;

namespace BDTest.Tests.ReadmeExample;

[Story(AsA = "account holder",
    IWant = "to withdraw money",
    SoThat = "I can use my savings")]
public class ReadmeExampleTests : NUnitBDTestBase<AccountContext>
{
    // The base class creates a separate Context for each NUnit test.
    private AccountSteps Account => new AccountSteps(Context);
    private AccountAssertionsSteps AccountAssertions => new AccountAssertionsSteps(Context);

    [Test]
    [ScenarioText("Withdrawing money reduces the account balance")]
    public async Task WithdrawMoney()
    {
        await Given(() => Account.CreateAnAccount(100m))
            .When(() => Account.Withdraw(25m))
            .Then(() => AccountAssertions.TheBalanceIs(75m))
            .BDTestAsync();
    }
}

public class AccountContext
{
    public decimal Balance { get; set; }
}

public class AccountSteps
{
    private readonly AccountContext _context;

    public AccountSteps(AccountContext context)
    {
        _context = context;
    }

    [StepText("I have an account with a balance of {0}")]
    public void CreateAnAccount(decimal openingBalance)
    {
        _context.Balance = openingBalance;
    }

    [StepText("I withdraw {0}")]
    public void Withdraw(decimal amount)
    {
        _context.Balance -= amount;
    }
}

public class AccountAssertionsSteps
{
    private readonly AccountContext _context;

    public AccountAssertionsSteps(AccountContext context)
    {
        _context = context;
    }

    [StepText("the account balance is {0}")]
    public void TheBalanceIs(decimal expectedBalance)
    {
        Assert.That(_context.Balance, Is.EqualTo(expectedBalance));
    }
}
