using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Serialization.Types;

namespace OpenShock.LocalRelay.Config;

public sealed class ShockerConfig
{
    public List<Guid> Shockers { get; set; } = new List<Guid>();
    
    public ControlType Type { get; set; } = ControlType.Vibrate;
    public ushort MaxShockLength { get; set; } = ushort.MaxValue;
}