namespace Intiface2Openshock.Config;

public class ShockerConnectionConfig
{
    public SerialConfig Serial { get; set; } = new();
    public ShockerConnectionType Type { get; set; } = ShockerConnectionType.LiveControl;
}

public enum ShockerConnectionType : byte
{
    Serial = 0,
    LiveControl = 1
}