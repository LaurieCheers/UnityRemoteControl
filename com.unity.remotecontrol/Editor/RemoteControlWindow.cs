using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor
{
    public class RemoteControlWindow : EditorWindow
    {
        private const string PortPrefKey = "RemoteControl.Port";
        private const int DefaultPort = 6000;
        private const int MaxLogLines = 100;

        private TcpServer _server;
        private CommandRegistry _commandRegistry;
        private int _port = DefaultPort;
        private Vector2 _logScrollPosition;
        private readonly List<string> _logMessages = new List<string>();
        private bool _autoScroll = true;

        [MenuItem("Window/Remote Control")]
        public static void ShowWindow()
        {
            var window = GetWindow<RemoteControlWindow>();
            window.titleContent = new GUIContent("Remote Control");
            window.minSize = new Vector2(300, 200);
            window.Show();
        }

        private void OnEnable()
        {
            _port = EditorPrefs.GetInt(PortPrefKey, DefaultPort);

            MainThreadDispatcher.Initialize();

            _commandRegistry = new CommandRegistry();
            _commandRegistry.RegisterDefaults();

            _server = new TcpServer
            {
                CommandRegistry = _commandRegistry
            };

            _server.OnLog += OnServerLog;
            _server.OnClientConnected += OnClientConnected;
            _server.OnClientDisconnected += OnClientDisconnected;
        }

        private void OnDisable()
        {
            _server?.Stop();
            _server?.Dispose();
            _server = null;

            MainThreadDispatcher.Shutdown();
        }

        private void OnServerLog(string message)
        {
            _logMessages.Add(message);
            while (_logMessages.Count > MaxLogLines)
                _logMessages.RemoveAt(0);

            Repaint();
        }

        private void OnClientConnected(int clientId)
        {
            Repaint();
        }

        private void OnClientDisconnected(int clientId)
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);

            DrawServerControls();

            EditorGUILayout.Space(10);

            DrawStatus();

            EditorGUILayout.Space(10);

            DrawLogArea();
        }

        private void DrawServerControls()
        {
            EditorGUILayout.LabelField("Server Controls", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(_server?.IsRunning == true);
                var newPort = EditorGUILayout.IntField("Port", _port);
                if (newPort != _port)
                {
                    _port = Mathf.Clamp(newPort, 1024, 65535);
                    EditorPrefs.SetInt(PortPrefKey, _port);
                }
                EditorGUI.EndDisabledGroup();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_server?.IsRunning != true)
                {
                    if (GUILayout.Button("Start Server", GUILayout.Height(30)))
                    {
                        try
                        {
                            _server.Start(_port);
                        }
                        catch (System.Exception ex)
                        {
                            EditorUtility.DisplayDialog("Error", $"Failed to start server: {ex.Message}", "OK");
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop Server", GUILayout.Height(30)))
                    {
                        _server.Stop();
                    }
                }
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            var running = _server?.IsRunning == true;
            var statusColor = running ? Color.green : Color.gray;
            var statusText = running ? $"Running on port {_server.Port}" : "Stopped";

            using (new EditorGUILayout.HorizontalScope())
            {
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = statusColor;
                EditorGUILayout.LabelField("Server:", statusText, style);
            }

            if (running)
            {
                EditorGUILayout.LabelField("Connected Clients:", _server.ClientCount.ToString());
            }
        }

        private void DrawLogArea()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", GUILayout.Width(80));

                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
                    _logMessages.Clear();
                }
            }

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(5, 5, 5, 5)
            };

            using (var scrollView = new EditorGUILayout.ScrollViewScope(_logScrollPosition, boxStyle, GUILayout.ExpandHeight(true)))
            {
                _logScrollPosition = scrollView.scrollPosition;

                var logStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = false,
                    fontSize = 11
                };

                foreach (var message in _logMessages)
                {
                    EditorGUILayout.LabelField(message, logStyle);
                }

                if (_autoScroll && _logMessages.Count > 0)
                {
                    GUILayout.Space(0);
                    _logScrollPosition.y = float.MaxValue;
                }
            }
        }
    }
}
