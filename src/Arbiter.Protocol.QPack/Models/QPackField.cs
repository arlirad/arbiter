namespace Arlirad.Infrastructure.QPack.Models;

public class QPackField(string name, string? value = null)
{
    public string Name { get => name; }
    public string? Value { get => value; }

    public override bool Equals(object? obj)
    {
        return obj is QPackField other && other.Name == Name && other.Value == Value;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Value);
    }
}