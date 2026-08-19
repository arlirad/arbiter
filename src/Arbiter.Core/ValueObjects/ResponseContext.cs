using Arbiter.Core.Enums;

namespace Arbiter.Core.ValueObjects;

public class ResponseContext
{
    private readonly Headers _headers = [];

    internal ResponseContext()
    {
        Headers = new ReadOnlyHeaders(_headers);
    }

    public Status? Status
    {
        get;
        private set;
    }
    public ReadOnlyHeaders Headers
    {
        get;
    }
    public Stream? Stream
    {
        get;
        private set;
    }

    public bool HasResponse => Status is not null;

    public string? ContentType
    {
        get => _headers.ContentType;
        set => _headers.ContentType = value;
    }

    public ValueTask Set(Status status, Stream? stream = null)
    {
        Status = status;
        Stream = stream;

        return ValueTask.CompletedTask;
    }

    public void AddHeader(string name, string value) => _headers.Add(name, value);

    public void SetHeader(string name, string value) => _headers.Replace(name, value);

    public void SetHeader(string name, IReadOnlyList<string> values) => _headers[name] = values;

    public bool RemoveHeader(string name)
    {
        if (_headers[name] is null)
            return false;

        _headers[name] = null;

        return true;
    }

    public void AppendHeader(string name, string value)
    {
        var values = (List<string>?)_headers[name];

        if (values is null)
        {
            _headers.Add(name, value);

            return;
        }

        foreach (var existing in values)
        {
            if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                return;
        }

        values.Add(value);
    }

    public string? Header(string name) => Headers[name]?.FirstOrDefault();
}
