namespace Arbiter.Api.Http;

public class QueryCollection
{
    private readonly Dictionary<string, List<string>> _values;

    private QueryCollection(Dictionary<string, List<string>> values)
    {
        _values = values;
    }

    public string? this[string key] => _values.GetValueOrDefault(key)?.LastOrDefault();

    public static QueryCollection Empty
    {
        get;
    } = new([]);

    public IReadOnlyList<string>? GetValues(string key) => _values.GetValueOrDefault(key);

    public static QueryCollection Parse(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString) || queryString == "?")
            return Empty;

        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var q = queryString.StartsWith('?') ? queryString[1..] : queryString;

        foreach (var segment in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIndex = segment.IndexOf('=');
            var key = Uri.UnescapeDataString(eqIndex > 0 ? segment[..eqIndex] : segment);
            var value = eqIndex > 0 ? Uri.UnescapeDataString(segment[(eqIndex + 1)..]) : "";

            if (!values.TryGetValue(key, out var list))
            {
                list = [];
                values[key] = list;
            }

            list.Add(value);
        }

        return new QueryCollection(values);
    }
}
