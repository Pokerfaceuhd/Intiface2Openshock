using OpenShock.Serialization.Types;

namespace OpenShock.LocalRelay.Models.Serial;

public sealed class LiveControlShocker
{
    public required ushort Id { get; set; }
    public required ShockerModelType Model { get; set; }
    public required ShockerCommandType Type { get; set; }
    public required byte Intensity { get; set; }
}