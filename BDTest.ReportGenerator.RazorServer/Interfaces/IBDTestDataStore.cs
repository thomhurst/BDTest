using System.Threading.Tasks;
using BDTest.Maps;

namespace BDTest.ReportGenerator.RazorServer.Interfaces
{
    public interface IBDTestDataStore
    {
        public Task<BDTestOutputModel> GetDataFromStore(string id);
        public Task StoreData(string id, BDTestOutputModel data);
    }
}