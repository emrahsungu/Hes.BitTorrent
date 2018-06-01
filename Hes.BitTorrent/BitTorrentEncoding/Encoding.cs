namespace Hes.BitTorrent.BitTorrentEncoding {

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Extensions;

    public static class Encoding {

        #region Decode

        public static object Decode(byte[] bytes) {
            var enumerator = ((IEnumerable<byte>) bytes).GetEnumerator();
            enumerator.MoveNext();
            return DecodeNextObject(enumerator);
        }

        public static object DecodeFile(string path) {
            var bytes = File.ReadAllBytes(path);
            return Decode(bytes);
        }

        private static object DecodeNextObject(IEnumerator<byte> enumerator) {
            if(enumerator.Current == Delimiters.DictionaryStart)
                return DecodeDictionary(enumerator);

            if(enumerator.Current == Delimiters.ListStart)
                return DecodeList(enumerator);

            if(enumerator.Current == Delimiters.NumberStart)
                return DecodeNumber(enumerator);

            return DecodeByteArray(enumerator);
        }

        private static Dictionary<string, object> DecodeDictionary(IEnumerator<byte> enumerator) {
            var dict = new Dictionary<string, object>();
            var keys = new List<string>();

            // keep decoding objects until we hit the end flag
            while(enumerator.MoveNext()) {
                if(enumerator.Current == Delimiters.DictionaryEnd)
                    break;

                // all keys are valid UTF8 strings
                var key = System.Text.Encoding.UTF8.GetString(DecodeByteArray(enumerator));
                enumerator.MoveNext();
                var val = DecodeNextObject(enumerator);

                keys.Add(key);
                dict.Add(key, val);
            }

            // verify incoming dictionary is sorted correctly
            // we will not be able to create an identical encoding otherwise
            var sortedKeys = keys.OrderBy(x => BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(x)));
            if(!keys.SequenceEqual(sortedKeys))
                throw new Exception("error loading dictionary: keys not sorted");

            return dict;
        }

        private static List<object> DecodeList(IEnumerator<byte> enumerator) {
            var list = new List<object>();

            // keep decoding objects until we hit the end flag
            while(enumerator.MoveNext()) {
                if(enumerator.Current == Delimiters.ListEnd)
                    break;

                list.Add(DecodeNextObject(enumerator));
            }

            return list;
        }

        private static byte[] DecodeByteArray(IEnumerator<byte> enumerator) {
            var lengthBytes = new List<byte>();

            // scan until we get to divider
            do {
                if(enumerator.Current == Delimiters.ByteArrayDivider)
                    break;

                lengthBytes.Add(enumerator.Current);
            }
            while(enumerator.MoveNext());

            var lengthString = System.Text.Encoding.UTF8.GetString(lengthBytes.ToArray());

            int length;
            if(!int.TryParse(lengthString, out length))
                throw new Exception("unable to parse length of byte array");

            // now read in the actual byte array
            var bytes = new byte[length];

            for(var i = 0; i < length; i++) {
                enumerator.MoveNext();
                bytes[i] = enumerator.Current;
            }

            return bytes;
        }

        private static long DecodeNumber(IEnumerator<byte> enumerator) {
            var bytes = new List<byte>();

            // keep pulling bytes until we hit the end flag
            while(enumerator.MoveNext()) {
                if(enumerator.Current == Delimiters.NumberEnd)
                    break;

                bytes.Add(enumerator.Current);
            }

            var numAsString = System.Text.Encoding.UTF8.GetString(bytes.ToArray());

            return long.Parse(numAsString);
        }

        #endregion Decode

        #region Encode

        public static byte[] Encode(object obj) {
            var buffer = new MemoryStream();

            EncodeNextObject(buffer, obj);

            return buffer.ToArray();
        }

        public static void EncodeToFile(object obj, string path) {
            File.WriteAllBytes(path, Encode(obj));
        }

        private static void EncodeNextObject(MemoryStream buffer, object obj) {
            if(obj is byte[])
                EncodeByteArray(buffer, (byte[]) obj);
            else if(obj is string)
                EncodeString(buffer, (string) obj);
            else if(obj is long)
                EncodeNumber(buffer, (long) obj);
            else if(obj.GetType() == typeof(List<object>))
                EncodeList(buffer, (List<object>) obj);
            else if(obj.GetType() == typeof(Dictionary<string, object>))
                EncodeDictionary(buffer, (Dictionary<string, object>) obj);
            else
                throw new Exception("unable to encode type " + obj.GetType());
        }

        private static void EncodeByteArray(MemoryStream buffer, byte[] body) {
            buffer.Append(System.Text.Encoding.UTF8.GetBytes(Convert.ToString(body.Length)));
            buffer.Append(Delimiters.ByteArrayDivider);
            buffer.Append(body);
        }

        private static void EncodeString(MemoryStream buffer, string input) {
            EncodeByteArray(buffer, System.Text.Encoding.UTF8.GetBytes(input));
        }

        private static void EncodeNumber(MemoryStream buffer, long input) {
            buffer.Append(Delimiters.NumberStart);
            buffer.Append(System.Text.Encoding.UTF8.GetBytes(Convert.ToString(input)));
            buffer.Append(Delimiters.NumberEnd);
        }

        private static void EncodeList(MemoryStream buffer, List<object> input) {
            buffer.Append(Delimiters.ListStart);
            foreach(var item in input)
                EncodeNextObject(buffer, item);
            buffer.Append(Delimiters.ListEnd);
        }

        private static void EncodeDictionary(MemoryStream buffer, Dictionary<string, object> input) {
            buffer.Append(Delimiters.DictionaryStart);

            // we need to sort the keys by their raw bytes, not the string
            var sortedKeys = input.Keys.ToList().OrderBy(x => BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(x)));

            foreach(var key in sortedKeys) {
                EncodeString(buffer, key);
                EncodeNextObject(buffer, input[key]);
            }

            buffer.Append(Delimiters.DictionaryEnd);
        }

        #endregion Encode
    }
}