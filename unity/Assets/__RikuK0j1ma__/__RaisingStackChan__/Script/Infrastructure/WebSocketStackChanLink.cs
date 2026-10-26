using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>
    /// IStackChanLink over System.Net.WebSockets.ClientWebSocket.
    /// Endpoint resolution order: QR payload, mDNS (stackchan.local), last used endpoint.
    /// Reconnects forever with exponential backoff until disposed.
    /// </summary>
    public sealed class WebSocketStackChanLink : IStackChanLink, IDisposable
    {
        private const int ConnectTimeoutMs = 5000;
        private const int ReceiveBufferSize = 4096;

        private readonly PlayerPrefsEndpointStore _store;
        private readonly ReconnectBackoff _backoff = new ReconnectBackoff();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private StackChanEndpoint? _qrEndpoint;
        private ClientWebSocket _socket;
        private UniTaskCompletionSource _firstConnected;
        private bool _loopStarted;
        private StackChanConnectionState _state = StackChanConnectionState.Disconnected;

        public WebSocketStackChanLink() : this(new PlayerPrefsEndpointStore()) { }

        public WebSocketStackChanLink(PlayerPrefsEndpointStore store)
        {
            _store = store;
        }

        public bool IsConnected => _state == StackChanConnectionState.Connected
                                   && _socket != null && _socket.State == WebSocketState.Open;

        public StackChanConnectionState State => _state;
        public string LastError { get; private set; }

        public event Action<StackChanConnectionState> StateChanged;

        public bool TrySetEndpointFromQr(string payload)
        {
            if (!StackChanEndpoint.TryParse(payload, out var ep)) return false;
            _qrEndpoint = ep;
            return true;
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            if (!_loopStarted)
            {
                _loopStarted = true;
                _firstConnected = new UniTaskCompletionSource();
                RunAsync(_lifetime.Token).Forget();
            }

            if (IsConnected) return;
            await _firstConnected.Task.AttachExternalCancellation(ct);
        }

        public UniTask SendEmotionAsync(StackChanEmotion emotion, int durationMs, CancellationToken ct)
            => SendTextAsync(StackChanMessages.Emotion(emotion, durationMs), ct);

        public UniTask SendShowQrAsync(CancellationToken ct)
            => SendTextAsync(StackChanMessages.ShowQr(), ct);

        public UniTask SendLookAsync(float yawDeg, float pitchDeg, CancellationToken ct)
            => SendTextAsync(StackChanMessages.Look(yawDeg, pitchDeg), ct);

        public void Dispose()
        {
            _lifetime.Cancel();
            try { _socket?.Dispose(); } catch (Exception) { /* ignore */ }
            _lifetime.Dispose();
            _sendLock.Dispose();
        }

        private async UniTask SendTextAsync(string text, CancellationToken ct)
        {
            var socket = _socket;
            if (!IsConnected) throw new InvalidOperationException("StackChan is not connected.");

            var bytes = Encoding.UTF8.GetBytes(text);
            await _sendLock.WaitAsync(ct);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    SetState(_backoff.Attempt == 0 && !_firstConnected.Task.Status.IsCompleted()
                        ? StackChanConnectionState.Connecting
                        : StackChanConnectionState.Reconnecting);

                    var socket = await TryConnectAnyAsync(ct);
                    if (socket != null)
                    {
                        _socket = socket;
                        _backoff.Reset();
                        LastError = null;
                        SetState(StackChanConnectionState.Connected);
                        _firstConnected.TrySetResult();

                        await ReceiveLoopAsync(socket, ct);

                        _socket = null;
                        socket.Dispose();
                    }

                    SetState(StackChanConnectionState.Reconnecting);
                    await UniTask.Delay(_backoff.Next(), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed.
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogException(e);
            }
            finally
            {
                if (!_lifetime.IsCancellationRequested) SetState(StackChanConnectionState.Disconnected);
            }
        }

        private async UniTask<ClientWebSocket> TryConnectAnyAsync(CancellationToken ct)
        {
            foreach (var candidate in BuildCandidates())
            {
                ct.ThrowIfCancellationRequested();
                var socket = new ClientWebSocket();
                try
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        timeout.CancelAfterSlim(ConnectTimeoutMs);
                        await socket.ConnectAsync(candidate.ToUri(), timeout.Token);
                    }

                    if (!candidate.IsMdns) _store.Save(candidate);
                    return socket;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    socket.Dispose();
                    LastError = Hint("timeout", candidate);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    socket.Dispose();
                    LastError = Hint(e.Message, candidate);
                }
                catch (OperationCanceledException)
                {
                    socket.Dispose();
                    throw;
                }
            }

            return null;
        }

        private static string Hint(string detail, StackChanEndpoint ep) =>
            $"Cannot reach StackChan at {ep.Host}:{ep.Port} ({detail}). " +
            "Check that both devices are on the same Wi-Fi and that it does not block device-to-device traffic (guest networks often do).";

        private List<StackChanEndpoint> BuildCandidates()
        {
            var list = new List<StackChanEndpoint>(3);
            var hasLast = _store.TryLoad(out var last);

            if (_qrEndpoint.HasValue) AddUnique(list, _qrEndpoint.Value);

            // mDNS: reuse port/token from the QR or the last endpoint if known.
            if (_qrEndpoint.HasValue) AddUnique(list, _qrEndpoint.Value.WithMdnsHost());
            else if (hasLast) AddUnique(list, last.WithMdnsHost());
            else AddUnique(list, new StackChanEndpoint(StackChanEndpoint.MdnsHost, StackChanEndpoint.DefaultPort, string.Empty));

            if (hasLast) AddUnique(list, last);
            return list;
        }

        private static void AddUnique(List<StackChanEndpoint> list, StackChanEndpoint ep)
        {
            if (!list.Contains(ep)) list.Add(ep);
        }

        private static async UniTask ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[ReceiveBufferSize];
            var sb = new StringBuilder();
            try
            {
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    if (result.MessageType != WebSocketMessageType.Text) continue;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage) continue;

                    var json = sb.ToString();
                    sb.Clear();
                    var type = StackChanMessages.TryGetType(json, out _);
                    if (type == "hello") Debug.Log("[StackChanLink] " + json);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StackChanLink] receive ended: " + e.Message);
            }
        }

        private void SetState(StackChanConnectionState state)
        {
            if (_state == state) return;
            _state = state;
            StateChanged?.Invoke(state);
        }
    }
}
