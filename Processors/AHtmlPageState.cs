using System;

namespace Arbiter
{
    public class AHtmlPageState
    {
        public string Title;
        public string Layout;

        public Request Request;
        public Response Response;

        Stream Stream;
        Dictionary<string, Stream> SectionStreams;

        StreamWriter Writer;
        StreamWriter RawWriter;
        StreamWriter DefaultWriter;

        public AHtmlPageState(Stream stream)
        {
            SectionStreams = new Dictionary<string, Stream>();

            Stream = stream;
            Writer = new StreamWriter(stream, leaveOpen: true);
            Writer.NewLine = "\r\n";

            DefaultWriter = Writer;
        }

        public void Write(object obj)
        {
            Writer.Write(obj);
        }

        public void WriteLine(object obj)
        {
            Writer.WriteLine(obj);
        }

        public void Section(string name)
        {
            if (name == null)
            {
                Writer.Flush();
                Writer = DefaultWriter;
                return;
            }

            SectionStreams[name] = new MemoryStream();
            Writer = new StreamWriter(SectionStreams[name], leaveOpen: true);
            Writer.NewLine = "\r\n";
        }

        public void WriteSection(string name)
        {
            Writer.Flush();

            if (SectionStreams.TryGetValue(name, out Stream str))
            {
                str.Position = 0;
                str.CopyTo(Stream);
                str.Position = str.Length;
            }
        }

        public void Clear()
        {
            Writer = new StreamWriter(Stream, leaveOpen: true);
        }

        public void Flush()
        {
            Writer.Flush();
        }
    }
}