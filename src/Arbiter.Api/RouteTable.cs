using System.Reflection;
using Arbiter.Api.Attributes;
using Arbiter.Core.Enums;

namespace Arbiter.Api;

internal sealed class RouteTable
{
    private readonly List<Route> _routes = [];
    private readonly Dictionary<Method, List<Route>> _routesByMethod = [];

    public void Add(Route route)
    {
        _routes.Add(route);

        if (!_routesByMethod.TryGetValue(route.Method, out var list))
        {
            list = [];
            _routesByMethod[route.Method] = list;
        }

        list.Add(route);
    }

    public (Route Route, Dictionary<string, string> Parameters)? Match(Method method, string path)
    {
        foreach (var route in _routes)
        {
            var match = route.Match(method, path);
            if (match is not null)
                return (route, match);
        }

        return null;
    }

    public string? GetAllowedMethods(string path)
    {
        var methods = _routes
            .Where(r => r.Match(r.Method, path) is not null)
            .Select(r => r.Method.ToString().ToUpperInvariant())
            .Distinct()
            .ToList();

        return methods.Count > 0 ? string.Join(", ", methods) : null;
    }

    public static RouteTable BuildFromTypes(IEnumerable<Type> types)
    {
        var table = new RouteTable();

        foreach (var type in types)
        {
            var routePrefix = GetRoutePrefix(type);

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var attr = method.GetCustomAttribute<HttpMethodAttribute>();
                if (attr is null)
                    continue;

                var template = CombineTemplate(routePrefix, attr.Template);
                table.Add(new Route(attr.Method, template, type, method));
            }
        }

        table.SortRoutes();
        table.ValidateRoutes();

        return table;
    }

    private void SortRoutes()
    {
        _routes.Sort((a, b) => {
            for (var i = 0; i < Math.Min(a.PatternSegments.Length, b.PatternSegments.Length); i++)
            {
                var aIsParam = a.PatternSegments[i].StartsWith('{');
                var bIsParam = b.PatternSegments[i].StartsWith('{');

                if (aIsParam != bIsParam)
                    return aIsParam ? 1 : -1;
            }

            return a.PatternSegments.Length - b.PatternSegments.Length;
        });

        foreach (var list in _routesByMethod.Values)
        {
            list.Sort((a, b) => {
                for (var i = 0; i < Math.Min(a.PatternSegments.Length, b.PatternSegments.Length); i++)
                {
                    var aIsParam = a.PatternSegments[i].StartsWith('{');
                    var bIsParam = b.PatternSegments[i].StartsWith('{');

                    if (aIsParam != bIsParam)
                        return aIsParam ? 1 : -1;
                }

                return a.PatternSegments.Length - b.PatternSegments.Length;
            });
        }
    }

    private void ValidateRoutes()
    {
        foreach (var (method, routes) in _routesByMethod)
        {
            for (var i = 0; i < routes.Count; i++)
            {
                for (var j = i + 1; j < routes.Count; j++)
                {
                    var a = routes[i];
                    var b = routes[j];

                    if (AreAmbiguous(a, b))
                    {
                        throw new InvalidOperationException(
                            $"Ambiguous routes detected for {method}: '{string.Join("/", a.PatternSegments)}' and '{string.Join("/", b.PatternSegments)}'. " +
                            "Consider reordering or using more specific constraints.");
                    }
                }
            }
        }
    }

    private static bool AreAmbiguous(Route a, Route b)
    {
        if (a.PatternSegments.Length != b.PatternSegments.Length)
            return false;

        var aHasCatchAll = a.PatternSegments.Any(p => p.StartsWith("{**"));
        var bHasCatchAll = b.PatternSegments.Any(p => p.StartsWith("{**"));

        if (aHasCatchAll && bHasCatchAll)
            return true;

        for (var i = 0; i < a.PatternSegments.Length; i++)
        {
            var segA = a.PatternSegments[i];
            var segB = b.PatternSegments[i];

            var aIsParam = segA.StartsWith('{') && segA.EndsWith('}');
            var bIsParam = segB.StartsWith('{') && segB.EndsWith('}');
            var aIsCatchAllSegment = segA.StartsWith("{**");
            var bIsCatchAllSegment = segB.StartsWith("{**");

            if (aIsCatchAllSegment != bIsCatchAllSegment)
                continue;

            if (aIsParam != bIsParam)
                return false;

            if (aIsParam)
            {
                var paramNameA = GetParamName(segA);
                var paramNameB = GetParamName(segB);
                if (paramNameA != paramNameB)
                    continue;
            }
            else if (!segA.Equals(segB, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetParamName(string segment)
    {
        if (segment.StartsWith("{**"))
            return segment[3..];

        var inner = segment[1..^1];
        var colon = inner.IndexOf(':');
        return colon >= 0 ? inner[..colon] : inner;
    }

    private static string? GetRoutePrefix(Type type)
    {
        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        if (routeAttr is null)
            return null;

        var template = routeAttr.Template;

        if (template.Contains("[controller]", StringComparison.OrdinalIgnoreCase))
        {
            var controllerName = type.Name;
            if (controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                controllerName = controllerName[..^"Controller".Length];

            template = template.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);
        }

        return template;
    }

    private static string CombineTemplate(string? prefix, string template)
    {
        if (prefix is null)
            return template.TrimStart('/');

        template = template.TrimStart('/');

        return string.IsNullOrEmpty(template) ? prefix : $"{prefix}/{template}";
    }
}