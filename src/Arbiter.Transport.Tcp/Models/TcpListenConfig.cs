using System.Net;

namespace Arbiter.Transport.Tcp.Models;

public sealed record TcpListenConfig(List<IPAddress> Addresses, List<int> Ports);