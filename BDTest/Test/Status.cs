using BDTest.Attributes;

namespace BDTest.Test;

public enum Status
{
    [EnumOrder(5)] Passed,
    [EnumOrder(1)] Failed,
    [EnumOrder(3)] Inconclusive,
    [EnumOrder(2)] NotImplemented,
    [EnumOrder(4)] Skipped
}
    
public static class StatusExtensions
{
    public static int GetOrder(this Status status)
    {
        var enumType  = typeof(Status);
        var memberInfos = enumType.GetMember(status.ToString());
        var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType);
        var valueAttributes = enumValueMemberInfo?.GetCustomAttributes(typeof(EnumOrderAttribute), false) ?? Array.Empty<object>();
        return (valueAttributes.FirstOrDefault() as EnumOrderAttribute)?.Order ?? -1;
    }
}