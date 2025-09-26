namespace Arbiter.Services;

internal class ComponentTypeRegistry<T>(IDictionary<string, Type> components)
{
    private readonly Dictionary<string, Type> _components = new(components);

    public Type? Get(string name) => _components.GetValueOrDefault(name);
}