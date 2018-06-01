namespace Hes.BitTorrent {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using Enums;
    using Extensions;
    using Logging;
    using MiscUtil.Conversion;
    using Models;

    public class Peer {

        public override string ToString() {
            return $"[{IPEndPoint} ({Id})]";
        }

        public event EventHandler Disconnected;

        public event EventHandler StateChanged;

        public event EventHandler<DataRequest> BlockRequested;

        public event EventHandler<DataRequest> BlockCancelled;

        public event EventHandler<DataPackage> BlockReceived;

        #region Properties

        public string LocalId { get; set; }
        public string Id { get; set; }

        public Torrent Torrent { get; }

        public IPEndPoint IPEndPoint { get; }

        public string Key => IPEndPoint.ToString();

        private TcpClient TcpClient { get; set; }
        private NetworkStream stream { get; set; }
        private const int bufferSize = 256;
        private readonly byte[] streamBuffer = new byte[bufferSize];
        private List<byte> data = new List<byte>();

        public bool[] IsPieceDownloaded = new bool[0];

        public string PiecesDownloaded => string.Join("", IsPieceDownloaded.Select(Convert.ToInt32));

        public int PiecesRequiredAvailable => IsPieceDownloaded.Select((x, i) => x && !Torrent.IsPieceVerified[i]).Count(x => x);

        public int PiecesDownloadedCount => IsPieceDownloaded.Count(x => x);

        public bool IsCompleted => PiecesDownloadedCount == Torrent.PieceCount;

        public bool IsDisconnected;

        public bool IsHandshakeSent;
        public bool IsPositionSent;
        public bool IsChokeSent = true;
        public bool IsInterestedSent;

        public bool IsHandshakeReceived;
        public bool IsChokeReceived = true;
        public bool IsInterestedReceived;

        public bool[][] IsBlockRequested = new bool[0][];

        public int BlocksRequested => IsBlockRequested.Sum(x => x.Count(y => y));

        public DateTime LastActive;
        public DateTime LastKeepAlive = DateTime.MinValue;

        public long Uploaded;
        public long Downloaded;

        #endregion Properties

        #region Constructors

        public Peer(Torrent torrent, string localId, TcpClient client) : this(torrent, localId) {
            TcpClient = client;
            IPEndPoint = (IPEndPoint) client.Client.RemoteEndPoint;
        }

        public Peer(Torrent torrent, string localId, IPEndPoint endPoint) : this(torrent, localId) {
            IPEndPoint = endPoint;
        }

        private Peer(Torrent torrent, string localId) {
            LocalId = localId;
            Torrent = torrent;
            LastActive = DateTime.UtcNow;
            IsPieceDownloaded = new bool[Torrent.PieceCount];
            IsBlockRequested = new bool[Torrent.PieceCount][];
            for(var i = 0; i < Torrent.PieceCount; i++) {
                IsBlockRequested[i] = new bool[Torrent.GetBlockCount(i)];
            }
        }

        #endregion Constructors

        #region Tcp

        public void Connect() {
            if(TcpClient == null) {
                TcpClient = new TcpClient();
                try {
                    TcpClient.Connect(IPEndPoint);
                }
                catch(Exception e) {
                    Disconnect();
                    return;
                }
            }

            ConsoleLogger.WriteLn(this, "connected");

            stream = TcpClient.GetStream();
            stream.BeginRead(streamBuffer, 0, bufferSize, HandleRead, null);

            SendHandshake();
            if(IsHandshakeReceived)
                SendBitfield(Torrent.IsPieceVerified);
        }

        public void Disconnect() {
            if(!IsDisconnected) {
                IsDisconnected = true;
                ConsoleLogger.WriteLn(this, "disconnected, down " + Downloaded + ", up " + Uploaded);
            }

            if(TcpClient != null)
                TcpClient.Close();

            if(Disconnected != null)
                Disconnected(this, new EventArgs());
        }

        private void SendBytes(byte[] bytes) {
            try {
                stream.Write(bytes, 0, bytes.Length);
            }
            catch(Exception e) {
                Disconnect();
            }
        }

        private void HandleRead(IAsyncResult ar) {
            var bytes = 0;
            try {
                bytes = stream.EndRead(ar);
            }
            catch(Exception e) {
                Disconnect();
                return;
            }

            data.AddRange(streamBuffer.Take(bytes));

            var messageLength = GetMessageLength(data);
            while(data.Count >= messageLength) {
                HandleMessage(data.Take(messageLength).ToArray());
                data = data.Skip(messageLength).ToList();

                messageLength = GetMessageLength(data);
            }

            try {
                stream.BeginRead(streamBuffer, 0, bufferSize, HandleRead, null);
            }
            catch(Exception e) {
                Disconnect();
            }
        }

        private int GetMessageLength(List<byte> data) {
            if(!IsHandshakeReceived)
                return 68;

            if(data.Count < 4)
                return int.MaxValue;

            return EndianBitConverter.Big.ToInt32(data.ToArray(), 0) + 4;
        }

        #endregion Tcp

        #region Incoming Messages

        private MessageType GetMessageType(byte[] bytes) {
            if(!IsHandshakeReceived) {
                return MessageType.Handshake;
            }

            if(bytes.Length == 4 && EndianBitConverter.Big.ToInt32(bytes, 0) == 0) {
                return MessageType.KeepAlive;
            }

            if(bytes.Length > 4 && Enum.IsDefined(typeof(MessageType), (int) bytes[4])) {
                return (MessageType) bytes[4];
            }
            return MessageType.Unknown;
        }

        private void HandleMessage(byte[] bytes) {
            LastActive = DateTime.UtcNow;

            var type = GetMessageType(bytes);

            switch(type) {
                case MessageType.Unknown:
                    return;

                case MessageType.Handshake when PeerDecoder.DecodeHandshake(bytes, out var hash, out var id):
                    HandleHandshake(hash, id);
                    return;

                case MessageType.KeepAlive when PeerDecoder.DecodeKeepAlive(bytes):
                    HandleKeepAlive();
                    return;

                case MessageType.Choke when PeerDecoder.DecodeChoke(bytes):
                    HandleChoke();
                    return;

                case MessageType.Unchoke when PeerDecoder.DecodeUnchoke(bytes):
                    HandleUnchoke();
                    return;

                case MessageType.Interested when PeerDecoder.DecodeInterested(bytes):
                    HandleInterested();
                    return;

                case MessageType.NotInterested when PeerDecoder.DecodeNotInterested(bytes):
                    HandleNotInterested();
                    return;

                case MessageType.Have when PeerDecoder.DecodeHave(bytes, out var index):
                    HandleHave(index);
                    return;

                case MessageType.Bitfield when PeerDecoder.DecodeBitfield(bytes, IsPieceDownloaded.Length, out var isPieceDownloaded):
                    HandleBitfield(isPieceDownloaded);
                    return;

                case MessageType.Request when PeerDecoder.DecodeRequest(bytes, out var index, out var begin, out var length):
                    HandleRequest(index, begin, length);
                    return;

                case MessageType.Piece when PeerDecoder.DecodePiece(bytes, out var index, out var begin, out var _data):
                    HandlePiece(index, begin, _data);
                    return;

                case MessageType.Cancel when PeerDecoder.DecodeCancel(bytes, out var index, out var begin, out var length):
                    HandleCancel(index, begin, length);
                    return;

                case MessageType.Port:
                    ConsoleLogger.WriteLn(this, " <- port: " + string.Join("", bytes.Select(x => x.ToString("x2"))));
                    return;
            }

            ConsoleLogger.WriteLn(this, " Unhandled incoming message " + string.Join("", bytes.Select(x => x.ToString("x2"))));
            Disconnect();
        }

        private void HandleHandshake(byte[] hash, string id) {
            ConsoleLogger.WriteLn(this, "<- handshake");

            if(!Torrent.Infohash.SequenceEqual(hash)) {
                ConsoleLogger.WriteLn(this, "invalid handshake, incorrect torrent hash: expecting=" + Torrent.HexStringInfohash + ", received =" + string.Join("", hash.Select(x => x.ToString("x2"))));
                Disconnect();
                return;
            }

            Id = id;

            IsHandshakeReceived = true;
            SendBitfield(Torrent.IsPieceVerified);
        }

        private void HandleKeepAlive() {
            ConsoleLogger.WriteLn(this, "<- keep alive");
        }

        private void HandleChoke() {
            ConsoleLogger.WriteLn(this, "<- choke");
            IsChokeReceived = true;

            var handler = StateChanged;
            if(handler != null)
                handler(this, new EventArgs());
        }

        private void HandleUnchoke() {
            ConsoleLogger.WriteLn(this, "<- unchoke");
            IsChokeReceived = false;

            var handler = StateChanged;
            if(handler != null)
                handler(this, new EventArgs());
        }

        private void HandleInterested() {
            ConsoleLogger.WriteLn(this, "<- interested");
            IsInterestedReceived = true;

            var handler = StateChanged;
            if(handler != null)
                handler(this, new EventArgs());
        }

        private void HandleNotInterested() {
            ConsoleLogger.WriteLn(this, "<- not interested");
            IsInterestedReceived = false;

            var handler = StateChanged;
            if(handler != null)
                handler(this, new EventArgs());
        }

        private void HandleHave(int index) {
            IsPieceDownloaded[index] = true;
            ConsoleLogger.WriteLn(this, "<- have " + index + " - " + PiecesDownloadedCount + " available (" + PiecesDownloaded + ")");

            var handler = StateChanged;
            if(handler != null)
                handler(this, new EventArgs());
        }

        private void HandleBitfield(bool[] isPieceDownloaded) {
            for(var i = 0; i < Torrent.PieceCount; i++)
                IsPieceDownloaded[i] = IsPieceDownloaded[i] || isPieceDownloaded[i];

            ConsoleLogger.WriteLn(this, "<- bitfield " + PiecesDownloadedCount + " available (" + PiecesDownloaded + ")");

            var handler = StateChanged;
            handler?.Invoke(this, new EventArgs());
        }

        private void HandleRequest(int index, int begin, int length) {
            ConsoleLogger.WriteLn(this, "<- request " + index + ", " + begin + ", " + length);

            var handler = BlockRequested;
            handler?.Invoke(this, new DataRequest(this, index, begin, length));
        }

        private void HandlePiece(int index, int begin, byte[] data) {
            ConsoleLogger.WriteLn(this, "<- piece " + index + ", " + begin + ", " + data.Length);
            Downloaded += data.Length;

            var handler = BlockReceived;
            handler?.Invoke(this, new DataPackage(this, data, index, begin / Torrent.BlockSize));
        }

        private void HandleCancel(int index, int begin, int length) {
            ConsoleLogger.WriteLn(this, " <- cancel");

            var handler = BlockCancelled;
            handler?.Invoke(this, new DataRequest(this, index, begin, length));
        }

        private void HandlePort(int port) {
            ConsoleLogger.WriteLn(this, "<- port");
        }

        #endregion Incoming Messages

        #region Outgoing Messages

        private void SendHandshake() {
            if(IsHandshakeSent)
                return;

            ConsoleLogger.WriteLn(this, "-> handshake");
            SendBytes(PeerEncoder.EncodeHandshake(Torrent.Infohash, LocalId));
            IsHandshakeSent = true;
        }

        public void SendKeepAlive() {
            if(LastKeepAlive > DateTime.UtcNow.AddSeconds(-30))
                return;

            ConsoleLogger.WriteLn(this, "-> keep alive");
            SendBytes(PeerEncoder.EncodeKeepAlive());
            LastKeepAlive = DateTime.UtcNow;
        }

        public void SendChoke() {
            if(IsChokeSent) {
                return;
            }

            ConsoleLogger.WriteLn(this, "-> choke");
            SendBytes(PeerEncoder.EncodeChoke());
            IsChokeSent = true;
        }

        public void SendUnchoke() {
            if(!IsChokeSent)
                return;

            ConsoleLogger.WriteLn(this, "-> unchoke");
            SendBytes(PeerEncoder.EncodeUnchoke());
            IsChokeSent = false;
        }

        public void SendInterested() {
            if(IsInterestedSent)
                return;

            ConsoleLogger.WriteLn(this, "-> interested");
            SendBytes(PeerEncoder.EncodeInterested());
            IsInterestedSent = true;
        }

        public void SendNotInterested() {
            if(!IsInterestedSent)
                return;

            ConsoleLogger.WriteLn(this, "-> not interested");
            SendBytes(PeerEncoder.EncodeNotInterested());
            IsInterestedSent = false;
        }

        public void SendHave(int index) {
            ConsoleLogger.WriteLn(this, "-> have " + index);
            SendBytes(PeerEncoder.EncodeHave(index));
        }

        public void SendBitfield(bool[] isPieceDownloaded) {
            ConsoleLogger.WriteLn(this, "-> bitfield " + string.Join("", isPieceDownloaded.Select(x => x ? 1 : 0)));
            SendBytes(PeerEncoder.EncodeBitfield(isPieceDownloaded));
        }

        public void SendRequest(int index, int begin, int length) {
            ConsoleLogger.WriteLn(this, "-> request " + index + ", " + begin + ", " + length);
            SendBytes(PeerEncoder.EncodeRequest(index, begin, length));
        }

        public void SendPiece(int index, int begin, byte[] data) {
            ConsoleLogger.WriteLn(this, "-> piece " + index + ", " + begin + ", " + data.Length);
            SendBytes(PeerEncoder.EncodePiece(index, begin, data));
            Uploaded += data.Length;
        }

        public void SendCancel(int index, int begin, int length) {
            ConsoleLogger.WriteLn(this, "-> cancel");
            SendBytes(PeerEncoder.EncodeCancel(index, begin, length));
        }

        #endregion Outgoing Messages
    }
}