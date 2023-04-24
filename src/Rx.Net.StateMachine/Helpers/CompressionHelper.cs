using System;
using System.IO;
using System.IO.Compression;

namespace Rx.Net.StateMachine.Helpers
{
    internal class CompressionHelper
    {
        private const int _bufferSize = 4096;
        private static void CopyTo(Stream src, Stream dest)
        {
            byte[] buffer = new byte[_bufferSize];

            int count;

            while ((count = src.Read(buffer, 0, buffer.Length)) != 0)
                dest.Write(buffer, 0, count);
        }

        public static string Zip(MemoryStream input)
        {
            input.Position = 0;
            using var output = new MemoryStream();
            using (var zipStream = new GZipStream(output, CompressionMode.Compress))
                CopyTo(input, zipStream);

            return Convert.ToBase64String(output.ToArray());
        }

        public static MemoryStream Unzip(string zipped)
        {
            var bytes = Convert.FromBase64String(zipped);
            var output = new MemoryStream();

            using var input = new MemoryStream(bytes);
            using (var zipStream = new GZipStream(input, CompressionMode.Decompress))
                CopyTo(zipStream, output);
            output.Position = 0;

            return output;
        }
    }
}
