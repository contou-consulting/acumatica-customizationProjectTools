namespace AcuPackageTools;

public sealed class AcuClientOptions
{
    public string Url { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Tenant { get; set; }
    public bool SkipCertificateCheck { get; set; }
}
