using System.Net;
using System.Reactive.Linq;
using System.Text;
using LucHeart.WebsocketLibrary;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Config;
using Intiface2Openshock.Config;
using Intiface2Openshock.Models.Serial;
using Intiface2Openshock.Utils;
using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Updatables;
using OpenShock.Serialization.Types;
using ShockerModelType = OpenShock.Serialization.Types.ShockerModelType;
using System.Reactive.Subjects;

namespace Intiface2Openshock.Services;

public sealed class FlowManager
{
    private readonly IModuleConfig<Intiface2OpenshockConfig> _config;
    private readonly ILogger<FlowManager> _logger;
    private readonly ILogger<IntifaceConnection> _intifaceConnectionLogger;
    private readonly ILogger<SerialPortClient> _serialPortClientLogger;
    private readonly IOpenShockService _openShockService;

    public byte LiveControlIntensity;
    private readonly Subject<byte> _liveControlUpdate = new();
    public IAsyncMinimalEventObservable OnLiveControlUpdate => _onLiveControlUpdate;
    private readonly AsyncMinimalEvent _onLiveControlUpdate = new();
    
    
    public IntifaceConnection? IntifaceConnection { get; private set; } = null;
    public SerialPortClient? SerialPortClient { get; private set; } = null;

    private IntifaceProtocolType _usedProtocolType;
    
    private readonly AsyncUpdatableVariable<WebsocketConnectionState> _state =
        new(WebsocketConnectionState.Disconnected);
    public IAsyncUpdatable<WebsocketConnectionState> State => _state;

    public FlowManager(
        IModuleConfig<Intiface2OpenshockConfig> config,
        ILogger<FlowManager> logger,
        ILogger<IntifaceConnection> intifaceConnectionLogger,
        ILogger<SerialPortClient> serialPortClientLogger,
        IOpenShockService openShockService)
    {
        _config = config;
        _logger = logger;
        _intifaceConnectionLogger = intifaceConnectionLogger;
        _serialPortClientLogger = serialPortClientLogger;
        _openShockService = openShockService;
    }

    public async Task LoadConfigAndStart()
    {
        _liveControlUpdate.Throttle(TimeSpan.FromMilliseconds(20)).Subscribe(u =>
        {
            OsTask.Run(() => _onLiveControlUpdate.InvokeAsyncParallel());
        });
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        OsTask.Run(LiveControlLoop);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        await StartIntifaceConnection();
        
        var serialConfig = _config.Config.ShockerConnection.Serial;
        
        if (serialConfig.AutoConnect)
        {
            await ConnectSerialPort(serialConfig.Port);
        }
    }
    
    private async Task<bool> StopIntifaceConnection()
    {
        if (IntifaceConnection == null) return false;
        await IntifaceConnection.DisposeAsync();
        IntifaceConnection = null;
        _state.Value = WebsocketConnectionState.Disconnected;
        return true;
    }

    private async Task StartIntifaceConnection()
    {
        await StopIntifaceConnection();
        
        _usedProtocolType = _config.Config.IntifaceConnection.ProtocolType;
        
        IntifaceConnection =
            new IntifaceConnection(new Uri($"ws://{IPAddress.Loopback.ToString()}:{_config.Config.IntifaceConnection.Port}"), _intifaceConnectionLogger, _config);
        IntifaceConnection.HandleMessage += OnProtocolMessage;
        await IntifaceConnection.State.Updated.SubscribeAsync(state =>
        {
            _state.Value = state;
            LiveControlIntensity = 0;
            return Task.CompletedTask;
        }).ConfigureAwait(false);
        
        await IntifaceConnection.InitializeAsync().ConfigureAwait(false);
    }

    private async Task LiveControlLoop()
    {
        while (true)
        {
            if (LiveControlIntensity != 0)
            {
                switch (_config.Config.ShockerConnection.Type)
                {
                    case ShockerConnectionType.LiveControl:
                        _openShockService.Control.LiveControl(_config.Config.Shocker.Shockers, LiveControlIntensity,
                            _config.Config.Shocker.Type);
                        await Task.Delay(40);
                        break;
                    case ShockerConnectionType.Serial:
                        if (SerialPortClient == null) return;
                        Task.WaitAll(
                            _openShockService.Data.Hubs.Value.SelectMany(hub => hub.Shockers)
                                .TakeWhile(shocker => _config.Config.Shocker.Shockers.Contains(shocker.Id))
                                .Select(shocker => SerialPortClient.Control(new RfTransmit
                                {
                                    Id = shocker.RfId,
                                    Intensity = LiveControlIntensity,
                                    Model = (ShockerModelType)(byte)shocker.Model,
                                    Type = (ShockerCommandType)(byte)_config.Config.Shocker.Type,
                                    DurationMs = 150
                                })));
                        await Task.Delay(130);
                        break;
                }
            }
        }
    }
    
    private async Task<byte[]?> OnProtocolMessage(byte[] data)
    {
        var protocolOutput = _usedProtocolType switch
        {
            IntifaceProtocolType.Lovense => await LovenseProtocol(data),
            _ => null
        };
        _liveControlUpdate.OnNext(0);
        return protocolOutput;
    }
    
    private async Task<byte[]?> LovenseProtocol(byte[] buffer)
    {
        string message = Encoding.UTF8.GetString(buffer);
        
        if (message.StartsWith("Vibrate:"))
        {
            LiveControlIntensity = (byte)(5 * message
                .Where(char.IsDigit)
                .Aggregate(0, (total, c) => total * 10 + (c - '0')));

        }
        else if (message.StartsWith("DeviceType;"))
        {
            return Encoding.UTF8.GetBytes($"Z:{_config.Config.IntifaceConnection.StartupMessage.address}:10");
        }
        else if (message.StartsWith("Battery"))
        {
            return Encoding.UTF8.GetBytes($"90;");
        }
        return null;
    }
    
    private IAsyncDisposable? _onConsoleBufferUpdateDisposable = null;

    public async Task ConnectSerialPort(string? portName)
    {
        if (SerialPortClient != null)
        {
            if(_onConsoleBufferUpdateDisposable != null) await _onConsoleBufferUpdateDisposable.DisposeAsync();
            await SerialPortClient.DisposeAsync();
            SerialPortClient = null;
        }
        
        if(string.IsNullOrWhiteSpace(portName)) return;
        
        SerialPortClient = new SerialPortClient(_serialPortClientLogger, portName);
        await SerialPortClient.Open();
    }
}