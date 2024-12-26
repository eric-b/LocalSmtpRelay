namespace LocalSmtpRelay.Components.Llm
{
    public sealed record HealthStatus(string Status)
    {
        public static readonly HealthStatus ForceOk = new HealthStatus("ok");

        public bool IsOk() => Status?.Equals("ok", System.StringComparison.OrdinalIgnoreCase) == true;

        public override string ToString() => Status;
    }
}
