using System;
using System.IO;
using System.IO.Compression;

namespace VVRace
{
    public static class IOUtility
    {
        public static string SerializeGZipBase64String(byte[] bytes)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var gz = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gz.Write(bytes, 0, bytes.Length);
                }

                return Convert.ToBase64String(outputStream.ToArray());
            }
        }

        public static byte[] DeserializeGZipBase64String(string str)
        {
            using (var outputStream = new MemoryStream())
            using (var inputStream = new MemoryStream(Convert.FromBase64String(str)))
            using (var gz = new GZipStream(inputStream, CompressionMode.Decompress))
            {
                gz.CopyTo(outputStream);
                return outputStream.ToArray();
            }
        }

    }
}
