using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Threading.Tasks;
#endif

namespace Crestforge.Networking
{
    /// <summary>
    /// Cross-platform WebSocket client for Unity.
    /// Works on Desktop, Mobile, and WebGL.
    /// </summary>
    public class WebSocketClient
    {
        public event Action OnOpen;
        public event Action<string> OnMessage;
        public event Action OnClose;
        public event Action<string> OnError;

        public bool IsConnected { get; private set; }

        private Queue<string> receivedMessages = new Queue<string>();
        private readonly object messageLock = new object();

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL uses JavaScript WebSocket via jslib
        private int webSocketId = -1;
        private static Dictionary<int, WebSocketClient> instances = new Dictionary<int, WebSocketClient>();
        private static int nextId = 0;

        [DllImport("__Internal")]
        private static extern int WebSocketCreate(string url);

        [DllImport("__Internal")]
        private static extern void WebSocketSend(int id, string message);

        [DllImport("__Internal")]
        private static extern void WebSocketClose(int id);

        [DllImport("__Internal")]
        private static extern int WebSocketGetState(int id);

        // Called from JavaScript
        [AOT.MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnWebSocketOpen(int id)
        {
            if (instances.TryGetValue(id, out var client))
            {
                client.IsConnected = true;
                client.OnOpen?.Invoke();
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Action<int, string>))]
        private static void OnWebSocketMessage(int id, string message)
        {
            if (instances.TryGetValue(id, out var client))
            {
                lock (client.messageLock)
                {
                    client.receivedMessages.Enqueue(message);
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnWebSocketClose(int id)
        {
            if (instances.TryGetValue(id, out var client))
            {
                client.IsConnected = false;
                client.OnClose?.Invoke();
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Action<int, string>))]
        private static void OnWebSocketError(int id, string error)
        {
            if (instances.TryGetValue(id, out var client))
            {
                client.OnError?.Invoke(error);
            }
        }

        public IEnumerator Connect(string url)
        {
            webSocketId = nextId++;
            instances[webSocketId] = this;
            WebSocketCreate(url);

            // Wait for connection
            float timeout = 10f;
            while (!IsConnected && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!IsConnected)
            {
                OnError?.Invoke("Connection timeout");
            }
        }

        public void Send(string message)
        {
            if (webSocketId >= 0 && IsConnected)
            {
                WebSocketSend(webSocketId, message);
            }
        }

        public void Close()
        {
            if (webSocketId >= 0)
            {
                WebSocketClose(webSocketId);
                instances.Remove(webSocketId);
                webSocketId = -1;
            }
            IsConnected = false;
        }

#else
        // Desktop/Mobile uses System.Net.WebSockets
        private ClientWebSocket webSocket;
        private CancellationTokenSource cancellationToken;
        private bool isReceiving;

        public IEnumerator Connect(string url)
        {
            Debug.Log($"[WebSocketClient] Attempting to connect to: {url}");

            webSocket = new ClientWebSocket();
            cancellationToken = new CancellationTokenSource();

            Task connectTask = null;
            try
            {
                connectTask = webSocket.ConnectAsync(new Uri(url), cancellationToken.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketClient] Failed to start connection: {e.Message}");
                OnError?.Invoke($"Failed to start connection: {e.Message}");
                yield break;
            }

            float timeout = 10f;
            while (!connectTask.IsCompleted && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!connectTask.IsCompleted)
            {
                Debug.LogError("[WebSocketClient] Connection timeout");
                OnError?.Invoke("Connection timeout");
                yield break;
            }

            if (connectTask.IsFaulted)
            {
                string errorMsg = connectTask.Exception?.InnerException?.Message ?? connectTask.Exception?.Message ?? "Connection failed";
                Debug.LogError($"[WebSocketClient] Connection failed: {errorMsg}");
                OnError?.Invoke(errorMsg);
                yield break;
            }

            if (webSocket.State == WebSocketState.Open)
            {
                Debug.Log("[WebSocketClient] Connected successfully!");
                IsConnected = true;
                OnOpen?.Invoke();
                StartReceiving();
            }
            else
            {
                Debug.LogError($"[WebSocketClient] Unexpected state: {webSocket.State}");
                OnError?.Invoke($"WebSocket state: {webSocket.State}");
            }
        }

        private async void StartReceiving()
        {
            if (isReceiving) return;
            isReceiving = true;

            var buffer = new byte[8192];
            var messageBuffer = new List<byte>();

            try
            {
                while (webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    var segment = new ArraySegment<byte>(buffer);
                    var result = await webSocket.ReceiveAsync(segment, cancellationToken.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        IsConnected = false;
                        OnClose?.Invoke();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Accumulate message bytes (handles fragmented messages)
                        for (int i = 0; i < result.Count; i++)
                        {
                            messageBuffer.Add(buffer[i]);
                        }

                        // Only process when we have the complete message
                        if (result.EndOfMessage)
                        {
                            var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                            messageBuffer.Clear();
                            lock (messageLock)
                            {
                                receivedMessages.Enqueue(message);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (WebSocketException e)
            {
                OnError?.Invoke(e.Message);
            }
            catch (Exception e)
            {
                OnError?.Invoke(e.Message);
            }
            finally
            {
                isReceiving = false;
                if (IsConnected)
                {
                    IsConnected = false;
                    OnClose?.Invoke();
                }
            }
        }

        public void Send(string message)
        {
            if (webSocket == null || webSocket.State != WebSocketState.Open)
            {
                Debug.LogWarning("[WebSocketClient] Cannot send - not connected");
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(bytes);

            try
            {
                webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken.Token);
            }
            catch (Exception e)
            {
                OnError?.Invoke(e.Message);
            }
        }

        public void Close()
        {
            cancellationToken?.Cancel();

            if (webSocket != null)
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    }
                }
                catch { }

                webSocket.Dispose();
                webSocket = null;
            }

            IsConnected = false;
        }
#endif

        /// <summary>
        /// Call this from Update() to dispatch received messages
        /// </summary>
        public void DispatchMessages()
        {
            while (true)
            {
                string message;
                lock (messageLock)
                {
                    if (receivedMessages.Count == 0) break;
                    message = receivedMessages.Dequeue();
                }
                OnMessage?.Invoke(message);
            }
        }
    }
}
