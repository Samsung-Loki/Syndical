using System;
using System.Linq;
using System.Xml;

namespace Syndical.Library
{
    /// <summary>
    /// Firmware information
    /// </summary>
    public class FirmwareInfo
    {
        /// <summary>
        /// Decrypt method version
        /// </summary>
        [Flags]
        public enum DecryptVersion { V4 = 4, V2 = 2 }
        
        /// <summary>
        /// Firmware type
        /// </summary>
        public enum FirmwareType
        {
            Factory = 1,
            Home = 0
        }

        /// <summary>
        /// Firmware version
        /// </summary>
        public string Version;

        /// <summary>
        /// Body -> Put -> DEVICE_LOCAL_CODE
        /// </summary>
        public string Region;

        /// <summary>
        /// Body -> Put -> DEVICE_MODEL_NAME
        /// </summary>
        public string Model;

        /// <summary>
        /// Body -> Put -> MODEL_PATH
        /// </summary>
        public string CloudModelRoot;

        /// <summary>
        /// Body -> Put -> DESCRIPTION
        /// </summary>
        public string DescriptionUrl;
        
        /// <summary>
        /// Body -> Put -> LOGIC_VALUE_HOME
        /// </summary>
        public string HomeLogicValue;
        
        /// <summary>
        /// Body -> Put -> LOGIC_VALUE_FACTORY
        /// </summary>
        public string FactoryLogicValue;

        /// <summary>
        /// Body -> Put -> CURRENT_OS_VERSION
        /// </summary>
        public string OsVersion;
        
        /// <summary>
        /// Body -> Put -> BINARY_NAME
        /// </summary>
        public string FileName;
        
        /// <summary>
        /// Body -> Put -> BINARY_NATURE
        /// </summary>
        public FirmwareType Type;

        /// <summary>
        /// Decrypt version
        /// </summary>
        public DecryptVersion DecryptType;
        
        /// <summary>
        /// Decryption key out of data provided
        /// </summary>
        public byte[] DecryptionKey;

        /// <summary>
        /// Body -> Put -> BINARY_BYTE_SIZE
        /// </summary>
        public long FileSize;
        
        /// <summary>
        /// Body -> Results -> Status
        /// </summary>
        public double Status;

        /// <summary>
        /// Body -> Put -> BINARY_CRC
        /// </summary>
        public byte[] CrcChecksum;

        /// <summary>
        /// Parse XML
        /// </summary>
        /// <param name="doc">XMl Document</param>
        /// <param name="version">Firmware version</param>
        /// <returns>FirmwareInfo</returns>
        public static FirmwareInfo FromXml(XmlDocument doc, string version)
        {
            // Basic stuff
            var info = new FirmwareInfo {
                Version = version
            };
            if (doc.DocumentElement?.SelectSingleNode("./FUSBody") == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            var body = doc.DocumentElement.SelectSingleNode("./FUSBody");
            // info.Status
            if (body?.SelectSingleNode("./Results/Status")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.Status = double.Parse(body.SelectSingleNode("./Results/Status")?.InnerText!);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (info.Status != 200)
                throw new InvalidOperationException($"Invalid status: {info.Status}");
            // info.Type
            if (body.SelectSingleNode("./Put/BINARY_NATURE/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.Type = (FirmwareType)int.Parse(body.SelectSingleNode("./Put/BINARY_NATURE/Data")?.InnerText!);
            // info.Model
            if (body.SelectSingleNode("./Put/DEVICE_MODEL_NAME/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.Model = body.SelectSingleNode("./Put/DEVICE_MODEL_NAME/Data")?.InnerText!;
            // info.Region
            if (body.SelectSingleNode("./Put/DEVICE_LOCAL_CODE/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.Region = body.SelectSingleNode("./Put/DEVICE_LOCAL_CODE/Data")?.InnerText!;
            // info.CloudModelRoot
            if (body.SelectSingleNode("./Put/MODEL_PATH/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.CloudModelRoot = body.SelectSingleNode("./Put/MODEL_PATH/Data")?.InnerText!;
            // info.DescriptionUrl
            if (body.SelectSingleNode("./Put/DESCRIPTION/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.DescriptionUrl = body.SelectSingleNode("./Put/DESCRIPTION/Data")?.InnerText!;
            // info.HomeLogicValue
            if (body.SelectSingleNode("./Put/LOGIC_VALUE_HOME/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.HomeLogicValue = body.SelectSingleNode("./Put/LOGIC_VALUE_HOME/Data")?.InnerText!;
            // info.FactoryLogicValue
            if (body.SelectSingleNode("./Put/LOGIC_VALUE_FACTORY/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.FactoryLogicValue = body.SelectSingleNode("./Put/LOGIC_VALUE_FACTORY/Data")?.InnerText!;
            // info.OsVersion
            if (body.SelectSingleNode("./Put/CURRENT_OS_VERSION/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.OsVersion = body.SelectSingleNode("./Put/CURRENT_OS_VERSION/Data")?.InnerText!;
            // info.FileName
            if (body.SelectSingleNode("./Put/BINARY_NAME/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.FileName = body.SelectSingleNode("./Put/BINARY_NAME/Data")?.InnerText!;
            // info.FileSize
            if (body.SelectSingleNode("./Put/BINARY_BYTE_SIZE/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.FileSize = long.Parse(body.SelectSingleNode("./Put/BINARY_BYTE_SIZE/Data")?.InnerText!);
            // info.CrcChecksum
            if (body.SelectSingleNode("./Put/BINARY_CRC/Data")?.InnerText == null)
                throw new InvalidOperationException("Invalid FirmwareInfo XML!");
            info.CrcChecksum = BitConverter.GetBytes(Convert.ToUInt32(body.SelectSingleNode("./Put/BINARY_CRC/Data")?.InnerText)).Reverse().ToArray();
            // info.DecryptType
            info.DecryptType = (DecryptVersion)int.Parse(info.FileName!.TakeLast(1).ToArray()[0].ToString());
            // info.DecryptionKey
            info.DecryptionKey = info.DecryptType == DecryptVersion.V2
                ? Crypto.GetVersion2Key(version, info.Model, info.Region)
                : Crypto.GetVersion4Key(info.Type, doc);
            return info;
        }
    }
}