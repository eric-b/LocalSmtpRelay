namespace LocalSmtpRelay.Components.AlertManager
{
    public sealed record Status(ClusterStatus Cluster, VersionInfo VersionInfo)
    {
        public bool IsReady() => Cluster?.Status?.Equals("ready", System.StringComparison.OrdinalIgnoreCase) == true;
    };
    public sealed record ClusterStatus(string Name, string Status);
    public sealed record VersionInfo(string Version, string Revision, string Branch);
}
