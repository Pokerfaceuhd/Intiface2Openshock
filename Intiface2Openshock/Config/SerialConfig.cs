﻿namespace Intiface2Openshock.Config;

public sealed class SerialConfig
{
    public bool AutoConnect { get; set; } = true;
    public string? Port { get; set; } = null;
}