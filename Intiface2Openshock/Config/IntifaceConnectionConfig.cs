namespace OpenShock.LocalRelay.Config;

public sealed class IntifaceConnectionConfig
{
    public IntifaceConnectionStartupMessage StartupMessage { get; set; } = new();
    public IntifaceProtocolType ProtocolType { get; set; } = IntifaceProtocolType.Lovense;
}

public sealed class IntifaceConnectionStartupMessage
{
    public string Identifier { get; set; } = "Openshock";
    public string Address { get; set; } = $"Openshocki{Random.Shared.Next().ToString()}";
    public ushort Version { get; set; } = 0;
}

public enum IntifaceProtocolType: byte
{
    Lovense = 0
}