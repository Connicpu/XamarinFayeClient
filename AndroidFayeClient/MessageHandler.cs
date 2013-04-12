using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AndroidFayeClient {
    public abstract class MessageHandler : IDisposable {

        #region Fields

        internal string connectionUri;
        public readonly List<string> subscriptions = new List<string>();

        #endregion

        #region Events

        public event Action Connected;
        public event Action ConnectTimeout;
        public event Action Disconnected;

        public event ErrorEventHandler Error;

        public event Action<JObject> HandshakeResponse;
        public event Action<JObject> PublishResponse;
        public event Action<JObject> SubscribeResponse;
        public event Action<JObject> UnsubscribeResponse;
        public event Action<JObject> ConnectResponse;

        public event Action<JObject> MessageRecieved;

        #endregion

        #region Abstracts

        public abstract bool IsConnected { get; }

        protected abstract void Connect(string uri);
        public abstract void Handshake();
        public abstract void ConnectRequest();
        public abstract void Disconnect();
        public abstract void Subscribe(string channel);
        public abstract void Unsubscribe(string channel);
        public abstract void Publish(string channel, JToken data);

        public abstract void Dispose();

        #endregion

        #region Methods

        public void Connect() {
            Connect(connectionUri);
        }

        public bool IsSubscribed(string channel) {
            return subscriptions.Any(subscription => SubscriptionMatches(subscription, channel));
        }

        private static bool SubscriptionMatches(string match, string sub) {
            var matchParts = match.Split(new[] { '/' });
            var subParts = sub.Split(new[] { '/' });
            if (subParts.Length < matchParts.Length) {
                return false;
            }
            for (var i = 0; i < matchParts.Length; i++) {
                if (matchParts[i] == "**") {
                    return true;
                }
                if ((matchParts[i] != "*") && (matchParts[i] != subParts[i])) {
                    return false;
                }
            }
            return (subParts.Length == matchParts.Length);
        }

        public void Publish(string channel, object data) {
            Publish(channel, JObject.FromObject(data));
        }

        #endregion

        #region Event Methods
        protected virtual void OnConnected() {
            if (Connected != null) {
                Connected();
            }
        }

        protected virtual void OnConnectResponse(JObject response) {
            if (ConnectResponse != null) {
                ConnectResponse(response);
            }
        }

        protected virtual void OnConnectTimeout() {
            if (ConnectTimeout != null) {
                ConnectTimeout();
            }
        }

        protected virtual void OnDisconnected() {
            if (Disconnected != null) {
                Disconnected();
            }
        }

        protected void OnError(JObject errorData, ErrorLevel level) {
            if (Error != null) {
                Error(this, new ErrorEventArgs {
                    ErrorData = errorData,
                    Level = level
                });
            }
        }

        protected virtual void OnHandshakeResponse(JObject response) {
            if (HandshakeResponse != null) {
                HandshakeResponse(response);
            }
        }

        protected virtual void OnMessageRecieved(JObject response) {
            if (MessageRecieved != null) {
                MessageRecieved(response);
            }
        }

        protected virtual void OnPublishResponse(JObject response) {
            if (PublishResponse != null) {
                PublishResponse(response);
            }
        }

        protected virtual void OnSubscribeResponse(JObject response) {
            if ((bool)response["successful"]) {
                if (!subscriptions.Contains((string)response["subscription"])) {
                    subscriptions.Add((string)response["subscription"]);
                }
            } else {
                subscriptions.Remove((string)response["subscription"]);
            }
            if (SubscribeResponse != null) {
                SubscribeResponse(response);
            }
        }

        protected virtual void OnUnsubscribeResponse(JObject response) {
            subscriptions.Remove((string)response["subscription"]);
            if (UnsubscribeResponse != null) {
                UnsubscribeResponse(response);
            }
        }
        #endregion

        #region Message Processing
        protected void ProcessMessage(JObject data) {
            var channel = data["channel"].ToString().ToLower().Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (channel.Length < 2) {
                OnError(JObject.FromObject(new {
                    type = "transmission-content",
                    part = "channel",
                    error = "less than two parts detected in channel path"
                }), ErrorLevel.Warning);
            }

            if (channel[0] == "meta") {
                switch (channel[1]) {
                    case "handshake":
                        OnHandshakeResponse(data);
                        break;
                    case "connect":
                        OnConnectResponse(data);
                        break;
                    case "subscribe":
                        OnSubscribeResponse(data);
                        break;
                    case "unsubscribe":
                        OnUnsubscribeResponse(data);
                        break;
                }
            }

            OnMessageRecieved(data);
        }
        #endregion

        #region Properties

        public string ClientID { get; protected set; }
        public JToken MessageExt { get; set; }
        public int Timeout { get; set; }

        #endregion

        #region Enums, Classes, and Delegates
        public class ErrorEventArgs : EventArgs {
            public JObject ErrorData { get; set; }
            public ErrorLevel Level { get; set; }
        }

        public delegate void ErrorEventHandler(object sender, ErrorEventArgs error);

        public enum ErrorLevel {
            Warning,
            Severe,
            Fatal
        }
        #endregion

    }
}

