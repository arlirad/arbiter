namespace Arbiter.Core.Enums;

public static class StatusExtensions
{
    public static bool IsBodyForbidden(this Status status)
    {
        return (int)status switch {
            >= 100 and <= 199 => true,
            204 => true,
            304 => true,
            _ => false,
        };
    }
}
