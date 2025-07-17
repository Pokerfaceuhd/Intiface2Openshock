using System.Net;
using System.Text;
using LucHeart.WebsocketLibrary;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Config;
using Intiface2Openshock.Config;
using Intiface2Openshock.Models.Serial;
using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Updatables;
using OpenShock.Serialization.Gateway;
using OpenShock.Serialization.Types;

namespace Intiface2Openshock.Services;

public sealed class FlowManager
{
    private readonly IModuleConfig<Intiface2OpenshockConfig> _config;
    private readonly ILogger<FlowManager> _logger;
    private readonly ILogger<IntifaceConnection> _intifaceConnectionLogger;
    private readonly ILogger<SerialPortClient> _serialPortClientLogger;
    private readonly IOpenShockService _openShockService;
    
    public IntifaceConnection? IntifaceConnection { get; private set; } = null;
    public SerialPortClient? SerialPortClient { get; private set; } = null;
    
    public IAsyncMinimalEventObservable OnConsoleBufferUpdate => _onConsoleBufferUpdate;
    private readonly AsyncMinimalEvent _onConsoleBufferUpdate = new();

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
        _usedProtocolType = _config.Config.IntifaceConnection.ProtocolType;
        
        await StartIntifaceConnection();
        
        var serialConfig = _config.Config.Serial;
        
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
        
        IntifaceConnection =
            new IntifaceConnection(new Uri($"ws://{IPAddress.Loopback.ToString()}+{_config.Config.Port}"), _intifaceConnectionLogger, _config);
        IntifaceConnection.HandleMessage += OnProtocolMessage;
        await IntifaceConnection.State.Updated.SubscribeAsync(state =>
        {
            _state.Value = state;
            if (state != WebsocketConnectionState.Connected)
            {
                SerialPortClient!.KillLiveControl();
            }
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        await IntifaceConnection.InitializeAsync().ConfigureAwait(false);
    }

    private async Task SendShockMessage(byte intensity)
    {
        switch (_config.Config.ShockerConnection.Type)
        {
            case ShockerConnectionType.LiveControl:
                _openShockService.Control.LiveControl(_config.Config.Shocker.Shockers, intensity, _config.Config.Shocker.Type);
                break;
            case ShockerConnectionType.Serial:
                if (SerialPortClient == null) return;
                var shockers = _openShockService.Data.Hubs.Value
                    .SelectMany(hub => hub.Shockers)
                    .Select(shocker => (shocker.RfId, (ShockerModelType)(byte)shocker.Model));
                SerialPortClient.LiveControl(shockers,  intensity, (ShockerCommandType)(byte)_config.Config.Shocker.Type);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    private async Task<byte[]?> OnProtocolMessage(byte[] data)
    {
        return _usedProtocolType switch
        {
            IntifaceProtocolType.Lovense => await LovenseProtocol(data),
            _ => null
        };
    }
    
    private async Task<byte[]?> LovenseProtocol(byte[] buffer)
    {
        if (SerialPortClient == null) throw new NullReferenceException("SerialPortClient is null");
        
        string message = Encoding.UTF8.GetString(buffer);
        
        if (message.StartsWith("Vibrate:"))
        {
            await SendShockMessage((byte)message
                .Where(char.IsDigit)
                .Aggregate(0, (total, c) => total * 10 + (c - '0')));
        }
        else if (message.StartsWith("DeviceType;"))
        {
            return Encoding.UTF8.GetBytes($"Z:{_config.Config.IntifaceConnection.StartupMessage.Address}:10");
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
        _onConsoleBufferUpdateDisposable = await SerialPortClient.OnConsoleBufferUpdate.SubscribeAsync(_onConsoleBufferUpdate.InvokeAsyncParallel);
        await SerialPortClient.Open();
    }
}