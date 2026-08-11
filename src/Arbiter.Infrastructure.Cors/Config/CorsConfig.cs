namespace Arbiter.Infrastructure.Cors.Config;

public sealed record CorsConfig
{
    public List<string>? AllowOrigin
    {
        get; init;
    }
    public List<string>? AllowMethods
    {
        get; init;
    }
    public List<string>? AllowHeaders
    {
        get; init;
    }
    public bool? AllowCredentials
    {
        get; init;
    }
}
