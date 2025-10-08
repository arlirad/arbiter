namespace Arbiter.Infrastructure.Acme.Models;

internal class ConfigModel
{
    public Uri? AcmeDirectoryUrl { get; set; }
    public string? AccountName { get; set; }
    public bool? TosAccepted { get; set; }
}