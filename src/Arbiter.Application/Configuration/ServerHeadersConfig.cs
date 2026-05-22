namespace Arbiter.Application.Configuration;

public class ServerHeadersConfig
{
    public bool Server
    {
        get;
        init;
    } = true;
    public bool Date
    {
        get;
        init;
    } = true;
    public bool RequestId
    {
        get;
        init;
    } = true;
    public StrictTransportSecurityConfig? StrictTransportSecurity
    {
        get;
        init;
    }
}
