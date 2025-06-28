using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Verse;

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

        public static void ScribeNativeFloatArray(ref NativeArray<float> arr, string name)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                string encoded = null;
                Scribe_Values.Look(ref encoded, name);

                if (encoded != null)
                {
                    var bytes = IOUtility.DeserializeGZipBase64String(encoded);
                    if (!BitConverter.IsLittleEndian)
                    {
                        for (int i = 0; i < bytes.Length; i += 4)
                        {
                            var f1 = bytes[i];
                            var f2 = bytes[i + 1];
                            var f3 = bytes[i + 2];
                            var f4 = bytes[i + 3];

                            bytes[i] = f4;
                            bytes[i + 1] = f3;
                            bytes[i + 2] = f2;
                            bytes[i + 3] = f1;
                        }
                    }

                    var arrLength = BitConverter.ToInt32(bytes, 0);
                    if (arrLength == arr.Length)
                    {
                        unsafe
                        {
                            var ptr = (IntPtr)arr.GetUnsafePtr();
                            Marshal.Copy(bytes, 4, ptr, arrLength * sizeof(float));
                        }
                    }
                    else
                    {
                        Log.Warning($"Failed to load mana grid since map size not matchded : {arrLength} != {arr.Length}");
                    }
                }
            }
            else if (Scribe.mode == LoadSaveMode.Saving)
            {
                // size 4bytes + float array
                var bytes = new byte[sizeof(int) + arr.Length * 4];
                Array.Copy(BitConverter.GetBytes(arr.Length), 0, bytes, 0, 4);

                unsafe
                {
                    var ptr = (IntPtr)arr.GetUnsafePtr();
                    Marshal.Copy(ptr, bytes, sizeof(int), arr.Length * 4);
                }

                if (!BitConverter.IsLittleEndian)
                {
                    for (int i = 0; i < bytes.Length; i += 4)
                    {
                        var f1 = bytes[i];
                        var f2 = bytes[i + 1];
                        var f3 = bytes[i + 2];
                        var f4 = bytes[i + 3];

                        bytes[i] = f4;
                        bytes[i + 1] = f3;
                        bytes[i + 2] = f2;
                        bytes[i + 3] = f1;
                    }
                }

                var encodedManaGrid = IOUtility.SerializeGZipBase64String(bytes);
                Scribe_Values.Look(ref encodedManaGrid, name);
            }
        }
    }
}
