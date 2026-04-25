namespace Arbiter.Api.Http;

public class RouteValueDictionary
{
    private readonly Dictionary<string, string> _values;

    internal RouteValueDictionary(Dictionary<string, string> values)
    {
        _values = values;
    }

    public string? this[string key] => _values.GetValueOrDefault(key);

    public static RouteValueDictionary Empty
    {
        get;
    } = new([]);
}