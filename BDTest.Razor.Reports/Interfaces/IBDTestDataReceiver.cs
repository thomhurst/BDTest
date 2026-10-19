using BDTest.Contracts;

namespace BDTest.Razor.Reports.Interfaces;

public interface IBDTestDataReceiver
{
    public Task OnReceiveTestDataAsync(BDTestReport bdTestOutputModel);
}