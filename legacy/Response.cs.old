using System;
using System.Globalization;

namespace Arbiter;

public class Response
{
    private static readonly Dictionary<int, string> _defaultPhrases = new Dictionary<int, string>
    {
        [0] = "Unknown",

        [100] = "Continue",
        [101] = "Switching Protocol",
        [103] = "Early Hints",
        [200] = "OK",
        [201] = "Created",
        [202] = "Accepted",
        [203] = "Non-Authoritative Information",
        [204] = "No Content",
        [205] = "Reset Content",
        [206] = "Partial Content",
        [300] = "Multiple Choice",
        [301] = "Moved Permanently",
        [302] = "Found",
        [303] = "See Other",
        [304] = "Not Modified",
        [305] = "Use Proxy",
        [307] = "Temporary Redirect",
        [308] = "Permanent Redirect",
        [400] = "Bad Request",
        [401] = "Unauthorized",
        [402] = "Payment Required",
        [403] = "Forbidden",
        [404] = "Not Found",
        [405] = "Method Not Allowed",
        [406] = "Not Acceptable",
        [407] = "Proxy Authentication Required",
        [408] = "Request Timeout",
        [409] = "Conflict",
        [410] = "Gone",
        [411] = "Length Required",
        [412] = "Precondition Failed",
        [413] = "Payload Too large",
        [414] = "URI Too Long",
        [415] = "Unsupported Media Type",
        [416] = "Range Not Satisfiable",
        [417] = "Expectation Failed",
        [418] = "I'm a teapot",
        [421] = "Misdirected Request",
        [422] = "Unprocessable Entity",
        [423] = "Locked",
        [424] = "Failed Dependency",
        [425] = "Too Early",
        [426] = "Upgrade Required",
        [428] = "Precondition Required",
        [429] = "Too Many Requests",
        [431] = "Request Header Fields Too Large",
        [451] = "Unavailable For Legal Reasons",
        [500] = "Internal Server Error",
        [501] = "Not Implemented",
        [502] = "Bad Gateway",
        [503] = "Service Unavailable",
        [504] = "Gateway Timeout",
        [505] = "HTTP Version Not Supported",
        [506] = "Variant Also Negotiates",
        [507] = "Insufficient Storage",
        [508] = "Loop Detected",
        [510] = "Not Extended",
        [511] = "Network Authentication Required",
    };

    public int Code = 500;
    public string Phrase = _defaultPhrases[500];
    public Dictionary<string, string> Headers = new Dictionary<string, string>();

    public string? Mime = null;
    public Stream? Stream = null;
    public bool SimpleResponse = false;
    public bool DontRespond = false;

    public void SetCode(int code)
    {
        string? phrase;

        Code = code;

        if (_defaultPhrases.TryGetValue(code, out phrase))
            Phrase = phrase;
        else
            Phrase = _defaultPhrases[0];
    }

    public void SimpleCode(int code)
    {
        string? phrase;

        Stream = null;
        Code = code;
        SimpleResponse = true;

        if (_defaultPhrases.TryGetValue(code, out phrase))
            Phrase = phrase;
        else
            Phrase = _defaultPhrases[0];
    }

    public void Redirect(string uri)
    {
        SetCode(302);
        Headers["Location"] = uri;
    }

    public void Proxy(Request request, System.Net.IPEndPoint ep, string uri)
    {
        var link = new Link(request, uri);
        link.Begin(ep);

        DontRespond = true;
        Stream = null;
    }

    public void SetCookie(Cookie cookie, DateTime expires = default, string? path = null, string? domain = null, bool secure = true, bool httponly = true)
    {
        if (!Headers.TryGetValue("Set-Cookie", out string cookieString))
            cookieString = "";

        cookieString += cookie.Name + "=" + cookie.Value;

        if (expires != default)
            cookieString += "; expires=" + expires.ToUniversalTime().ToString("ddd, dd-MMM-yyyy hh:mm:ss", CultureInfo.InvariantCulture) + " GMT";

        if (path != null)
            cookieString += "; path=" + path;

        if (domain != null)
            cookieString += "; domain=" + domain;

        if (secure)
            cookieString += "; secure";

        if (httponly)
            cookieString += "; httponly";

        Headers["Set-Cookie"] = cookieString;
    }
}