using System;

namespace Arbiter;

[AttributeUsage(AttributeTargets.Class)]
public class IdentifierAttribute : System.Attribute
{
    public string Identifier;

    public IdentifierAttribute(string identifier)
    {
        Identifier = identifier;
    }
}

public interface IStatement
{
    void Read(TokenStream stream);
}