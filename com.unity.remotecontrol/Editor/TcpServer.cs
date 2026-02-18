using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.RemoteControl.Editor
{
    public class TcpServer : IDisposable
    {
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<int, ClientHandler> _clients = new ConcurrentDictionary<int, ClientHandler>();
        private int _clientIdCounter;
        private bool _disposed;

        public int Port { get; private set; }
        public bool IsRunning => _listener != null;
        public int ClientCount => _clients.Count;

        public event Action<string> OnLog;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;

        public CommandRegistry CommandRegistry { get; set; }

        public void Start(int port)
        {
            if (_listener != null)
            {
                Log("Server already running");
                return;
            }

            Port = port;
            _cts = new CancellationTokenSource();

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();
                Log($"Server started on port {port}");

                Task.Run(() => AcceptClientsAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                Log($"Failed to start server: {ex.Message}");
                _listener = null;
                _cts?.Dispose();
                _cts = null;
                throw;
            }
        }

        public void Stop()
        {
            if (_listener == null)
                return;

            Log("Stopping server...");

            _cts?.Cancel();

            foreach (var client in _clients.Values)
            {
                client.Dispose();
            }
            _clients.Clear();

            try
            {
                _listener.Stop();
            }
            catch { }

            _listener = null;
            _cts?.Dispose();
            _cts = null;

            Log("Server stopped");
        }

        private async Task AcceptClientsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    var clientId = Interlocked.Increment(ref _clientIdCounter);

                    var handler = new ClientHandler(clientId, tcpClient, this);
                    _clients[clientId] = handler;

                    Log($"Client {clientId} connected from {tcpClient.Client.RemoteEndPoint}");
                    MainThreadDispatcher.Enqueue(() => OnClientConnected?.Invoke(clientId));

                    _ = Task.Run(() => handler.ProcessAsync(ct));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        Log($"Accept error: {ex.Message}");
                }
            }
        }

        internal void RemoveClient(int clientId)
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                client.Dispose();
                Log($"Client {clientId} disconnected");
                MainThreadDispatcher.Enqueue(() => OnClientDisconnected?.Invoke(clientId));
            }
        }

        internal void Log(string message)
        {
            var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
            MainThreadDispatcher.Enqueue(() => OnLog?.Invoke(timestamped));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
        }
    }
}
