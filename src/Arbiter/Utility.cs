using System.Collections.Concurrent;
using System.Net;

namespace Arbiter;

public class Utility
{
    private static ConcurrentDictionary<EndPoint, ConcurrentDictionary<string, DateTime>> _rateLimits = new ConcurrentDictionary<EndPoint, ConcurrentDictionary<string, DateTime>>(); // i wanna die
    private static object _rateLimitsLock = new object();

    public static bool RateLimit(string identifier, TimeSpan timeout, Request request)
    {
        var epLimits = _rateLimits.GetOrAdd(request.EndPoint, (ep) =>
        {
            return new ConcurrentDictionary<string, DateTime>();
        });

        lock (_rateLimitsLock)
        {
            if (!epLimits.ContainsKey(identifier))
            {
                epLimits[identifier] = DateTime.Now + timeout;
                return false;
            }

            if (DateTime.Now <= epLimits[identifier])
                return true;

            epLimits[identifier] = DateTime.Now + timeout;
            return false;
        }
    }
}