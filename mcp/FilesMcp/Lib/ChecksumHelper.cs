using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FourthDevs.FilesMcp.Lib
{
    internal static class ChecksumHelper
    {
        public static string ComputeFileChecksum(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BytesToHex(hash);
            }
        }

        public static string ComputeStringChecksum(string content)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                byte[] hash = sha256.ComputeHash(bytes);
                return BytesToHex(hash);
            }
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
