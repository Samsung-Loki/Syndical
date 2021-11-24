using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;

namespace Syndical.Library
{
    /// <summary>
    /// FUS client
    /// </summary>
    public class FusClient
    {
        private byte[] _encryptedNonce = Array.Empty<byte>();
        private byte[] _nonce = Array.Empty<byte>();
        private byte[] _token = Array.Empty<byte>();
        private string _sessionId = "";

        /// <summary>
        /// Instantiate a FusClient
        /// </summary>
        public FusClient() {
            SendRequest("NF_DownloadGenerateNonce.do");
        }

        /// <summary>
        /// Send a request to FUS server
        /// </summary>
        /// <param name="path">Path</param>
        /// <param name="cloud">Cloud prefix</param>
        /// <param name="data">Request body</param>
        /// <param name="query">Query parameters</param>
        /// <param name="method">HTTP Method</param>
        /// <returns>Response body</returns>
        public HttpWebResponse SendRequest([NotNull] string path, [NotNull] string data = "", string query = null, [NotNull] string method = "POST", bool cloud = false)
        {
            Globals.Logger.Information($"[FusClient] Sending {method} request to {path}");
            var url = cloud 
                ? $"http://cloud-neofussvr.sslcs.cdngc.net/{path}"
                : $"https://neofussvr.sslcs.cdngc.net/{path}";
            if (!string.IsNullOrEmpty(query))
                url += $"?{query}";
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Headers.Add("Authorization", $"FUS nonce=\"{_encryptedNonce.ToUtf8String()}\", " +
                                             $"signature=\"{_token.ToUtf8String()}\", nc=\"\", type=\"\", realm=\"\", newauth=\"1\"");
            req.Headers.Add("Cache-Control", "no-cache");
            if (!string.IsNullOrEmpty(data)) {
                byte[] buf = Encoding.ASCII.GetBytes(data); // ASCII here, why?
                using (Stream stream = req.GetRequestStream())
                    stream.Write(buf, 0, buf.Length);
                req.ContentLength = buf.Length;
            }
            req.UserAgent = "Kies2.0_FUS";
            req.CookieContainer = new CookieContainer();
            req.CookieContainer.Add(new Uri(url), new Cookie("JSESSIONID", _sessionId));
            var res = (HttpWebResponse)req.GetResponse();
            if (res.Headers.AllKeys.Contains("NONCE") && res.Headers["NONCE"] != null) {
                _encryptedNonce = res.Headers["NONCE"].ToUtf8Bytes();
                _nonce = Crypto.DecryptNonce(_encryptedNonce);
                _token = Crypto.NonceToToken(_nonce);
            }

            if (res.Cookies.FirstOrDefault(x => x.Name == "JSESSIONID") != null)
                _sessionId = res.Cookies["JSESSIONID"]?.Value;

            return res;
        }

        /// <summary>
        /// Download a file
        /// </summary>
        /// <param name="filename">Filename</param>
        /// <returns>Response stream</returns>
        public HttpWebResponse DownloadFile([NotNull] string filename)
        {
            return SendRequest("NF_DownloadBinaryForMass.do", query: $"file={filename}", method: "GET", cloud: true);
        }

        /// <summary>
        /// Build a XML for a FUS request
        /// </summary>
        /// <param name="data">Data dictionary</param>
        /// <param name="logicCheck">LOGIC_CHECK value</param>
        /// <returns>XML string</returns>
        public static string BuildFusXml([NotNull] Dictionary<string, string> data, [NotNull] string logicCheck)
        {
            // Create an XML document
            var doc = new XmlDocument();
            var root = doc.CreateElement("FUSMsg");
            // Build FUSHdr
            var hdrRoot = doc.CreateElement("FUSHdr");
            var hdrVer = doc.CreateElement("ProtoVer");
            hdrVer.InnerText = "1.0";
            hdrRoot.AppendChild(hdrVer);
            // Build body root
            var bodyRoot = doc.CreateElement("FUSBody");
            var body = doc.CreateElement("Put");
            // LOGIC_CHECK is always required
            data.Add("LOGIC_CHECK", logicCheck);
            // Build body data
            foreach (KeyValuePair<string, string> pair in data) {
                var el = doc.CreateElement(pair.Key);
                var elData = doc.CreateElement("Data");
                elData.InnerText = pair.Value;
                el.AppendChild(elData);
                body.AppendChild(el);
            }
            // Append all child elements
            bodyRoot.AppendChild(body);
            root.AppendChild(hdrRoot);
            root.AppendChild(bodyRoot);
            doc.AppendChild(root);
            return doc.OuterXml;
        }

        /// <summary>
        /// Send a binary init request
        /// </summary>
        /// <param name="filename">Filename</param>
        public void SendBinaryInit([NotNull] string filename)
        {
            var xml = BuildFusXml(new Dictionary<string, string> {
                {"BINARY_FILE_NAME", filename}
            }, Crypto.GetLogicCheck(string.Join("", filename.Split(".")[0].TakeLast(16).ToArray())!.ToUtf8Bytes(), _nonce).ToUtf8String()); 
            // Logic check out of filename without the extension and use only last 16 characters
            SendRequest("NF_DownloadBinaryInitForMass.do", xml);
        }

        /// <summary>
        /// Get binary information XML
        /// </summary>
        /// <param name="version">Firmware version</param>
        /// <param name="model">Device model</param>
        /// <param name="region">Device region</param>
        /// <param name="factory">Factory firmware (BINARU_NATURE = 1)</param>
        /// <returns>Binary information XML</returns>
        public XmlDocument GetBinaryInfo(string version, string model, string region, bool factory)
        {
            var xml = BuildFusXml(new Dictionary<string, string> {
                {"ACCESS_MODE", "2"},
                {"BINARY_NATURE", factory ? "1" : "0"},
                {"CLIENT_PRODUCT", "Smart Switch"},
                {"DEVICE_FW_VERSION", version},
                {"DEVICE_LOCAL_CODE", region},
                {"DEVICE_MODEL_NAME", model}
            }, Crypto.GetLogicCheck(version.ToUtf8Bytes(), _nonce).ToUtf8String());
            var doc = new XmlDocument();
            doc.LoadXml(SendRequest("NF_DownloadBinaryInform.do", xml).GetString());
            return doc;
        } 
    }
}