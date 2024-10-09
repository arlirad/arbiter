using System;

namespace Arbiter;

[Identifier("process")]
public class ProcessStatement : IStatement
{
    public void Read(TokenStream stream)
    {
        string ext = stream.ExpectString();
        string processorName = stream.ExpectString();

        Server.Handler.BindProcessor(ext, processorName);
    }
}
