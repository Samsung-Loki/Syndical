using System;
using System.Net;
using System.Xml;

namespace TheAirBlow.Syndical.Library
{
    /// <summary>
    /// FOTA Cloud fetcher
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
        public static DeviceFirmwaresXml GetDeviceFirmwares(string model, string region)
        {
            if (!DeviceExists(model, region))
                throw new InvalidOperationException("Device does not exist!");
            var req = (HttpWebRequest)WebRequest.Create($"https://fota-cloud-dn.ospserver.net/firmware/{region}/{model}/version.xml");
            var res = (HttpWebResponse)req.GetResponse();
            var doc = new XmlDocument();
            doc.LoadXml(res.GetString());
            return DeviceFirmwaresXml.FromXml(doc);
        }

        /// <summary>
        /// Get firmware list XML
        /// </summary>
        /// <param name="model">Device model</param>
        /// <param name="region">Device region</param>
        /// <param name="version">Firmware version</param>
        /// <param name="normalized">Is version normalized</param>
        /// <returns>Does firmware exist</returns>
        public static bool FirmwareExists(string model, string region, string version, bool normalized)
        {
            var info = GetDeviceFirmwares(model, region);
            if (normalized && info.Latest.NormalizedVersion == version) return true;
            if (!normalized && info.Latest.Version == version) return true;
            foreach (var fw in info.Old) {
                if (normalized && fw.NormalizedVersion == version) return true;
                if (!normalized && fw.Version == version) return true;
            }

            return false;
        }
    }
}