using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UnityRemote;

public class TcpClientWrapper : IDisposable
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeout;
    private bool _disposed;

    public bool IsConnected => _client?.Connected == true;

    public TcpClientWrapper(string host, int port, int timeout = 5000)
    {
        _host = host;
        _port = port;
        _timeout = timeout;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _client = new TcpClient();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            await _client.ConnectAsync(_host, _port, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Connection to {_host}:{_port} timed out");
        }

        var stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public async Task<JsonNode?> SendCommandAsync(string command, Dictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        if (_writer == null || _reader == null)
            throw new InvalidOperationException("Not connected");

        var request = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["command"] = command
        };

        if (parameters != null)
        {
            var paramsObj = new JsonObject();
            foreach (var kvp in parameters)
            {
                paramsObj[kvp.Key] = JsonValue.Create(kvp.Value);
            }
            request["params"] = paramsObj;
        }

        var json = request.ToJsonString();
        await _writer.WriteLineAsync(json);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        string? response;
        try
        {
            response = await _reader.ReadLineAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("Response timed out");
        }

        if (response == null)
            throw new IOException("Connection closed");

        return JsonNode.Parse(response);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();
    }
}
