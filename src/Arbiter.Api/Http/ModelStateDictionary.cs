namespace Arbiter.Api.Http;

public class ModelStateDictionary
{
    private readonly Dictionary<string, List<ModelError>> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public IEnumerable<ModelError> AllErrors => _errors.Values.SelectMany(e => e);

    public void AddModelError(string key, string error)
    {
        if (!_errors.TryGetValue(key, out var list))
        {
            list = [];
            _errors[key] = list;
        }

        list.Add(new ModelError(key, error));
    }

    public void AddModelError(string key, string error, string? attemptedValue)
    {
        if (!_errors.TryGetValue(key, out var list))
        {
            list = [];
            _errors[key] = list;
        }

        list.Add(new ModelError(key, error, attemptedValue));
    }

    public IReadOnlyList<ModelError> GetErrors(string key) => _errors.TryGetValue(key, out var list) ? list : [];

    public Dictionary<string, string[]> ToSerializableDictionary()
    {
        return _errors.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(e => e.ErrorMessage).ToArray()
        );
    }
}

public record ModelError(string Key, string ErrorMessage, string? AttemptedValue = null);
