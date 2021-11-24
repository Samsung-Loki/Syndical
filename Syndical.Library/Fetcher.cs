using System;
using System.Net;
using System.Xml;

namespace Syndical.Library
{
    /// <summary>
    /// Samsung device fetcher
    /// </summary>
    public static class Fetcher
    {
        /// <summary>
        /// Check does the device exist
        /// </summary>
        /// <param name="model">Device model</param>
        /// <param name="region">Device region</param>
        /// <returns>Does it exist</returns>
        public static bool DeviceExists(string model, string region)
        {
            try {
                var req = (HttpWebRequest) WebRequest.Create(
                    $"https://fota-cloud-dn.ospserver.net/firmware/{region}/{model}/version.xml");
                var res = (HttpWebResponse) req.GetResponse();
                return res.StatusCode == HttpStatusCode.OK;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Get firmware list XML
        /// </summary>
        /// <param name="model">Device model</param>
        /// <param name="region">Device region</param>
        /// <returns>Firmware list MXL</returns>
        /// <exception cref="InvalidOperationException">Device does not exist</exception>
        public static XmlDocument GetFirmwareList(string model, string region)
        {
            if (!DeviceExists(model, region))
                throw new InvalidOperationException("Device does not exist!");
            var req = (HttpWebRequest)WebRequest.Create($"https://fota-cloud-dn.ospserver.net/firmware/{region}/{model}/version.xml");
            var res = (HttpWebResponse)req.GetResponse();
            var doc = new XmlDocument();
            doc.LoadXml(res.GetString());
            return doc;
        }
    }
}