namespace Intiface2Openshock.Config;

public sealed class IntifaceConnectionConfig
{
    public IntifaceConnectionStartupMessage StartupMessage { get; set; } = new();
    public IntifaceProtocolType ProtocolType { get; set; } = IntifaceProtocolType.Lovense;
}

public sealed class IntifaceConnectionStartupMessage
{
    public string identifier { get; set; } = "Openshock";
    public string address { get; set; } = Random.Shared.Next().ToString();
    public ushort version { get; set; } = 0;
}

public enum IntifaceProtocolType: byte
{
    Lovense = 0
}