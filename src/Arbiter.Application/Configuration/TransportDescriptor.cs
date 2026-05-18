namespace Arbiter.Application.Configuration;

public sealed record TransportDescriptor(string Key, Type AcceptorType, Type ConfigType);