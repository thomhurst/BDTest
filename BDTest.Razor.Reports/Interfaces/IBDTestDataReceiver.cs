using BDTest.Maps;

namespace BDTest.Razor.Reports.Interfaces;

public interface IBDTestDataReceiver
{
    public Task OnReceiveTestDataAsync(BDTestOutputModel bdTestOutputModel);
}