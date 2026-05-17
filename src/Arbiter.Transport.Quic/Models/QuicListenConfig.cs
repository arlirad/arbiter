using System.Net;

namespace Arbiter.Transport.Quic.Models;

public sealed record QuicListenConfig(List<IPAddress> Addresses, List<int> Ports);