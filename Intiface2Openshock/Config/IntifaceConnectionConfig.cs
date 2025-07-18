namespace Intiface2Openshock.Config;

public sealed class IntifaceConnectionConfig
{
    public IntifaceConnectionStartupMessage StartupMessage { get; set; } = new();
    public ushort Port { get; set; } = 54817;
    public IntifaceProtocolType ProtocolType { get; set; } = IntifaceProtocolType.JoyHubV1;
}

public sealed class IntifaceConnectionStartupMessage
{
    //Keep all as lowercase! Startup message is case-sensitive
    // ReSharper disable InconsistentNaming
    public string identifier { get; set; } = "Openshock";
    public string address { get; set; } = Random.Shared.Next().ToString();
    public ushort version { get; set; } = 0;
    // ReSharper enable InconsistentNaming
}

public enum IntifaceProtocolType: byte
{
    Lovense = 0,
    JoyHubV1 = 1
}