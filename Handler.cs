using System;

namespace Arbiter;

public class Handler
{
    public Dictionary<string, IProcessor> Processors = new Dictionary<string, IProcessor>();
    public Dictionary<string, IProcessor> ProcessorBindings = new Dictionary<string, IProcessor>();
    public Dictionary<string, IRewriter> Rewriters = new Dictionary<string, IRewriter>();
    public Dictionary<string, Site> Sites = new Dictionary<string, Site>();
    public Dictionary<string, string> Mime = new Dictionary<string, string>();

    public Handler()
    {
        GatherProcessors();
        GatherRewriters();
    }

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

        try
        {
            foreach (string rewriter in site.Rewriters)
                Rewriters[rewriter].Rewrite(request);
        }
        catch (Exception e)
        {
            return GetExceptionPage(e);
        }

        string path = site.Path + request.Uri.LocalPath;
        string filename = Path.GetFileName(path);

        if (Server.Cache.GetFile(path, out Stream stream))
        {
            string ext = Path.GetExtension(path);

            if (filename.Length < 32 && ProcessorBindings.TryGetValue(ext, out var processor))
            {
                try
                {
                    processor.Process(stream, request, response);
                }
                catch (Exception e)
                {
                    return GetExceptionPage(e);
                }
            }
            else if (ResolveMime(ext, out string? mime))
            {
                response.SetCode(200);
                response.Mime = mime;
                response.Stream = stream;
            }
            else
            {
                response.SetCode(403);
            }
        }
        else if (Directory.Exists(path))
        {
            bool found = false;

            foreach (var doc in site.DefaultDocs)
            {
                string docpath = Path.Combine(path, doc);

                if (Server.Cache.GetFile(docpath, out Stream docstream))
                {
                    string ext = Path.GetExtension(docpath);

                    request.RewrittenUri = new Uri(request.Uri, Path.GetFileName(docpath));

                    if (filename.Length < 32 && ProcessorBindings.TryGetValue(ext, out var processor))
                    {
                        try
                        {
                            processor.Process(stream, request, response);
                        }
                        catch (Exception e)
                        {
                            return GetExceptionPage(e);
                        }
                    }
                    else if (ResolveMime(ext, out string? mime))
                    {
                        response.SetCode(200);
                        response.Mime = mime;
                        response.Stream = docstream;
                    }
                    else
                    {
                        response.SetCode(403);
                    }

                    found = true;
                    break;
                }
            }

            if (!found)
                response.SetCode(404);
        }
        else
        {
            response.SetCode(404);
        }

        if (response.Stream == null && !response.SimpleResponse && !response.DontRespond)
        {
            response.Mime = "text/html";

            if (request.Method != "HEAD")
                response.Stream = Server.Cache.GetFile($"err/{response.Code}.html");
        }

        if (request.Method == "HEAD")
        {
            if (response.Stream != null)
                response.Stream.Dispose();

            response.Stream = null;
        }

        return response;
    }

    public void BindProcessor(string extension, string processorName)
    {
        ProcessorBindings[extension] = Processors[processorName];
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

            using (var writer = new StreamWriter(response.Stream, null, -1, true))
                writer.Write(page);
        }
        else
        {
            using (var writer = new StreamWriter(response.Stream, null, -1, true))
                writer.WriteLine(e.ToString());
        }

        return response;
    }

    private void GatherProcessors()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IProcessor).IsAssignableFrom(p));

        foreach (var type in types)
        {
            if (type.IsInterface)
                continue;

            string name = null;
            var attributes = type.GetCustomAttributes(false);

            foreach (var attribute in attributes)
            {
                if (attribute is NameAttribute casted)
                    name = casted.Name;
            }

            if (name == null)
                continue;

            var instance = Activator.CreateInstance(type);
            if (instance != null)
                Processors[name] = instance as IProcessor;
        }
    }

    private void GatherRewriters()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IRewriter).IsAssignableFrom(p));

        foreach (var type in types)
        {
            if (type.IsInterface)
                continue;

            string name = null;
            var attributes = type.GetCustomAttributes(false);

            foreach (var attribute in attributes)
            {
                if (attribute is NameAttribute casted)
                    name = casted.Name;
            }

            if (name == null)
                continue;

            var instance = Activator.CreateInstance(type);
            if (instance != null)
                Rewriters[name] = instance as IRewriter;
        }
    }
}
