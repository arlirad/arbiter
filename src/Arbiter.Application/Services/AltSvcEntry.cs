namespace Arbiter.Application.Services;

public sealed record AltSvcEntry(string ProtocolId, string Authority, int MaxAge, bool Persist);