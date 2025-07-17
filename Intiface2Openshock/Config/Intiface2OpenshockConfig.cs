namespace Intiface2Openshock.Config;

public sealed class Intiface2OpenshockConfig
{
    public ShockerConfig Shocker { get; set; } = new();
    public ushort Port { get; set; } = 54817;
    public SerialConfig Serial { get; set; } = new();
    public ShockerConnectionConfig ShockerConnection { get; set; } = new();
    public IntifaceConnectionConfig IntifaceConnection { get; set; } = new();
}