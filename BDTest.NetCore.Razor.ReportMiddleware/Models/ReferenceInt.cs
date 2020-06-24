namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class ReferenceInt
    {
        private int _int;

        public ReferenceInt(int integer)
        {
            _int = integer;
        }
        
        public int Get() => _int;
        public ReferenceInt IncrementAndGet()
        {
            ++_int;
            return this;
        }
    }
}