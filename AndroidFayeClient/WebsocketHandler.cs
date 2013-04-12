using System;
using System.Threading;
using Newtonsoft.Json.Linq;
using WebSocket4Net;

namespace AndroidFayeClient {
    internal class WebsocketHandler : MessageHandler {
        // Fields
        private SessionState state;

        #region Implementation of MessageHandler

        protected override void Connect(string uri) {
            if (Opened) {
                state.socket.Close();
            }

            state = new SessionState {
                socket = new WebSocket(uri)
            };

            state.socket.Opened += OnSocketOpened;
            state.socket.Closed += OnSocketClosed;
            state.socket.Error += OnSocketError;
            state.socket.MessageReceived += OnMessageReceived;
            state.socket.Open();

            new Thread(WaitForTimeout).Start();
        }

        public override void Disconnect() {
            if (!IsConnected) return;
            Send(new { channel = "/meta/disconnect", clientId = ClientID });
            state.socket.Opened -= OnSocketOpened;
            state.socket.Closed -= OnSocketClosed;
            state.socket.Error -= OnSocketError;
            state.socket.MessageReceived -= OnMessageReceived;
            OnDisconnected();
        }

        public override void ConnectRequest() {
            if (!IsConnected) {
                throw new InvalidOperationException("WebSocket not connected or bayeux session not initialized");
            }
            Send(new { channel = "/meta/connect", connectionType = "websocket" });
        }

        public override void Handshake() {
            if (!Opened) {
                throw new InvalidOperationException("WebSocket not connected");
            }
            Send(new { channel = "/meta/handshake", version = "1.0", supportedConnectionTypes = new[] { "websocket" } }, false);
        }

        public override void Subscribe(string channel) {
            if (!IsConnected) {
                throw new InvalidOperationException("WebSocket not connected or bayeux session not initialized");
            }
            Send(new { channel = "/meta/subscribe", subscription = channel });
        }

        public override void Unsubscribe(string channel) {
            if (!IsConnected) {
                throw new InvalidOperationException("WebSocket not connected or bayeux session not initialized");
            }
            Send(new { channel = "/meta/unsubscribe", subscription = channel });
        }

        public override void Publish(string channel, JToken data) {
            if (!IsConnected) {
                throw new InvalidOperationException("WebSocket not connected or bayeux session not initialized");
            }
            Send(new { channel, data });
        }

        public override void Dispose() {
            if (Opened) {
                state.socket.Opened -= OnSocketOpened;
                state.socket.Closed -= OnSocketClosed;
                state.socket.Error -= OnSocketError;
                state.socket.MessageReceived -= OnMessageReceived;
                state.socket.Close();
            }
            state.socket = null;
        }

        #endregion

        #region Internal Events

        protected override void OnConnected() {
            state.connected = true;
            Handshake();
            base.OnConnected();
        }

        protected override void OnConnectResponse(JObject response) {
            ConnectRequest();
            base.OnConnectResponse(response);
        }

        protected override void OnHandshakeResponse(JObject response) {
            if (!((bool)response["successful"])) {
                OnDisconnected();
                Dispose();
            }
            ClientID = (string)response["clientId"];
            state.handshake = true;
            ConnectRequest();
            base.OnHandshakeResponse(response);
        }

        private void OnMessageReceived(object sender, MessageReceivedEventArgs messageReceivedEventArgs) {
            var serverMessages = JArray.Parse(messageReceivedEventArgs.Message);
            foreach (var message in serverMessages) {
                ProcessMessage((JObject)message);
            }
        }

        #endregion

        #region Socket Event

        private void OnSocketClosed(object sender, EventArgs eventArgs) {
            OnDisconnected();
        }

        private void OnSocketError(object sender, SuperSocket.ClientEngine.ErrorEventArgs errorEventArgs) {
            OnError(JObject.FromObject(new { type = "socket-error", error = errorEventArgs.Exception }), ErrorLevel.Severe);
        }

        private void OnSocketOpened(object sender, EventArgs eventArgs) {
            OnConnected();
        }

        private void Send(object obj, bool addClientId = true) {
            var jobj = JObject.FromObject(obj);
            jobj["ext"] = MessageExt;
            if (addClientId) {
                jobj["clientId"] = ClientID;
            }
            state.socket.Send(jobj.ToString());
        }

        private void WaitForTimeout() {
            Thread.Sleep(Timeout);
            if (!EstablishedConnection) {
                OnConnectTimeout();
            }
        }

        #endregion

        #region Properties

        public override bool IsConnected {
            get {
                return (((state.socket != null) && (state.socket.State == WebSocketState.Open)) && state.handshake);
            }
        }

        public bool Opened {
            get {
                return ((state.socket != null) && ((state.socket.State == WebSocketState.Open) || (state.socket.State == WebSocketState.Connecting)));
            }
        }

        public bool EstablishedConnection {
            get { return Opened && state.socket.State == WebSocketState.Open; }
        }

        public struct SessionState {
            public WebSocket socket;
            public bool connected;
            public bool handshake;
        }

        #endregion
    }
}
