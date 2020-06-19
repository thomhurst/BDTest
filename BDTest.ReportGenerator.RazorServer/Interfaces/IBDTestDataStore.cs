using System.Threading.Tasks;

namespace BDTest.ReportGenerator.RazorServer.Interfaces
{
    public interface IBDTestDataStore
    {
        public Task<string> GetDataFromStore(string id);
        public Task StoreData(string id, string data);
    }
}