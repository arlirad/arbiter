namespace Arbiter;

public static class ConfigReader
{
    private static Dictionary<string, IStatement> _statements = new Dictionary<string, IStatement>();

    static ConfigReader()
    {
        GatherStatements();
    }

    public static void ReadFromFile(string path)
    {
        var stream = TokenStream.Tokenize(path, File.OpenRead(path));

        while (!stream.EndOfStream)
        {
            if (stream.AcceptIdentifier(out string? identifier) && _statements.TryGetValue(identifier, out IStatement? statement))
                statement.Read(stream);
            else
                throw new UnexpectedTokenException(stream.Peek() ?? throw new EndOfStreamException());
        }
    }

    private static void GatherStatements()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IStatement).IsAssignableFrom(p));

        foreach (var type in types)
        {
            if (type.IsInterface)
                continue;

            string? identifier = null;
            var attributes = type.GetCustomAttributes(false);

            foreach (var attribute in attributes)
            {
                if (attribute is IdentifierAttribute casted)
                    identifier = casted.Identifier;
            }

            if (identifier == null)
                continue;

            var instance = Activator.CreateInstance(type);
            if (instance != null)
                _statements[identifier] = instance as IStatement;
        }
    }
}