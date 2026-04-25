namespace Arlirad.Infrastructure.QPack.Models;

public class QPackField(string name, string? value = null)
{
    public string Name => name;
    public string? Value => value;

    public override bool Equals(object? obj) => obj is QPackField other && other.Name == Name && other.Value == Value;

    public override int GetHashCode() => HashCode.Combine(Name, Value);
}