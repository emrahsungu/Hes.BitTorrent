namespace Hes.BitTorrent.Extensions {

    using System.IO;

    public static class MemoryStreamExtensions {

        /// <summary>
        /// Appends the given byte value to the end of the stream.
        /// </summary>
        /// <param name="stream">
        /// MemoryStream to append to.
        /// </param>
        /// <param name="value">
        /// byte value to append.
        /// </param>
        public static void Append(this MemoryStream stream, byte value) {
            stream.WriteByte(value);
        }

        /// <summary>
        /// Appends the given byte array to the end of the stream.
        /// </summary>
        /// <param name="stream">
        /// MemoryStream to append to.
        /// </param>
        /// <param name="values">
        /// byte array to append to memory stream.
        /// </param>
        public static void Append(this MemoryStream stream, byte[] values) {
            stream.Write(values, 0, values.Length);
        }
    }
}