using System;
using System.Net;
using System.Windows;
using System.Windows.Input;
using CodeTitans.JSon;
using Newtonsoft.Json.Linq;

namespace Wp7Faye {
    class NtsftToCdtt : IJSonWritable {
        private readonly JToken obj;

        public NtsftToCdtt (JToken obj) {
            this.obj = obj;
        }

        #region Implementation of IJSonWritable

        public void Write (IJSonWriter output) {
            var reader = new JSonReader ();
            var tree = reader.Read (obj.ToString ());
            output.Write (tree);
        }

        #endregion
    }
}
