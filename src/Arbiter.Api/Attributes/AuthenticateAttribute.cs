namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AuthenticateAttribute(string authenticatorName = "master-key") : Attribute
{
    public string AuthenticatorName
    {
        get;
    } = authenticatorName;
}