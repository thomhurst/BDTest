namespace BDTest.Interfaces;

public interface IStepTextStringConverter<in T>
{
    string ConvertToString(T t);
}