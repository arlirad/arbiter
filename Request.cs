using System;
using System.Net;

namespace Arbiter {
    public class Request {
        public string Method;
        public Uri Uri;
        public Uri RewrittenUri;
        public string Version;

        public Dictionary<string, string> Headers    = new Dictionary<string, string>();
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();

        public Site Site;
        public ClampedStream Stream;
        public Stream SocketStream;

        public EndPoint EndPoint;
    }
}