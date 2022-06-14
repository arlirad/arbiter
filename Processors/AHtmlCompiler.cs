using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Arbiter {
    public class AHtmlCompiler {
        const char SPECIAL = '!';

        static readonly string[] QuickTerminations = new string[] {
            "using", "layout", "title", "section", "/section", "writesection", 
            "DOCTYPE",
        };

        static readonly ParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);

        public static CSharpCompilation CreatePageCompilation(string name, Stream input) {
            var reader = new StreamReader(input, leaveOpen: true, encoding: System.Text.Encoding.UTF8);

            var usings = new List<UsingDirectiveSyntax>();
            var classes = new List<MemberDeclarationSyntax>();
            var statements = new List<StatementSyntax>();

            int line = 1;
            int col = 0;
            bool end = false;

            int Read() {
                int v = reader.Read();
                if (v == -1)
                    return '\0';

                if (v == '\n') {
                    line++;
                    col = 0;
                }

                col++;

                return v;
            }

            var segments = new List<Segment>();
            var textsb = new StringBuilder();

            while (!end) {
                int c = Read();
                if (c == '<') {
                    c = Read();
                    if (c == SPECIAL) {
                        int spline = line;
                        int spcol = col;

                        segments.Add(new TextSegment(textsb));
                        textsb.Clear();

                        var sb = new StringBuilder();
                        var bb = string.Empty;
                        string nn = null;

                        for (;;) {
                            c = Read();

                            if (c == '\0') {
                                break;
                            }
                            else if (c == '>') {
                                if (nn == null) {
                                    nn = bb;
                                    bb = null;
                                }

                                if (QuickTerminations.Contains(nn))
                                    break;
                            }

                            if (c == SPECIAL) {
                                c = Read();
                                if (c == '>')
                                    break;
                                else
                                    sb.Append(SPECIAL);
                            }

                            sb.Append((char) c);

                            if (bb != null) {
                                if (bb.Length == 2 && bb == "--") {
                                    nn = bb;
                                    bb = null;

                                    for (;;) {
                                        c = Read();
                                        sb.Append((char) c);

                                        if (c == '-') {
                                            c = Read();
                                            sb.Append((char) c);

                                            if (c == '-') {
                                                c = Read();
                                                sb.Append((char) c);

                                                if (c == '>') {
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    break;
                                }

                                if (c == ' ') {
                                    nn = bb;
                                    bb = null;
                                }
                            }

                            if (bb != null)
                                bb += (char) c;
                        }

                        if (nn == null)
                            nn = "*";

                        switch (nn) {
                        case "DOCTYPE":
                            segments.Add(new TextSegment("<!", sb, ">"));
                            break;
                        case "--":
                            segments.Add(new TextSegment("<!", sb));
                            break;
                        case "using": {
                            usings.Add(UsingDirective(null, ParseName(sb.ToString().Substring(6))));
                        } break;
                        case "layout": {
                            string verb = sb.ToString().Substring(7).Trim();
                            var statement = ParseStatement("{ Page.Layout = " + verb + "; }", 0, ParseOptions, true);
                            segments.Add(new StatementSegment(statement));
                        } break;
                        case "title": {
                            string verb = sb.ToString().Substring(6).Trim();
                            var statement = ParseStatement("{ Page.Title = " + verb + "; }", 0, ParseOptions, true);
                            segments.Add(new StatementSegment(statement));
                        } break;
                        case "section": {
                            string verb = sb.ToString().Substring(8).Trim();
                            var statement = ParseStatement("{ Page.Section(" + verb + "); }", 0, ParseOptions, true);
                            segments.Add(new StatementSegment(statement));
                        } break;
                        case "/section": {
                            var statement = ParseStatement("{ Page.Section(null); }", 0, ParseOptions, true);
                            segments.Add(new StatementSegment(statement));
                        } break;
                        case "writesection": {
                            string verb = sb.ToString().Substring(13).Trim();
                            var statement = ParseStatement("{ Page.WriteSection(" + verb + "); }", 0, ParseOptions, true);
                            segments.Add(new StatementSegment(statement));
                        } break;
                        case "class": {
                            classes.Add(ParseMemberDeclaration(sb.ToString()));
                        } break;
                        default: {
                            string str = sb.ToString().Trim() + ";\n;";
                            int offset = 0;

                            //var statement = ParseStatement(sb.ToString() + ";", 0, null, false);
                            while (offset < str.Length) {
                                var statement = ParseStatement(str, offset, ParseOptions, false);
                                // Console.WriteLine("aaa");
                                // Console.WriteLine(str.Substring(offset));
                                offset += statement.FullSpan.Length;

                                segments.Add(new StatementSegment(statement));

                                if (statement.FullSpan.Length == 0) {
                                    statement = ParseStatement(str, offset, null, true);
                                    offset += statement.FullSpan.Length;
                                    segments.Add(new StatementSegment(statement));

                                    break;
                                }

                                while (offset < str.Length && char.IsWhiteSpace(str[offset]))
                                    offset++;
                            }
                        } break;
                        }

                        continue;
                    }
                    else {
                        textsb.Append('<');
                    }
                }
                else if (c == '\0') {
                    break;
                }

                textsb.Append((char) c);
            }

            segments.Add(new TextSegment(textsb));
            textsb.Clear();

            foreach (var @class in classes) {
                
            }

            foreach (var segment in segments) {
                if (segment is TextSegment text) {
                    string str = text.String.Trim();
                    if (str.Length == 0)
                        continue;

                    var statement = ExpressionStatement(InvocationExpression(ParseExpression("Page.Write"), ArgumentList().AddArguments(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(text.String)))))).WithTrailingTrivia(Comment("//bong"));
                    statements.Add(statement);
                }
                else if (segment is StatementSegment stat) {
                    statements.Add(stat.Statement);
                }
            }

            var runMembers = new List<MemberDeclarationSyntax>() {
                ParseMemberDeclaration("public static System.Threading.AsyncLocal<Arbiter.AHtmlPageState> AsyncLocalState = new System.Threading.AsyncLocal<Arbiter.AHtmlPageState>();"),

                ParseMemberDeclaration("private static Arbiter.AHtmlPageState State { get => AsyncLocalState.Value; }"),

                ParseMemberDeclaration("private static string Title { get => State.Title; set => State.Title = value; }"),
                ParseMemberDeclaration("private static string Layout { get => State.Layout; set => State.Layout = value; }"),

                ParseMemberDeclaration("private static Arbiter.Request Request { get => State.Request; }"),
                ParseMemberDeclaration("private static Arbiter.Response Response { get => State.Response; }"),
                
                ParseMemberDeclaration("private static void Write(object obj) { State.Write(obj); }"),
                ParseMemberDeclaration("private static void WriteLine(object obj) { State.WriteLine(obj); }"),
                
                ParseMemberDeclaration("private static void Section(string name) { State.Section(name); }"),
                ParseMemberDeclaration("private static void WriteSection(string name) { State.WriteSection(name); }"),
            };

            var runMainAsync = MethodDeclaration(ParseTypeName("System.Threading.Tasks.Task"), Identifier("MainAsync")).AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.AsyncKeyword)).AddBodyStatements(statements.ToArray());
            var runClass = ClassDeclaration(Identifier("Page")).AddModifiers(Token(SyntaxKind.PublicKeyword)).AddMembers(runMainAsync).AddMembers(runMembers.ToArray()).AddMembers(classes.ToArray());
            var runNamespace = NamespaceDeclaration(SyntaxFactory.ParseName("Arbiter.Page")).AddMembers(runClass);
            var classNamespace = NamespaceDeclaration(SyntaxFactory.ParseName("Arbiter.Page"));

            var runUnit = CompilationUnit().AddMembers(runNamespace).AddUsings(usings.ToArray());

            using (var bong = File.Open("/tmp/atest.txt", FileMode.OpenOrCreate, FileAccess.Write))
                using (var writer = new StreamWriter(bong))
                    writer.Write(runUnit.GetText());
                    
            var refs = new List<MetadataReference>();

            foreach (var assembly in AHtmlProcessor.Context.Assemblies)
                if (!string.IsNullOrEmpty(assembly.Location))
                    AHtmlProcessor.References.Add(MetadataReference.CreateFromFile(assembly.Location));

            reader.Dispose();

            return CSharpCompilation.Create(name, new[] { SyntaxFactory.SyntaxTree(runUnit) }, AHtmlProcessor.References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug));
        }

        public static CSharpCompilation CreateSourceFileCompilation(string name, Stream input) {
            SourceText sourceCode;
            
            using (var reader = new StreamReader(input, leaveOpen: true))
                sourceCode = SourceText.From(reader.ReadToEnd());

            var tree = SyntaxFactory.ParseSyntaxTree(sourceCode, ParseOptions);
            var refs = new List<MetadataReference>();

            foreach (var assembly in AHtmlProcessor.Context.Assemblies)
                if (!string.IsNullOrEmpty(assembly.Location))
                    AHtmlProcessor.References.Add(MetadataReference.CreateFromFile(assembly.Location));

            return CSharpCompilation.Create(name, new[] { tree }, AHtmlProcessor.References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug));
        }

        class Segment {}

        class TextSegment : Segment {
            public string String;

            public TextSegment(StringBuilder sb) {
                String = sb.ToString();
            }

            public TextSegment(string a, StringBuilder sb) {
                String = a + sb.ToString();
            }

            public TextSegment(string a, StringBuilder sb, string b) {
                String = a + sb.ToString() + b;
            }
        }

        class StatementSegment : Segment {
            public StatementSyntax Statement;

            public StatementSegment(StatementSyntax stat) {
                Statement = stat;
            }
        }
    }
}