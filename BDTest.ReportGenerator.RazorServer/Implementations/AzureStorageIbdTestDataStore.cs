using System.Threading.Tasks;
using BDTest.ReportGenerator.RazorServer.Interfaces;

namespace BDTest.ReportGenerator.RazorServer.Implementations
{
    public class AzureStorageIbdTestDataStore : IBDTestDataStore
    {
        public Task<string> GetDataFromStore(string id)
        {
            throw new System.NotImplementedException();
        }

        public Task StoreData(string id, string data)
        {
            throw new System.NotImplementedException();
        }
    }
}