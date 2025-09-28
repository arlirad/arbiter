namespace Arbiter.Models.Config.Workers;

public class AcmeWorkerConfigModel
{
    public Uri? AcmeDirectoryUrl { get; set; }
    public string? AccountName { get; set; }
    public bool? TosAccepted { get; set; }
}