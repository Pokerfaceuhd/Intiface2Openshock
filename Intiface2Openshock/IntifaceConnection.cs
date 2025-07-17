using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Intiface2Openshock.Utils;
using LucHeart.WebsocketLibrary;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.ModuleBase.StableInterfaces;
using OpenShock.LocalRelay.Config;
using OpenShock.LocalRelay.Utils;
using OpenShock.SDK.CSharp.Updatables;
using OpenShock.SDK.CSharp.Utils;
using OpenShock.Serialization.Gateway;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace OpenShock.LocalRelay;

public sealed class IntifaceConnection : IAsyncDisposable
{
    private readonly CancellationTokenSource _dispose;
    private CancellationTokenSource _linked;
    private CancellationTokenSource? _currentConnectionClose = null;
    private readonly ILogger<IntifaceConnection> _logger;
    private readonly IModuleConfig<Intiface2OpenshockConfig> _config;
    private readonly Uri _intifaceUri;
    private ClientWebSocket? _clientWebSocket = null;
    private DateTimeOffset _connectedAt = DateTimeOffset.MinValue;
    
    public event Func<byte[], Task<byte[]?>>? HandleMessage;
    
    public IntifaceConnection(
        Uri intifaceUri,
        ILogger<IntifaceConnection> logger,
        IModuleConfig<Intiface2OpenshockConfig> config)
    {
        _intifaceUri = intifaceUri;
        _logger = logger;
        _config = config;
        
        _dispose = new CancellationTokenSource();
        _linked = CancellationTokenSource.CreateLinkedTokenSource(_dispose.Token);
    }
    
    private readonly AsyncUpdatableVariable<WebsocketConnectionState> _state =
        new(WebsocketConnectionState.Disconnected);

    public IAsyncUpdatable<WebsocketConnectionState> State => _state;

    public struct Disposed;

    public struct Reconnecting;
    
    public Task InitializeAsync() => ConnectAsync();
    

    private async Task<OneOf<Success, Disposed, Reconnecting>> ConnectAsync()
    {
        if (_dispose.IsCancellationRequested)
        {
            _logger.LogWarning("Dispose requested, not connecting");
            return new Disposed();
        }
        
        _logger.LogDebug("Connecting to intiface");

        _state.Value = WebsocketConnectionState.Connecting;
        if (_currentConnectionClose != null) await _currentConnectionClose.CancelAsync();
        _linked.Dispose();
        _currentConnectionClose?.Dispose();

        _currentConnectionClose = new CancellationTokenSource();
        _linked = CancellationTokenSource.CreateLinkedTokenSource(_dispose.Token, _currentConnectionClose.Token);

        _clientWebSocket?.Abort();
        _clientWebSocket?.Dispose();
        
        _clientWebSocket = new ClientWebSocket();
        
        _logger.LogInformation("Connecting to websocket....");
        try
        {
            await _clientWebSocket.ConnectAsync(_intifaceUri, _linked.Token);

            _logger.LogInformation("Connected to websocket");
            _state.Value = WebsocketConnectionState.Connected;
            _connectedAt = DateTimeOffset.UtcNow;

            String json = JsonSerializer.Serialize(_config.Config.IntifaceConnection);
            SendUtf8(json);
            
            OsTask.Run(ReceiveLoop, _linked.Token);

            return new Success();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while connecting, retrying in 3 seconds");
        }

        await Reconnect();
        return new Reconnecting();
    }
    
    private async Task Reconnect()
    {
        _logger.LogWarning("Reconnecting in 3 seconds");
        
        _state.Value = WebsocketConnectionState.Connecting;
        _clientWebSocket?.Abort();
        _clientWebSocket?.Dispose();
        await Task.Delay(3000, _dispose.Token);
        OsTask.Run(ConnectAsync, _dispose.Token);
    }

    private async Task SendUtf8(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await _clientWebSocket!.SendAsync(buffer, WebSocketMessageType.Text, true, _linked.Token);
    }
    
    private async Task ReceiveLoop()
    {
        while (!_linked.Token.IsCancellationRequested)
        {
            try
            {
                if (_clientWebSocket!.State == WebSocketState.Aborted)
                {
                    _logger.LogWarning("Websocket connection aborted, closing loop");
                    break;
                }
                
                var buffer = new byte[1024];
                await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _linked.Token);
                
                lastMessage = DateTime.UtcNow;
                
                var returnMessage = await HandleMessage!(buffer);
                if (returnMessage != null)
                    await _clientWebSocket.SendAsync(returnMessage, WebSocketMessageType.Text, true, _linked.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebSocket connection terminated due to close or shutdown");
                break;
            }
            catch (WebSocketException e)
            {
                if (e.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely)
                    _logger.LogError(e, "Error in receive loop, websocket exception");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while processing websocket request");
            }
        }

        await _currentConnectionClose!.CancelAsync();

        if (_dispose.IsCancellationRequested)
        {
            _logger.LogDebug("Dispose requested, not reconnecting");
            return;
        }

        _logger.LogWarning("Lost websocket connection, trying to reconnect in 3 seconds");
        _state.Value = WebsocketConnectionState.Connecting;

        _clientWebSocket?.Abort();
        _clientWebSocket?.Dispose();

        await Task.Delay(3000, _dispose.Token);

        OsTask.Run(ConnectAsync, _dispose.Token);
    }
    
    DateTime lastMessage = DateTime.UtcNow;

    public event Func<Task>? OnDispose;

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _dispose.CancelAsync();
        await OnDispose.Raise();
        _clientWebSocket?.Dispose();
    }
}