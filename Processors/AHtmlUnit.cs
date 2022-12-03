using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Arbiter
{
    public class AHtmlUnit : IDisposable
    {
        public bool Success;
        public string LayoutPath;

        public Diagnostic[] Diagnostics;
        public Assembly Assembly;
        public AssemblyLoadContext Context;

        public AHtmlUnit(bool success, ImmutableArray<Diagnostic> diagnostics, Assembly assembly = null, AssemblyLoadContext context = null)
        {
            Success = success;
            Diagnostics = diagnostics.ToArray();
            Assembly = assembly;
            Context = context;
        }

        ~AHtmlUnit()
        {
            Dispose();
        }

        public void Dispose()
        {
            // if (Context != null) {
            //     Context.Unload(); 
            //     Context = null;
            // } 
        }

        public void Run(Request request, Response response, AHtmlPageState state)
        {
            var pageType = Assembly.GetType("Arbiter.Page.Page");
            var field = pageType.GetField("AsyncLocalState", BindingFlags.Public | BindingFlags.Static);
            var fieldValue = field.GetValue(null);
            var prop = field.FieldType.GetProperty("Value");
            prop.SetMethod.Invoke(fieldValue, new[] { state });

            var entry = Assembly.GetType("Arbiter.Page.Page").GetMethod("MainAsync");

            var task = (Task)entry.Invoke(null, null);
            task.GetAwaiter().GetResult();
        }

        public static AHtmlUnit CompilePage(Site site, string path)
        {
            using (var buffer = new MemoryStream())
            {
                CSharpCompilation compilation;

                using (var stream = File.OpenRead(path))
                    compilation = AHtmlCompiler.CreatePageCompilation(Path.GetFileName(path), stream);

                var result = compilation.Emit(buffer);
                if (!result.Success)
                    return new AHtmlUnit(false, result.Diagnostics);

                buffer.Position = 0;

                var assemblyLoadContext = new AssemblyLoadContext(path, true);
                var assembly = assemblyLoadContext.LoadFromStream(buffer);

                buffer.Position = 0;

                return new AHtmlUnit(true, result.Diagnostics, assembly, assemblyLoadContext);
            }
        }

        public static AHtmlUnit CompileSourceFile(string path)
        {
            using (var buffer = new MemoryStream())
            {
                CSharpCompilation compilation;

                using (var stream = File.OpenRead(path))
                {
                    compilation = AHtmlCompiler.CreateSourceFileCompilation(Path.GetFileName(path), stream);
                }

                var result = compilation.Emit(buffer);
                if (!result.Success)
                    return new AHtmlUnit(false, result.Diagnostics);

                buffer.Position = 0;

                var assemblyLoadContext = new AssemblyLoadContext(path, true);
                var assembly = assemblyLoadContext.LoadFromStream(buffer);

                return new AHtmlUnit(true, result.Diagnostics, assembly, assemblyLoadContext);
            }
        }

        private static void Assembly_ModuleResolve(object sender)
        {

        }
    }
}