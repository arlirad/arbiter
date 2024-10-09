using System;

namespace Arbiter;

[Identifier("site")]
public class SiteStatement : IStatement
{
    public void Read(TokenStream stream)
    {
        var site = new Site();

        stream.ExpectString(out string name);
        stream.ExpectOperator("{");

        while (!stream.AcceptOperator("}"))
        {
            stream.ExpectIdentifier(out string identifier);

            switch (identifier)
            {
                case "path":
                    stream.ExpectString(out site.Path);
                    break;
                case "listen":
                    site.Bindings.Add(new Uri(stream.ExpectString().Replace("*", "0.0.0.0")));
                    break;
                case "rewrite":
                    site.Rewriters.Add(stream.ExpectString());
                    break;
                case "process":
                    site.Processor = Server.Handler.Processors[stream.ExpectString()];
                    break;
                case "parameter":
                    string key = stream.ExpectString();
                    string value = stream.ExpectString();

                    site.Parameters[key] = value;
                    break;
                case "default":
                    if (stream.AcceptString(out string doc))
                    {
                        site.DefaultDocs.Add(doc);
                        break;
                    }

                    stream.ExpectOperator("{");

                    while (!stream.AcceptOperator("}"))
                    {
                        site.DefaultDocs.Add(stream.ExpectString());
                    }
                    break;
            }
        }

        Server.Handler.Sites[name] = site;
    }
}
