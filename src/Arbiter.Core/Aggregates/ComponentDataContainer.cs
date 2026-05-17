namespace Arbiter.Core.Aggregates;

public class ComponentDataContainer
{
    private readonly Dictionary<Type, object> _data = [];

    public T Get<T>() where T : new() => (T)(_data.TryGetValue(typeof(T), out var value) ? value : _data[typeof(T)] = new T());
}
