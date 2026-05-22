using Serilog;

namespace Arbiter.Application.Services;

public sealed class AltSvcService
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "altsvc");
    private readonly Dictionary<string, AltSvcEntry> _entries = [];

    public string? HeaderValue
    {
        get;
        private set;
    }

    public void Set(string protocolId, string authority, int maxAge, bool persist = false)
    {
        if (_entries.TryGetValue(protocolId, out var existing) && existing.Authority == authority && existing.MaxAge == maxAge && existing.Persist == persist)
            return;

        _entries[protocolId] = new AltSvcEntry(protocolId, authority, maxAge, persist);
        Rebuild();
        Log.Information("Added: {ProtocolId}=\"{Authority}\"; MaxAge={MaxAge}", protocolId, authority, maxAge);
    }

    public void Remove(string protocolId)
    {
        if (_entries.Remove(protocolId))
        {
            Rebuild();
            Log.Information("Removed: {ProtocolId}", protocolId);
        }
    }

    private void Rebuild()
    {
        HeaderValue = _entries.Count == 0
            ? null
            : string.Join(", ", _entries.Values.Select(e => {
                var s = $"{e.ProtocolId}=\"{e.Authority}\"; ma={e.MaxAge}";

                if (e.Persist)
                {
                    s += "; persist=1";
                }

                return s;
            }));
    }
}
