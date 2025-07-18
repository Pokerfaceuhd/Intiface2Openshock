using OpenShock.Desktop.ModuleBase.Models;

namespace Intiface2Openshock.Config;

public sealed class ShockerConfig
{
    public List<Guid> Shockers { get; set; } = new List<Guid>();
    
    public ControlType Type { get; set; } = ControlType.Vibrate;
}