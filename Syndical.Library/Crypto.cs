using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Syndical.Library
{
    /// <summary>
    /// Cryptography helper
    /// </summary>
    public static class Crypto
    {
        private const string Key1 = "hqzdurufm2c8mf6bsjezu1qgveouv7c7";
        private const string Key2 = "w13r4cvf4hctaujv";
        
        /// <summary>
        /// AES encryption (CBC mode, PKCS#7 padding)
        /// </summary>
        /// <param name="input">Input</param>
        /// <param name="key">Key</param>
        public static byte[] Encrypt(byte[] input, byte[] key)
        {
            RijndaelManaged rj = new RijndaelManaged();
            rj.Key = key;
            rj.IV = key.Take(16).ToArray();
            rj.Mode = CipherMode.CBC;
            rj.Padding = PaddingMode.PKCS7;
            try {
                MemoryStream ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, rj.CreateEncryptor(), CryptoStreamMode.Write)) {
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                }
                ms.Close();
                return ms.ToArray();
            } finally { rj.Clear(); }
        }
        
        /// <summary>
        /// AES decryption (CBC mode, PKCS#7 padding)
        /// </summary>
        /// <param name="input">Input</param>
        /// <param name="key">Key</param>
        public static byte[] Decrypt(byte[] input, byte[] key)
        {
            RijndaelManaged rj = new RijndaelManaged();
            rj.BlockSize = 128; 
            rj.Key = key;
            rj.IV = key.Take(16).ToArray();
            rj.Mode = CipherMode.CBC;
            rj.Padding = PaddingMode.PKCS7;
            try {
                MemoryStream ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, rj.CreateDecryptor(), CryptoStreamMode.Write)) {
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                }
                ms.Close();
                return ms.ToArray();
            } finally { rj.Clear(); }
        }

        /// <summary>
        /// Convert nonce to key
        /// </summary>
        /// <param name="nonce">Nonce</param>
        /// <returns>AES key</returns>
        private static byte[] NonceToKey(byte[] nonce)
        {
            var str = nonce.ToUtf8String();
            var sb = new StringBuilder();
            foreach (char chr in str.Take(16))
                sb.Append(Key1[char.ConvertToUtf32(chr.ToString(), 0) % 16]);
            sb.Append(Key2);
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Get LOGIC_CHECK value
        /// </summary>
        /// <param name="input">Input</param>
        /// <param name="nonce">Nonce</param>
        /// <returns>LOGIC_CHECK value</returns>
        public static byte[] GetLogicCheck(byte[] input, byte[] nonce)
        {
            var sb = new StringBuilder();
            foreach (char chr in nonce.ToUtf8String())
                sb.Append(input.ToUtf8String()[char.ConvertToUtf32(chr.ToString(), 0) & 0xf]);
            return sb.ToString().ToUtf8Bytes();
        }

        /// <summary>
        /// Convert nonce to response token
        /// </summary>
        /// <param name="nonce">Nonce</param>
        /// <returns>Response token</returns>
        public static byte[] NonceToToken(byte[] nonce)
            => Convert.ToBase64String(Encrypt(nonce, NonceToKey(nonce))).ToUtf8Bytes();

        /// <summary>
        /// Decrypt nonce using Key 1
        /// </summary>
        /// <param name="nonce">Nonce</param>
        /// <returns>Decrypted nonce</returns>
        public static byte[] DecryptNonce(byte[] nonce)
            => Decrypt(Convert.FromBase64String(nonce.ToUtf8String()), Key1.ToUtf8Bytes());

        /// <summary>
        /// Get key for version 2 encryption
        /// </summary>
        /// <param name="version">Firmware version</param>
        /// <param name="model">Device model</param>
        /// <param name="region">Device region</param>
        /// <returns>Version 2 encryption key</returns>
        public static byte[] GetVersion2Key(string version, string model, string region)
            => Encoding.UTF8.GetBytes($"{region}:{model}:{version}".GetMd5Hash());

        /// <summary>
        /// Get key for version 4 encryption
        /// </summary>
        /// <param name="factory">Factory firmware (BINARY_NATURE = 1)</param>
        /// <param name="xml">Binary information</param>
        /// <returns>Version 4 encryption key</returns>
        public static byte[] GetVersion4Key(bool factory, XmlDocument xml)
        {
            var input = xml.DocumentElement?.SelectSingleNode("./FUSBody/Results/LATEST_FW_VERSION/Data")?.Value;
            var nonce = xml.DocumentElement?.SelectSingleNode(factory ? "./FUSBody/Put/LOGIC_VALUE_FACTORY/Data" : "./FUSBody/Put/LOGIC_VALUE_HOME/Data")?.Value;
            return GetLogicCheck(input.ToUtf8Bytes(), nonce.ToUtf8Bytes()).ToUtf8String().GetMd5Hash().ToUtf8Bytes();
        }
    }
}