namespace Hes.BitTorrent {

    internal static class Delimiters {
        internal static byte DictionaryStart = (byte) 'd';  // 100
        internal static byte DictionaryEnd = (byte) 'e';    // 101
        internal static byte ListStart = (byte) 'l';        // 108
        internal static byte ListEnd = (byte) 'e';          // 101
        internal static byte NumberStart = (byte) 'i';      // 105
        internal static byte NumberEnd = (byte) 'e';        // 101
        internal static byte ByteArrayDivider = (byte) ':'; //  58
    }
}