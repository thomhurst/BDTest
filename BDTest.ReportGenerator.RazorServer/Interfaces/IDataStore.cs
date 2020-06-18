using System.Threading.Tasks;

namespace BDTest.ReportGenerator.RazorServer.Interfaces
{
    public interface IDataStore
    {
        public Task<string> GetDataFromStore(string id);
        public Task StoreData(string id, string data);
    }
}