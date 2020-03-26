using System.Runtime.CompilerServices;

namespace BDTest.Helpers
{
    public class TwoKeyConditionalWeakTable<TKey1, TKey2, TObject>
        where TKey1 : class
        where TKey2 : class
        where TObject : class
    {
        private readonly ConditionalWeakTable<TKey1, ConditionalWeakTable<TKey2, TObject>> _conditionalWeakTable = new
            ConditionalWeakTable<TKey1, ConditionalWeakTable<TKey2, TObject>>();

        public void Add(TKey1 key, TKey2 key2, TObject value)
        {
            var innerTable = GetInnerTable(key);
            innerTable.Add(key2, value);
        }

        public TObject GetOrCreateValue(TKey1 key, TKey2 key2)
        {
            var innerTable = GetInnerTable(key);
            return innerTable.GetOrCreateValue(key2);
        }

        public bool Remove(TKey1 key, TKey2 key2)
        {
            var innerTable = GetInnerTable(key);
            return innerTable.Remove(key2);
        }
        
        public bool Remove(TKey1 key)
        {
            return _conditionalWeakTable.Remove(key);
        }

        public bool TryGetValue(TKey1 key, TKey2 key2, out TObject value)
        {
            var innerTable = GetInnerTable(key);
            return innerTable.TryGetValue(key2, out value);
        }

        private ConditionalWeakTable<TKey2, TObject> GetInnerTable(TKey1 key1)
        {
            return _conditionalWeakTable.GetOrCreateValue(key1);
        }
    }
}