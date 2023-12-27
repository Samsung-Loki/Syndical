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

namespace TheAirBlow.Syndical.Library
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
        public const string CloudFusServerLink = "http://cloud-neofussvr.samsungmobile.com/";
        public const string FusServerLink = "https://neofussvr.sslcs.cdngc.net/";
        
        // Authentication stuff
        private string _encryptedNonce = "";
        private string _nonce = "";
        private string _token = "";
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
        /// <param name="start">Range header start</param>
        /// <returns>Response body</returns>
        public HttpWebResponse SendRequest([NotNull] string path, [NotNull] string data = "", string query = null, [NotNull] string method = "POST", 
            bool cloud = false, long start = 0)
        {
            var url = cloud 
                ? CloudFusServerLink + path
                : FusServerLink + path;
            if (!string.IsNullOrEmpty(query))
                url += $"?{query}";
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.ReadWriteTimeout = 0x61a8;
            req.Timeout = 0x61a8;
            req.Method = method;
            req.Headers.Add("Authorization", $"FUS nonce=\"{_encryptedNonce}\", " +
                                             $"signature=\"{_token}\", nc=\"\", type=\"\", realm=\"\", newauth=\"1\"");
            req.Headers.Add("Cache-Control", "no-cache");
            if (start > 0) req.Headers.Add("Range", $"bytes={start}-");
            if (!string.IsNullOrEmpty(data)) {
                byte[] buf = Encoding.ASCII.GetBytes(data);
                using (Stream stream = req.GetRequestStream())
                    stream.Write(buf, 0, buf.Length);
                req.ContentLength = buf.Length;
            }
            req.UserAgent = "Kies2.0_FUS";
            req.CookieContainer = new CookieContainer();
            req.CookieContainer.Add(new Uri(url), new Cookie("JSESSIONID", _sessionId));
            var res = (HttpWebResponse)req.GetResponse();
            if (res.Headers.AllKeys.Contains("NONCE") && res.Headers["NONCE"] != null) {
                _encryptedNonce = res.Headers["NONCE"];
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
        /// <param name="imei">Device region</param>
        /// <param name="type">Firmware type</param>
        /// <returns>Firmware information</returns>
        public FirmwareInfo GetFirmwareInformation(string version, string model, string region, string imei, FirmwareInfo.FirmwareType type)
        {
            var xml = BuildFusXml(new Dictionary<string, string> {
                {"ACCESS_MODE", "2"},
                {"CLIENT_PRODUCT", "Syndical"},
                {"DEVICE_IMEI_PUSH", imei},
                {"BINARY_NATURE", type == FirmwareInfo.FirmwareType.Factory ? "1" : "0"},
                {"DEVICE_FW_VERSION", version},
                {"DEVICE_LOCAL_CODE", region},
                {"DEVICE_MODEL_NAME", model},
                {"LOGIC_CHECK", Crypto.GetLogicCheck(version, _nonce).ToAsciiString()}
            });
            var doc = new XmlDocument();
            var str = SendRequest("NF_DownloadBinaryInform.do", xml).GetString();
            doc.LoadXml(str);
            return FirmwareInfo.FromXml(doc, version);
        }
        
        /// <summary>
        /// Send a firmware binary download init request
        /// </summary>
        /// <param name="info">Firmare info</param>
        public void InitializeDownload([NotNull] FirmwareInfo info)
        {
            var xml = BuildFusXml(new Dictionary<string, string> {
                {"BINARY_FILE_NAME", info.FileName},
                {"LOGIC_CHECK", Crypto.GetLogicCheck(string.Join("",         // Logic check out of filename without
                    info.FileName.Split(".")[0].TakeLast(16).ToArray())!  // the extension and use only 
                    , _nonce).ToAsciiString()}                                    // last 16 characters
            }); 
            
            SendRequest("NF_DownloadBinaryInitForMass.do", xml);
        }

        /// <summary>
        /// Download firmware binary
        /// </summary>
        /// <param name="info">Firmare info</param>
        /// <param name="start">Range header</param>
        /// <returns>Response stream</returns>
        public HttpWebResponse DownloadFirmware([NotNull] FirmwareInfo info, long start = 0)
            => SendRequest("NF_DownloadBinaryForMass.do", query: $"file={info.CloudModelRoot}{info.FileName}", method: "GET", 
                cloud: true, start: 0);
    }
}
