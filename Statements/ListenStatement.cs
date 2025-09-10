using System;
using System.Net;

namespace Arbiter;

[Identifier("listen")]
public class ListenStatement : IStatement
{
    public void Read(TokenStream stream)
    {
        stream.ExpectString(out string address);
        Server.Listener.Bind(IPAddress.Parse(address));
    }
}
