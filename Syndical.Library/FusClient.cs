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
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class FusClient
    {
        // Known endpoints and URLs
        public const string InitializeDownloadEndpoint = "NF_DownloadBinaryInitForMass.do";
        public const string BinaryInformationEndpoint = "NF_DownloadBinaryInform.do";
        public const string BinaryDownloadEndpoint = "NF_DownloadBinaryForMass.do";
        public const string CloudFusServerLink = "http://cloud-neofussvr.sslcs.cdngc.net/";
        public const string FusServerLink = "https://neofussvr.sslcs.cdngc.net/";
        
        // Authentication stuff
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
            var url = cloud 
                ? CloudFusServerLink + path
                : FusServerLink + path;
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
        /// Build a XML for a FUS request
        /// </summary>
        /// <param name="data">Data dictionary</param>
        /// <returns>XML string</returns>
        public static string BuildFusXml([NotNull] Dictionary<string, string> data)
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
        /// Get firmware information
        /// </summary>
        /// <param name="version">Firmware version</param>
        /// <param name="model">Device model</param>
        /// <param name="region">Device region</param>
        /// <param name="type">Firmware type</param>
        /// <returns>Firmware information</returns>
        public FirmwareInfo GetFirmwareInformation(string version, string model, string region, FirmwareInfo.FirmwareType type)
        {
            var xml = BuildFusXml(new Dictionary<string, string> {
                {"ACCESS_MODE", "2"},
                {"CLIENT_PRODUCT", "Amogus!!1!"},
                {"BINARY_NATURE", type == FirmwareInfo.FirmwareType.Factory ? "1" : "0"},
                {"DEVICE_FW_VERSION", version},
                {"DEVICE_LOCAL_CODE", region},
                {"DEVICE_MODEL_NAME", model},
                {"LOGIC_CHECK", Crypto.GetLogicCheck(version.ToUtf8Bytes(), _nonce).ToUtf8String()}
            });
            var doc = new XmlDocument();
            var str = SendRequest("NF_DownloadBinaryInform.do", xml).GetString();
            doc.LoadXml(str);
            return FirmwareInfo.FromXml(doc, version);
        }
    }
}