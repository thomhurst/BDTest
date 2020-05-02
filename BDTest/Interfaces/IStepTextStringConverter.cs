namespace BDTest.Interfaces
{
    public interface IStepTextStringConverter<in T>
    {
        public string ConvertToString(T t);
    }
}