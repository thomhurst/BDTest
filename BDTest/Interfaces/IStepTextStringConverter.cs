namespace BDTest.Interfaces
{
    public interface IStepTextStringConverter<T>
    {
        public string ConvertToString(T t);
    }
}