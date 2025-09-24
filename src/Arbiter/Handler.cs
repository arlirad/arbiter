using System;

namespace Arbiter;

public class Handler
{
    public Dictionary<string, Site> Sites = new();
    public Dictionary<string, string> Mime = new();

    public async Task<Response> Handle(Request request)
    {
        var response = new Response();
        var site = FindSite(request.Uri);

        response.Headers["access-control-allow-origin"] = "*";

        if (request.Uri.LocalPath == "/.debug/cache")
        {
            response.Stream = new MemoryStream();
            response.SetCode(200);
            Server.Cache.WriteDebugInfo(response.Stream);

            return response;
        }

        if (site == null)
        {
            response.SetCode(404);
            response.Mime = "text/html";
            response.Stream = Server.Cache.GetFile($"err/{response.Code}.html");
            return response;
        }

        request.Site = site;

        return response;
    }

    private Site FindSite(Uri uri)
    {
        foreach (var site in Sites)
        {
            foreach (var binding in site.Value.Bindings)
            {
                if (binding.Scheme == uri.Scheme && binding.Host == uri.Host && binding.Port == uri.Port)
                    return site.Value;
            }
        }

        return null;
    }

    private bool ResolveMime(string ext, out string? mime)
    {
        if (!Mime.TryGetValue(ext, out mime))
            Mime.TryGetValue(".*", out mime);

        return mime != null;
    }

    private Response GetExceptionPage(Exception e)
    {
        Console.WriteLine(e);

        var response = new Response();

        response.SetCode(500);
        response.Stream = new MemoryStream();

        if (Server.Cache.GetFile($"err/{response.Code}.html", out Stream err_stream))
        {
            string page;

            using (var reader = new StreamReader(err_stream))
                page = reader.ReadToEnd();

            page = page.Replace("<?exception_data?>", e.ToString());

            using var writer = new StreamWriter(response.Stream, null, -1, true);
            writer.Write(page);
        }
        else
        {
            using var writer = new StreamWriter(response.Stream, null, -1, true);
            writer.WriteLine(e.ToString());
        }

        return response;
    }
}
