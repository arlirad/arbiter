namespace Arbiter.Configuration;

public interface ITransportConfig;

public static class ConfigTypeExtensions
{
    public static Type? GetConfigType<TConfigType>(this Type type)
    {
        return type.GetInterfaces()
            .Where(i => i.IsGenericType && i.Name.StartsWith("IAsyncConfigurable"))
            .SelectMany(i => i.GetGenericArguments())
            .FirstOrDefault(t => typeof(TConfigType).IsAssignableFrom(t));
    }
}
