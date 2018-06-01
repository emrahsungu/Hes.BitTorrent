namespace Hes.BitTorrent.Extensions {

    using System;
    using System.Collections;
    using System.Linq;
    using System.Text;
    using Enums;
    using Logging;
    using MiscUtil.Conversion;

    public static class PeerEncoder {

        #region Encoding

        public static byte[] EncodeHandshake(byte[] hash, string id)
        {
            var message = new byte[68];
            message[0] = 19;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("BitTorrent protocol"), 0, message, 1, 19);
            Buffer.BlockCopy(hash, 0, message, 28, 20);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(id), 0, message, 48, 20);

            return message;
        }

        public static byte[] EncodeKeepAlive()
        {
            return EndianBitConverter.Big.GetBytes(0);
        }

        public static byte[] EncodeChoke()
        {
            return EncodeState(MessageType.Choke);
        }

        public static byte[] EncodeUnchoke()
        {
            return EncodeState(MessageType.Unchoke);
        }

        public static byte[] EncodeInterested()
        {
            return EncodeState(MessageType.Interested);
        }

        public static byte[] EncodeNotInterested()
        {
            return EncodeState(MessageType.NotInterested);
        }

        public static byte[] EncodeState(MessageType type)
        {
            var message = new byte[5];
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(1), 0, message, 0, 4);
            message[4] = (byte)type;
            return message;
        }

        public static byte[] EncodeHave(int index)
        {
            var message = new byte[9];
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(5), 0, message, 0, 4);
            message[4] = (byte)MessageType.Have;
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(index), 0, message, 5, 4);

            return message;
        }

        public static byte[] EncodeBitfield(bool[] isPieceDownloaded)
        {
            var numPieces = isPieceDownloaded.Length;
            var numBytes = Convert.ToInt32(Math.Ceiling(numPieces / 8.0));
            var numBits = numBytes * 8;

            var length = numBytes + 1;

            var message = new byte[length + 4];
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(length), 0, message, 0, 4);
            message[4] = (byte)MessageType.Bitfield;

            var downloaded = new bool[numBits];
            for (var i = 0; i < numPieces; i++)
                downloaded[i] = isPieceDownloaded[i];

            var bitfield = new BitArray(downloaded);
            var reversed = new BitArray(numBits);
            for (var i = 0; i < numBits; i++)
                reversed[i] = bitfield[numBits - i - 1];

            reversed.CopyTo(message, 5);

            return message;
        }

        public static byte[] EncodeRequest(int index, int begin, int length)
        {
            var message = new byte[17];
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(13), 0, message, 0, 4);
            message[4] = (byte)MessageType.Request;
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(index), 0, message, 5, 4);
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(begin), 0, message, 9, 4);
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(length), 0, message, 13, 4);

            return message;
        }

        public static byte[] EncodePiece(int index, int begin, byte[] data)
        {
            var length = data.Length + 9;

            var message = new byte[length + 4];
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(length), 0, message, 0, 4);
            message[4] = (byte)MessageType.Piece;
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(index), 0, message, 5, 4);
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(begin), 0, message, 9, 4);
            Buffer.BlockCopy(data, 0, message, 13, data.Length);

            return message;
        }

        public static byte[] EncodeCancel(int index, int begin, int length)
        {
            var message = new byte[17];
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(13), 0, message, 0, 4);
            message[4] = (byte)MessageType.Cancel;
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(index), 0, message, 5, 4);
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(begin), 0, message, 9, 4);
            Buffer.BlockCopy(EndianBitConverter.Big.GetBytes(length), 0, message, 13, 4);

            return message;
        }

        #endregion Encoding
    }

    public static class PeerDecoder {

        public static bool DecodeHandshake(byte[] bytes, out byte[] hash, out string id) {
            hash = new byte[20];
            id = "";

            if(bytes.Length != 68 || bytes[0] != 19) {
                ConsoleLogger.WriteLn("handshake not valid, length must be of 68 and first byte must be 19");
                return false;
            }

            if(Encoding.UTF8.GetString(bytes.Skip(1).Take(19).ToArray()) != "BitTorrent protocol") {
                ConsoleLogger.WriteLn("handshake not valid, protocol is not equal to \"BitTorrent protocol\"");
                return false;
            }
            hash = bytes.Skip(28).Take(20).ToArray();
            id = Encoding.UTF8.GetString(bytes.Skip(48).Take(20).ToArray());
            return true;
        }

        public static bool DecodeKeepAlive(byte[] bytes) {
            if(bytes.Length == 4 && EndianBitConverter.Big.ToInt32(bytes, 0) == 0) {
                return true;
            }
            ConsoleLogger.WriteLn("keep alive not valid");
            return false;
        }

        public static bool DecodeChoke(byte[] bytes) {
            return DecodeState(bytes, MessageType.Choke);
        }

        public static bool DecodeUnchoke(byte[] bytes) {
            return DecodeState(bytes, MessageType.Unchoke);
        }

        public static bool DecodeInterested(byte[] bytes) {
            return DecodeState(bytes, MessageType.Interested);
        }

        public static bool DecodeNotInterested(byte[] bytes) {
            return DecodeState(bytes, MessageType.NotInterested);
        }

        public static bool DecodeState(byte[] bytes, MessageType type) {
            if(bytes.Length != 5 || EndianBitConverter.Big.ToInt32(bytes, 0) != 1 || bytes[4] != (byte) type) {
                ConsoleLogger.WriteLn("invalid " + Enum.GetName(typeof(MessageType), type));
                return false;
            }

            return true;
        }

        public static bool DecodeHave(byte[] bytes, out int index) {
            index = -1;

            if(bytes.Length != 9 || EndianBitConverter.Big.ToInt32(bytes, 0) != 5) {
                ConsoleLogger.WriteLn("have not valid, first byte is not equal to 0x2");
                return false;
            }
            index = EndianBitConverter.Big.ToInt32(bytes, 5);
            return true;
        }

        public static bool DecodeBitfield(byte[] bytes, int pieces, out bool[] isPieceDownloaded) {
            isPieceDownloaded = new bool[pieces];

            var expectedLength = Convert.ToInt32(Math.Ceiling(pieces / 8.0)) + 1;

            if(bytes.Length != expectedLength + 4 || EndianBitConverter.Big.ToInt32(bytes, 0) != expectedLength) {
                ConsoleLogger.WriteLn("bitfield not valid, first is not equal to " + expectedLength);
                return false;
            }
            var bitfield = new BitArray(bytes.Skip(5).ToArray());
            for(var i = 0; i < pieces; i++) {
                isPieceDownloaded[i] = bitfield[bitfield.Length - 1 - i];
            }
            return true;
        }

        public static bool DecodeRequest(byte[] bytes, out int index, out int begin, out int length) {
            index = -1;
            begin = -1;
            length = -1;

            if(bytes.Length != 17 || EndianBitConverter.Big.ToInt32(bytes, 0) != 13) {
                ConsoleLogger.WriteLn("request message is not valid, must be of length 17");
                return false;
            }

            index = EndianBitConverter.Big.ToInt32(bytes, 5);
            begin = EndianBitConverter.Big.ToInt32(bytes, 9);
            length = EndianBitConverter.Big.ToInt32(bytes, 13);

            return true;
        }

        public static bool DecodePiece(byte[] bytes, out int index, out int begin, out byte[] data) {
            index = -1;
            begin = -1;
            data = new byte[0];

            if(bytes.Length < 13) {
                ConsoleLogger.WriteLn("piece message is not valid");
                return false;
            }

            var length = EndianBitConverter.Big.ToInt32(bytes, 0) - 9;
            index = EndianBitConverter.Big.ToInt32(bytes, 5);
            begin = EndianBitConverter.Big.ToInt32(bytes, 9);

            data = new byte[length];
            Buffer.BlockCopy(bytes, 13, data, 0, length);

            return true;
        }

        public static bool DecodeCancel(byte[] bytes, out int index, out int begin, out int length) {
            index = -1;
            begin = -1;
            length = -1;

            if(bytes.Length != 17 || EndianBitConverter.Big.ToInt32(bytes, 0) != 13) {
                ConsoleLogger.WriteLn("cancel message is not valid, must be of length 17");
                return false;
            }
            index = EndianBitConverter.Big.ToInt32(bytes, 5);
            begin = EndianBitConverter.Big.ToInt32(bytes, 9);
            length = EndianBitConverter.Big.ToInt32(bytes, 13);
            return true;
        }
    }
}