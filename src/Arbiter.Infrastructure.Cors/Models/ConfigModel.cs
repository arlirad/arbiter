namespace Arbiter.Infrastructure.Cors.Models;

internal class ConfigModel
{
    public List<string>? AllowOrigin { get; set; }
    public List<string>? AllowMethods { get; set; }
    public List<string>? AllowHeaders { get; set; }
    public bool? AllowCredentials { get; set; }
}