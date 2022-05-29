using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace TheAirBlow.Syndical.Library
{
    /// <summary>
    /// FOTA cloud device firmware history
    /// </summary>
    public class DeviceFirmwaresXml
    {
        /// <summary>
        /// Old firmware (basic information)
        /// </summary>
        public class Firmware
        {
            /// <summary>
            /// "rcount", dunno what it is
            /// </summary>
            public int RCount;
            
            /// <summary>
            /// Firmware filesize
            /// </summary>
            public long FileSize;
            
            /// <summary>
            /// Firmware version
            /// </summary>
            public string Version;
            
            /// <summary>
            /// Normalized firmware version
            /// </summary>
            public string NormalizedVersion;
        }
        
        /// <summary>
        /// Latest firmware (basic information)
        /// </summary>
        public class LatestFirmware
        {
            /// <summary>
            /// Android version
            /// </summary>
            public int AndroidVersion;

            /// <summary>
            /// Firmware version
            /// </summary>
            public string Version;
            
            /// <summary>
            /// Normalized firmware version
            /// </summary>
            public string NormalizedVersion;
        }

        public readonly LatestFirmware Latest = new();
        public readonly List<Firmware> Old = new();

        /// <summary>
        /// Parse an XML
        /// </summary>
        /// <param name="doc">XML document</param>
        /// <returns>DeviceFirmwaresXml</returns>
        public static DeviceFirmwaresXml FromXml([NotNull] XmlDocument doc)
        {
            // Basic stuff
            var info = new DeviceFirmwaresXml();
            if (doc.DocumentElement == null)
                throw new ArgumentNullException(nameof(doc), "doc.DocumentElement is null");
            var versionsRoot = doc.DocumentElement.SelectSingleNode("./firmware/version");
            if (versionsRoot == null)
                throw new InvalidOperationException("Invalid DeviceFirmwaresXml XML!");
            // Latest firmware
            var latestNode = versionsRoot.SelectSingleNode("./latest");
            if (latestNode?.Attributes?["o"]?.InnerText == null || latestNode.InnerText == null)
                throw new InvalidOperationException("Invalid DeviceFirmwaresXml XML!");
            info.Latest.AndroidVersion = int.Parse(latestNode.Attributes["o"].InnerText);
            info.Latest.Version = latestNode.InnerText;
            info.Latest.NormalizedVersion = latestNode.InnerText.NormalizeVersion();
            // Old firmware
            var oldNode = versionsRoot.SelectSingleNode("./upgrade");
            if (oldNode == null)
                throw new InvalidOperationException("Invalid DeviceFirmwaresXml XML!");
            foreach (XmlNode node in oldNode.ChildNodes) {
                var firm = new Firmware();
                if (node?.Attributes?["rcount"]?.InnerText == null || node?.Attributes?["fwsize"]?.InnerText == null || node.InnerText == null)
                    throw new InvalidOperationException("Invalid DeviceFirmwaresXml XML!");
                firm.RCount = int.Parse(node.Attributes["rcount"].InnerText);
                firm.FileSize = long.Parse(node.Attributes["fwsize"].InnerText);
                firm.Version = node.InnerText;
                firm.NormalizedVersion = node.InnerText.NormalizeVersion();
                info.Old.Add(firm);
            }
            return info;
        }
    }
}