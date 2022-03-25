using System.Threading.Tasks;
using BDTest.Maps;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces;

public interface IBDTestDataReceiver
{
    public Task OnReceiveTestDataAsync(BDTestOutputModel bdTestOutputModel);
}