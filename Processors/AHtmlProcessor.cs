using System;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Arbiter {
    [Name("ahtmlproc")]
    public class AHtmlProcessor : IProcessor {
        public static readonly List<MetadataReference> References = new List<MetadataReference>();
        public readonly Dictionary<string, AHtmlUnit> _sourceFiles = new Dictionary<string, AHtmlUnit>();

        public static AssemblyLoadContext Context { get; private set; }
        public static AssemblyDependencyResolver Resolver = new AssemblyDependencyResolver("ahtml");

        private SegmentHandle _sourceSegment; 
        private object _lock = new object();

        public AHtmlProcessor() {
            Context = new AssemblyLoadContext("ahtml", true);
            Context.Resolving += Context_Resolving;

            ReadConfig("cfg/ahtml.cfg");

            References.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            References.Add(MetadataReference.CreateFromFile(typeof(System.Uri).Assembly.Location));
            References.Add(MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location));
            References.Add(MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location));
            References.Add(MetadataReference.CreateFromFile(typeof(Server).Assembly.Location));
            References.Add(MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location));
            References.Add(MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location));

            _sourceSegment = Server.Cache.WatchSegment("ahtml");
            _sourceSegment.OnChanged += SourceSegment_OnChanged;
            _sourceSegment.Start();
        }

        private Assembly? Context_Resolving(AssemblyLoadContext context, AssemblyName name) {
            Console.WriteLine("Loading " + name);
            return context.LoadFromAssemblyName(name);
        }

        public void Process(Stream stream, Request request, Response response) {
            string lpath = request.RewrittenUri.LocalPath;
            string path = request.Site.Path + lpath;
            var unit = GetUnit(request, path);

            response.Stream = new LimitedMemoryStream(1048576 * 32);

            if (!unit.Success) {
                CompilationError(response, unit, lpath);
                return;
            }

            var state = new AHtmlPageState(response.Stream);

            state.Request = request;
            state.Response = response;

            response.SetCode(200);
            response.Mime = "text/html; charset=utf-8";

            int recursions = 0;

            try {
                do {
                    state.Layout = null;
                    state.Section(null);

                    unit.Run(request, response, state);
                    if (!unit.Success) {
                        CompilationError(response, unit, lpath);
                        return;
                    }

                    if (state.Layout == null)
                        break;

                    if (recursions++ > 16)
                        throw new Exception("too many recursions");

                    state.Clear();

                    lpath = state.Layout;
                    path = request.Site.Path + lpath;

                    unit = GetUnit(request, path);
                }
                while (true);
            }
            catch (Exception e) {
                RuntimeError(response, unit, lpath, e);
            }

            state.Section(null);
            state.Flush();
        }

        public static void ReadConfig(string path) {
            var stream = TokenStream.Tokenize(path, File.OpenRead(path));

            while (!stream.EndOfStream) {
                stream.ExpectIdentifier(out string identifier);

                switch (identifier) {
                case "assembly": {
                    if (stream.AcceptString(out string assemblyName)) {
                        var assembly = Context.LoadFromAssemblyName(new AssemblyName(assemblyName));

                        References.Add(MetadataReference.CreateFromFile(assembly.Location));
                        break;
                    }

                    stream.ExpectOperator("{");

                    while (!stream.AcceptOperator("}")) {
                        var assembly = Context.LoadFromAssemblyName(new AssemblyName(stream.ExpectString()));

                        References.Add(MetadataReference.CreateFromFile(assembly.Location));
                    }
                } break;
                }
            }
        }

        private AHtmlUnit GetUnit(Request request, string path) {
            var unit = Server.Cache.GetTie<AHtmlUnit>(path);
            if (unit == null) {
                lock (_lock) {
                    unit = Server.Cache.GetTie<AHtmlUnit>(path);
                    if (unit == null) {
                        unit = AHtmlUnit.CompilePage(request.Site, path);
                        Server.Cache.SetTie(path, unit);
                    }
                }
            }

            return unit;
        }

        public void CompilationError(Response response, AHtmlUnit unit, string path) {
            response.SetCode(500);
            response.Mime = "text/html";

            using (var writer = new StreamWriter(response.Stream, null, -1, true)) {
                writer.WriteLine("<meta charset=\"utf-8\">");
                writer.WriteLine($"Failed to compile <b>{path}</b>");
                writer.WriteLine("<br>");

                foreach (var diagnostic in unit.Diagnostics) {
                    writer.Write(diagnostic);
                    writer.WriteLine("<br>");
                }
            }
        }

        public void RuntimeError(Response response, AHtmlUnit unit, string path, Exception e) {
            response.SetCode(500);
            response.Mime = "text/html";

            using (var writer = new StreamWriter(response.Stream, null, -1, true)) {
                writer.WriteLine("<meta charset=\"utf-8\">");
                writer.WriteLine($"Exception while executing <b>{path}</b>");
                writer.WriteLine("<br>");
                writer.WriteLine(e);
            }
        }

        void SourceSegment_OnChanged(object sender, string path) {
            Console.WriteLine(path);
            // Console.WriteLine(File.ReadAllText(path));
            
            if (!File.Exists(path)) {
                _sourceFiles[path].Dispose();
                _sourceFiles[path] = null;
            }
            else {
                var unit = AHtmlUnit.CompileSourceFile(path);
                if (unit.Success) {
                    _sourceFiles[path] = unit;
                }
                else {
                    foreach (var diagnostic in unit.Diagnostics)
                        Console.WriteLine(diagnostic);
                }
            }
        }
    }

    public class LimitedMemoryStream : MemoryStream {
        private int _max = 0;

        public LimitedMemoryStream(int max) : base() {
            _max = max;
        }

        public override void Write(byte[] buffer, int offset, int count) {
            if (base.Position > _max)
                throw new OutOfMemoryException();

            base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value) {
            if (base.Position > _max)
                throw new OutOfMemoryException();

            base.WriteByte(value);
        }
    }
}