using System;
using System.Net;

namespace Arbiter
{
    [Identifier("listen")]
    public class ListenStatement : IStatement
    {
        public void Read(TokenStream stream)
        {
            stream.ExpectString(out string address);
            if (address == "*")
            {
                Server.Listener.Bind(IPAddress.Any);
                return;
            }

            Server.Listener.Bind(IPAddress.Parse(address));
        }
    }
}