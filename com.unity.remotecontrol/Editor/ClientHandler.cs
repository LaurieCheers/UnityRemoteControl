using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.RemoteControl;
using UnityEngine;

namespace Unity.RemoteControl.Editor
{
    public class ClientHandler : IDisposable
    {
        private readonly int _clientId;
        private readonly TcpClient _tcpClient;
        private readonly TcpServer _server;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly object _writeLock = new object();
        private bool _disposed;

        public int ClientId => _clientId;

        public ClientHandler(int clientId, TcpClient tcpClient, TcpServer server)
        {
            _clientId = clientId;
            _tcpClient = tcpClient;
            _server = server;
            _stream = tcpClient.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };
        }

        public async Task ProcessAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _tcpClient.Connected)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null)
                        break;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    await HandleMessageAsync(line);
                }
            }
            catch (IOException)
            {
                // Connection closed
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
            catch (Exception ex)
            {
                _server.Log($"Client {_clientId} error: {ex.Message}");
            }
            finally
            {
                _server.RemoveClient(_clientId);
            }
        }

        private async Task HandleMessageAsync(string json)
        {
            Request request = null;
            try
            {
                request = JsonUtility.FromJson<Request>(json);

                // JsonUtility doesn't handle Dictionary, so parse params manually
                request.@params = ParseParams(json);

                if (string.IsNullOrEmpty(request.command))
                {
                    SendResponse(Response.Error(request?.id ?? "", "Missing command"));
                    return;
                }

                _server.Log($"[{_clientId}] <- {request.command}");

                var registry = _server.CommandRegistry;
                if (registry == null)
                {
                    SendResponse(Response.Error(request.id, "Command registry not initialized"));
                    return;
                }

                var response = await registry.ExecuteAsync(request);
                SendResponse(response);
            }
            catch (Exception ex)
            {
                _server.Log($"[{_clientId}] Error: {ex.Message}");
                SendResponse(Response.Error(request?.id ?? "", ex.Message));
            }
        }

        private System.Collections.Generic.Dictionary<string, object> ParseParams(string json)
        {
            var result = new System.Collections.Generic.Dictionary<string, object>();

            try
            {
                // Find "params" object in JSON
                var paramsIndex = json.IndexOf("\"params\"");
                if (paramsIndex < 0)
                    paramsIndex = json.IndexOf("\"@params\"");

                if (paramsIndex < 0)
                    return result;

                var colonIndex = json.IndexOf(':', paramsIndex);
                if (colonIndex < 0)
                    return result;

                var braceStart = json.IndexOf('{', colonIndex);
                if (braceStart < 0)
                    return result;

                // Find matching closing brace
                int depth = 0;
                int braceEnd = -1;
                for (int i = braceStart; i < json.Length; i++)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            braceEnd = i;
                            break;
                        }
                    }
                }

                if (braceEnd < 0)
                    return result;

                var paramsJson = json.Substring(braceStart, braceEnd - braceStart + 1);
                ParseJsonObject(paramsJson, result);
            }
            catch
            {
                // Ignore parsing errors
            }

            return result;
        }

        private void ParseJsonObject(string json, System.Collections.Generic.Dictionary<string, object> result)
        {
            // Simple JSON parser for flat key-value pairs
            int i = 1; // Skip opening brace
            while (i < json.Length - 1)
            {
                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

                if (json[i] == '}' || json[i] == ',')
                {
                    i++;
                    continue;
                }

                // Parse key
                if (json[i] != '"')
                    break;

                i++;
                var keyStart = i;
                while (i < json.Length && json[i] != '"') i++;
                var key = json.Substring(keyStart, i - keyStart);
                i++; // Skip closing quote

                // Skip colon
                while (i < json.Length && json[i] != ':') i++;
                i++;

                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

                // Parse value
                object value = null;
                if (json[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < json.Length && json[i] != '"')
                    {
                        if (json[i] == '\\' && i + 1 < json.Length)
                        {
                            i++;
                            switch (json[i])
                            {
                                case 'n': sb.Append('\n'); break;
                                case 'r': sb.Append('\r'); break;
                                case 't': sb.Append('\t'); break;
                                case '\\': sb.Append('\\'); break;
                                case '"': sb.Append('"'); break;
                                default: sb.Append(json[i]); break;
                            }
                        }
                        else
                        {
                            sb.Append(json[i]);
                        }
                        i++;
                    }
                    value = sb.ToString();
                    i++;
                }
                else if (json[i] == '{')
                {
                    // Nested object - find matching brace
                    int depth = 0;
                    var start = i;
                    while (i < json.Length)
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}')
                        {
                            depth--;
                            if (depth == 0)
                            {
                                i++;
                                break;
                            }
                        }
                        i++;
                    }
                    value = json.Substring(start, i - start);
                }
                else if (json[i] == '[')
                {
                    // Array - find matching bracket
                    int depth = 0;
                    var start = i;
                    while (i < json.Length)
                    {
                        if (json[i] == '[') depth++;
                        else if (json[i] == ']')
                        {
                            depth--;
                            if (depth == 0)
                            {
                                i++;
                                break;
                            }
                        }
                        i++;
                    }
                    value = json.Substring(start, i - start);
                }
                else if (json.Substring(i).StartsWith("true"))
                {
                    value = true;
                    i += 4;
                }
                else if (json.Substring(i).StartsWith("false"))
                {
                    value = false;
                    i += 5;
                }
                else if (json.Substring(i).StartsWith("null"))
                {
                    value = null;
                    i += 4;
                }
                else
                {
                    // Number
                    var numStart = i;
                    while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-' || json[i] == 'e' || json[i] == 'E' || json[i] == '+'))
                        i++;

                    var numStr = json.Substring(numStart, i - numStart);
                    if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
                    {
                        if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                            value = d;
                    }
                    else
                    {
                        if (long.TryParse(numStr, out var l))
                            value = l;
                    }
                }

                if (key.Length > 0)
                    result[key] = value;
            }
        }

        public void SendResponse(Response response)
        {
            try
            {
                var json = SerializeResponse(response);
                lock (_writeLock)
                {
                    _writer.WriteLine(json);
                }
                _server.Log($"[{_clientId}] -> {(response.success ? "OK" : "ERR")}");
            }
            catch (Exception ex)
            {
                _server.Log($"[{_clientId}] Send error: {ex.Message}");
            }
        }

        private string SerializeResponse(Response response)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":");
            sb.Append(response.id != null ? $"\"{EscapeString(response.id)}\"" : "null");
            sb.Append(",\"success\":");
            sb.Append(response.success ? "true" : "false");
            sb.Append(",\"data\":");
            sb.Append(SerializeObject(response.data));
            sb.Append(",\"error\":");
            sb.Append(response.error != null ? $"\"{EscapeString(response.error)}\"" : "null");
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeObject(object obj)
        {
            if (obj == null)
                return "null";

            if (obj is bool b)
                return b ? "true" : "false";

            if (obj is string s)
                return $"\"{EscapeString(s)}\"";

            if (obj is int || obj is long || obj is float || obj is double)
                return Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture);

            if (obj is System.Collections.IList list)
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(SerializeObject(list[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }

            // Handle our Protocol types manually to properly serialize 'object' fields
            if (obj is GameObjectInfo go)
                return SerializeGameObjectInfo(go);

            if (obj is ComponentInfo comp)
                return SerializeComponentInfo(comp);

            if (obj is PropertyInfo prop)
                return SerializePropertyInfo(prop);

            if (obj is PrefabInfo prefab)
                return SerializePrefabInfo(prefab);

            if (obj is PrefabListResult prefabList)
                return SerializePrefabListResult(prefabList);

            if (obj is AssetInfo asset)
                return SerializeAssetInfo(asset);

            if (obj is AssetListResult assetList)
                return SerializeAssetListResult(assetList);

            // Use JsonUtility for other complex objects
            try
            {
                return JsonUtility.ToJson(obj);
            }
            catch
            {
                return $"\"{EscapeString(obj.ToString())}\"";
            }
        }

        private string SerializeGameObjectInfo(GameObjectInfo go)
        {
            var sb = new StringBuilder("{");
            sb.Append($"\"name\":\"{EscapeString(go.name)}\"");
            sb.Append($",\"path\":\"{EscapeString(go.path)}\"");
            if (go.components != null)
            {
                // Focus node: full detail
                sb.Append($",\"instanceId\":{go.instanceId}");
                sb.Append($",\"activeSelf\":{(go.activeSelf ? "true" : "false")}");
                sb.Append($",\"tag\":\"{EscapeString(go.tag)}\"");
                sb.Append($",\"layer\":{go.layer}");
                sb.Append(",\"components\":");
                sb.Append(SerializeObject(go.components));
            }
            if (go.componentNames != null)
            {
                sb.Append(",\"componentNames\":");
                sb.Append(SerializeObject(go.componentNames));
            }
            if (go.children != null && go.children.Count > 0)
            {
                sb.Append(",\"children\":");
                sb.Append(SerializeObject(go.children));
            }
            sb.Append($",\"childCount\":{go.childCount}");
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeComponentInfo(ComponentInfo comp)
        {
            var sb = new StringBuilder("{");
            sb.Append($"\"type\":\"{EscapeString(comp.type)}\"");
            sb.Append($",\"fullType\":\"{EscapeString(comp.fullType)}\"");
            sb.Append($",\"instanceId\":{comp.instanceId}");
            sb.Append($",\"enabled\":{(comp.enabled ? "true" : "false")}");
            sb.Append(",\"properties\":");
            sb.Append(SerializeObject(comp.properties));
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializePropertyInfo(PropertyInfo prop)
        {
            var sb = new StringBuilder("{");
            sb.Append($"\"name\":\"{EscapeString(prop.name)}\"");
            sb.Append($",\"path\":\"{EscapeString(prop.path)}\"");
            sb.Append($",\"type\":\"{EscapeString(prop.type)}\"");
            sb.Append($",\"value\":{SerializeObject(prop.value)}");
            sb.Append($",\"isReadOnly\":{(prop.isReadOnly ? "true" : "false")}");
            sb.Append($",\"isArray\":{(prop.isArray ? "true" : "false")}");
            sb.Append($",\"arraySize\":{prop.arraySize}");
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializePrefabInfo(PrefabInfo prefab)
        {
            var sb = new StringBuilder("{");
            sb.Append($"\"name\":\"{EscapeString(prefab.name)}\"");
            sb.Append($",\"path\":\"{EscapeString(prefab.path)}\"");
            sb.Append($",\"guid\":\"{EscapeString(prefab.guid)}\"");
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializePrefabListResult(PrefabListResult result)
        {
            var sb = new StringBuilder("{");
            sb.Append($"\"total\":{result.total}");
            sb.Append($",\"offset\":{result.offset}");
            sb.Append($",\"limit\":{result.limit}");
            sb.Append(",\"prefabs\":");
            sb.Append(SerializeObject(result.prefabs));
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeAssetInfo(AssetInfo asset)
        {
            var sb = new StringBuilder("{");
            sb.Append($"\"name\":\"{EscapeString(asset.name)}\"");
            sb.Append($",\"path\":\"{EscapeString(asset.path)}\"");
            sb.Append($",\"guid\":\"{EscapeString(asset.guid)}\"");
            sb.Append($",\"type\":\"{EscapeString(asset.type)}\"");
            if (asset.properties != null)
            {
                sb.Append(",\"properties\":");
                sb.Append(SerializeObject(asset.properties));
            }
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeAssetListResult(AssetListResult result)
        {
            var sb = new StringBuilder("{");
            sb.Append($"\"total\":{result.total}");
            sb.Append($",\"offset\":{result.offset}");
            sb.Append($",\"limit\":{result.limit}");
            sb.Append(",\"assets\":");
            sb.Append(SerializeObject(result.assets));
            sb.Append("}");
            return sb.ToString();
        }

        private string EscapeString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcpClient?.Dispose(); } catch { }
        }
    }
}
