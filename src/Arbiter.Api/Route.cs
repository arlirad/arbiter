using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Http;
using Arbiter.Api.Results;
using Arbiter.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Api;

internal sealed class Route
{
    private static readonly Dictionary<string, Func<string, bool>> Constraints = new(StringComparer.OrdinalIgnoreCase) {
        ["guid"] = v => Guid.TryParse(v, out _),
        ["int"] = v => int.TryParse(v, out _),
        ["long"] = v => long.TryParse(v, out _),
        ["bool"] = v => bool.TryParse(v, out _),
        ["alpha"] = v => v.All(char.IsLetter),
    };

    public Route(Method method, string pattern, Type controllerType, MethodInfo methodInfo)
    {
        var rawSegments = pattern.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<string>();

        for (var i = 0; i < rawSegments.Length; i++)
        {
            var seg = rawSegments[i];
            if (seg.StartsWith('{') && seg.EndsWith('}'))
            {
                var inner = seg[1..^1];
                if (inner.StartsWith("**"))
                {
                    var paramName = inner[2..];
                    segments.Add("{**" + paramName + "}");
                }
                else if (inner.EndsWith("?"))
                {
                    OptionalParamIndices.Add(i);
                    inner = inner[..^1];
                    seg = "{" + inner + "}";
                    segments.Add(seg);
                }
                else
                {
                    segments.Add(seg);
                }
            }
            else
            {
                segments.Add(seg);
            }
        }

        Method = method;
        PatternSegments = [.. segments];
        MinSegmentCount = PatternSegments.Length - OptionalParamIndices.Count;
        ControllerType = controllerType;
        ActionMethod = methodInfo;
    }

    public Method Method
    {
        get;
    }
    public string[] PatternSegments
    {
        get;
    }
    public HashSet<int> OptionalParamIndices
    {
        get;
    } = [];
    public int MinSegmentCount
    {
        get;
    }
    public Type ControllerType
    {
        get;
    }
    public MethodInfo ActionMethod
    {
        get;
    }

    public Dictionary<string, string>? Match(Method method, string path)
    {
        if (method != Method)
            return null;

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        var hasCatchAll = PatternSegments.Any(p => p.StartsWith("{**"));

        if (hasCatchAll)
        {
            if (segments.Length < MinSegmentCount)
                return null;
        }
        else if (segments.Length < MinSegmentCount || segments.Length > PatternSegments.Length)
        {
            return null;
        }

        var parameters = new Dictionary<string, string>();

        for (var i = 0; i < PatternSegments.Length; i++)
        {
            var patternSeg = PatternSegments[i];

            if (patternSeg.StartsWith("{**"))
            {
                var paramName = patternSeg[3..^1];
                var remaining = string.Join("/", segments.Skip(i).Select(Uri.UnescapeDataString));
                parameters[paramName] = remaining;
                return parameters;
            }

            if (patternSeg.StartsWith('{') && patternSeg.EndsWith('}'))
            {
                if (i >= segments.Length)
                {
                    if (OptionalParamIndices.Contains(i))
                        continue;
                    return null;
                }

                var inner = patternSeg[1..^1];
                var colonIndex = inner.IndexOf(':');
                var paramName = colonIndex >= 0 ? inner[..colonIndex] : inner;
                var constraintName = colonIndex >= 0 ? inner[(colonIndex + 1)..] : null;

                var value = Uri.UnescapeDataString(segments[i]);

                if (constraintName is not null && Constraints.TryGetValue(constraintName, out var constraint))
                {
                    if (!constraint(value))
                        return null;
                }

                parameters[paramName] = value;
            }
            else if (!patternSeg.Equals(segments[i], StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return parameters;
    }

    public async Task Invoke(IApiController controller, HttpContext httpContext, Dictionary<string, string> routeParameters)
    {
        httpContext.Request.RouteValues = new RouteValueDictionary(routeParameters);

        if (controller is ControllerBase controllerBase)
            controllerBase.HttpContext = httpContext;

        var authAttr = ActionMethod.GetCustomAttribute<AuthenticateAttribute>()
            ?? ControllerType.GetCustomAttribute<AuthenticateAttribute>();
        if (authAttr is not null)
        {
            var authenticator = httpContext.RequestServices.GetKeyedService<IAuthenticator>(authAttr.AuthenticatorName)
                ?? httpContext.RequestServices.GetService<IAuthenticator>();
            if (authenticator is null)
            {
                httpContext.Response.StatusCode = (int)Status.InternalServerError;
                return;
            }

            var authHeader = httpContext.Request.Headers.Authorization;
            var token = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                ? authHeader["Bearer ".Length..].Trim()
                : null;

            var authResult = await authenticator.AuthenticateAsync(token, httpContext.CancellationToken);
            if (!authResult.IsAuthenticated)
            {
                httpContext.Response.StatusCode = (int)Status.Unauthorized;
                return;
            }

            httpContext.AuthInfo = authResult;
        }

        var args = await ParameterBinder.BindAsync(ActionMethod, httpContext, routeParameters);

        var modelState = ValidateParameters(args, ActionMethod);

        if (controller is ControllerBase cb)
        {
            cb.SetModelState(modelState);

            if (!modelState.IsValid)
            {
                httpContext.Response.StatusCode = 400;
                var problem = new ValidationProblemDetails(modelState);
                await new BadRequestObjectResult(problem).ExecuteAsync(httpContext);
                return;
            }
        }

        var result = ActionMethod.Invoke(controller, args);

        switch (result)
        {
            case Task task:
                {
                    await task.ConfigureAwait(false);

                    var taskType = task.GetType();
                    if (taskType.IsGenericType)
                    {
                        var resultValue = taskType.GetProperty("Result")?.GetValue(task);
                        if (resultValue is IActionResult actionResult)
                            await actionResult.ExecuteAsync(httpContext).ConfigureAwait(false);
                    }

                    break;
                }
            case IActionResult actionResult:
                await actionResult.ExecuteAsync(httpContext).ConfigureAwait(false);
                break;
        }
    }

    private static ModelStateDictionary ValidateParameters(object?[] args, MethodInfo actionMethod)
    {
        var modelState = new ModelStateDictionary();
        var parameters = actionMethod.GetParameters();

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var value = args[i];

            if (value is null)
            {
                var requiredAttr = param.GetCustomAttribute<RequiredAttribute>();
                if (requiredAttr is not null)
                    modelState.AddModelError(param.Name!, requiredAttr.ErrorMessage ?? ("The " + param.Name + " field is required."));

                continue;
            }

            if (param.ParameterType.IsPrimitive || param.ParameterType == typeof(string))
                continue;

            var validationContext = new ValidationContext(value);
            var results = new List<ValidationResult>();

            if (Validator.TryValidateObject(value, validationContext, results, validateAllProperties: true))
                continue;

            foreach (var vr in results)
            {
                var memberName = vr.MemberNames.FirstOrDefault() ?? param.Name;
                modelState.AddModelError(memberName!, vr.ErrorMessage ?? "Invalid value.");
            }
        }

        return modelState;
    }
}