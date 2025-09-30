namespace Arlirad.QPack.Models;

public class QPackField(string name, string? value = null)
{
    public string Name { get => name; }
    public string? Value { get => value; }
}