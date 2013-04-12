using System;

namespace Wp7Faye {
    public class Faye {
        public static MessageHandler Connect(string uri) {
            return Connect(new Uri(uri));
        }

        public static MessageHandler Connect(Uri uri) {
            MessageHandler handler;
            switch (uri.Scheme) {
                case "http":
                case "https":
                    handler = new PollingHandler();
                    break;
                case "ws":
                case "wss":
                    handler = new WebsocketHandler();
                    break;
                default: throw new ArgumentException("Uri scheme '" + uri.Scheme + "' was not recognized.");
            }

            handler.connectionUri = uri.ToString();
            return handler;
        }
    }
}
