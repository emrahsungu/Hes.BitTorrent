namespace Hes.BitTorrent {

    using System;
    using System.Collections.Generic;
    using System.Net;
    using Enums;
    using MiscUtil.Conversion;

    public class Tracker {

        /// <summary>
        ///
        /// </summary>
        private HttpWebRequest _httpWebRequest;

        /// <summary>
        /// Creates a Tracker with the given address.
        /// </summary>
        /// <param name="address"></param>
        public Tracker(string address) {
            Address = address;
        }

        /// <summary>
        /// Address.
        /// </summary>
        public string Address { get; }

        /// <summary>
        ///
        /// </summary>
        public DateTime LastPeerRequest { get; private set; } = DateTime.MinValue;

        /// <summary>
        ///
        /// </summary>
        public TimeSpan PeerRequestInterval { get; private set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        ///
        /// </summary>
        public void ResetLastRequest() {
            LastPeerRequest = DateTime.MinValue;
        }

        /// <summary>
        ///
        /// </summary>
        public override string ToString() {
            return $"[Tracker: {Address}]";
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="torrent"></param>
        /// <param name="ev"></param>
        /// <param name="id"></param>
        /// <param name="port"></param>
        public void Update(Torrent torrent, TrackerEvent ev, string id, int port) {
            // We should wait for request interval to elapse before asking for new peers
            if(ev == TrackerEvent.Started && DateTime.UtcNow < LastPeerRequest.Add(PeerRequestInterval)) return;

            LastPeerRequest = DateTime.UtcNow;
            var url = $"{Address}?info_hash={torrent.UrlSafeStringInfohash}&peer_id={id}&port={port}&uploaded={torrent.Uploaded}&downloaded={torrent.Downloaded}&left={torrent.Left}&event={Enum.GetName(typeof(TrackerEvent), ev).ToLower()}&compact=1";
            Request(url);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="result"></param>
        private void OnResponseCallBack(IAsyncResult result) {
            byte[] data;

            using(var response = (HttpWebResponse) _httpWebRequest.EndGetResponse(result)) {
                if(response.StatusCode != HttpStatusCode.OK) {
                    Console.WriteLine("error reaching tracker " + this + ": " + response.StatusCode + " " + response.StatusDescription);
                    return;
                }

                using(var stream = response.GetResponseStream()) {
                    data = new byte[response.ContentLength];
                    stream.Read(data, 0, Convert.ToInt32(response.ContentLength));
                }
            }

            var info = BitTorrentEncoding.Encoding.Decode(data) as Dictionary<string, object>;

            if(info == null) {
                Console.WriteLine("unable to decode tracker announce response");
                return;
            }

            PeerRequestInterval = TimeSpan.FromSeconds((long) info["interval"]);
            var peerInfo = (byte[]) info["peers"];

            var peers = new List<IPEndPoint>();
            for(var i = 0; i < peerInfo.Length / 6; i++) {
                var offset = i * 6;
                var address = peerInfo[offset] + "." + peerInfo[offset + 1] + "." + peerInfo[offset + 2] + "." + peerInfo[offset + 3];
                int port = EndianBitConverter.Big.ToChar(peerInfo, offset + 4);

                peers.Add(new IPEndPoint(IPAddress.Parse(address), port));
            }

            var handler = PeerListUpdated;
            handler?.Invoke(this, peers);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="url"></param>
        private void Request(string url) {
            _httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
            _httpWebRequest.BeginGetResponse(OnResponseCallBack, null);
        }

        /// <summary>
        ///
        /// </summary>
        public event EventHandler<List<IPEndPoint>> PeerListUpdated;
    }
}