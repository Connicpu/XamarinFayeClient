using System;
using System.Diagnostics;
using CodeTitans.Bayeux;
using CodeTitans.JSon;
using Newtonsoft.Json.Linq;

namespace Wp7Faye {
    public class PollingHandler : MessageHandler {

        #region Fields

        private SessionState state;

        #endregion

        #region Overrides of MessageHandler

        public override bool IsConnected {
            get { throw new NotImplementedException(); }
        }

        protected override void Connect(string uri) {
            if (Opened) {
                state.connection.Dispose();
            }

            state = new SessionState {
                connection = new BayeuxConnection(uri)
            };

            state.connection.Connected += ConnectionOnConnected;
            state.connection.ConnectionFailed += ConnectionOnConnectionFailed;
            state.connection.Disconnected += ConnectionOnDisconnected;
            state.connection.ResponseReceived += ConnectionOnResponseReceived;

            state.connection.Timeout = Timeout;
            state.connection.LongPollingTimeout = Timeout;

            Handshake();
        }

        public override void Handshake() {
            state.connection.Handshake(BayeuxConnectionTypes.LongPolling, null, null, true);
        }

        public override void ConnectRequest() {
            state.connection.Connect();
        }

        public override void Disconnect() {
            if (Opened) state.connection.Disconnect();
        }

        public override void Subscribe(string channel) {
            var ext = NewtonsoftToCodeTitans(MessageExt);
            state.connection.Subscribe(null, ext, channel, true);
        }

        public override void Unsubscribe(string channel) {
            state.connection.Unsubscribe(channel);
        }

        public override void Publish(string channel, JToken data) {
            state.connection.Publish(NewtonsoftToCodeTitans(data),
                NewtonsoftToCodeTitans(MessageExt), channel, null, true);
        }

        public override void Dispose() {
            if (state.connection != null) {
                Disconnect();
                state.connection.Dispose();
            }
            state = new SessionState();
        }

        #endregion

        #region Internal Events

        protected override void OnConnected() {
            base.OnConnected();
        }

        private void ConnectionOnResponseReceived(object sender, BayeuxConnectionEventArgs bayeuxConnectionEventArgs) {
            var serverMessages = JArray.Parse(bayeuxConnectionEventArgs.Data);
            foreach (var message in serverMessages) {
                ProcessMessage((JObject)message);
            }
        }

        protected override void OnHandshakeResponse(JObject response) {
            if (!((bool)response["successful"])) {
                OnDisconnected();
                Dispose();
            }
            ClientID = (string)response["clientId"];
            state.started = true;
            ConnectRequest();
            state.connection.StartLongPolling();

            base.OnHandshakeResponse(response);
        }

        #endregion

        #region Helpers

        public IJSonWritable NewtonsoftToCodeTitans(JToken token) {
            return token != null ? new NtsftToCdtt(token) : null;
        }

        #endregion

        #region Socket Events

        private void ConnectionOnConnected(object sender, BayeuxConnectionEventArgs bayeuxConnectionEventArgs) {
            OnConnected();
        }

        private void ConnectionOnConnectionFailed(object sender, BayeuxConnectionEventArgs bayeuxConnectionEventArgs) {
            OnConnectTimeout();
        }

        private void ConnectionOnDisconnected(object sender, BayeuxConnectionEventArgs bayeuxConnectionEventArgs) {
            OnDisconnected();
        }

        #endregion

        #region Properties

        public bool Opened {
            get { return state.connection != null && state.started; }
        }

        public struct SessionState {
            public BayeuxConnection connection;
            public bool started;
        }

        #endregion
    }
}
