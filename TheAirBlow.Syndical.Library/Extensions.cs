using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TheAirBlow.Syndical.Library
{
    /// <summary>
    /// Extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Get string out of input stream
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>Input stream data</returns>
        public static string GetString(this HttpWebResponse source)
        {
            using var stream = new StreamReader(source.GetResponseStream(), Encoding.UTF8);
            return stream.ReadToEnd();
        }

        /// <summary>
        /// Normalizes version string
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>Normalized version string</returns>
        public static string NormalizeVersion(this string source)
        {
            var split = source.Split('/').ToList();
            if (split.Count == 3)
                split.Add(split[0]);
            if (split[2] == "")
                split[2] = split[0];
            return string.Join('/', split);
        }
        
        /// <summary>
        /// Get MD5 hashsum digest
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>MD5 hashsum digest</returns>
        public static byte[] GetMd5Hash(this string source)
        {
            // Use input string to calculate MD5 hash
            using var md5 = MD5.Create();
            byte[] inputBytes = source.ToAsciiBytes();
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return hashBytes;
        }
        
        /// <summary>
        /// Get MD5 hashsum digest
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>MD5 hashsum digest</returns>
        public static byte[] GetMd5Hash(this byte[] source)
        {
            // Use input string to calculate MD5 hash
            using var md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(source);
            return hashBytes;
        }

        /// <summary>
        /// Convert string to UTF-8 byte sequence
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>UTF-8 byte sequence</returns>
        public static byte[] ToAsciiBytes(this string source)
            => Encoding.ASCII.GetBytes(source);
        
        /// <summary>
        /// Convert UTF-8 byte sequence to string
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>String</returns>
        public static string ToAsciiString(this byte[] source)
            => Encoding.ASCII.GetString(source);
    }
}