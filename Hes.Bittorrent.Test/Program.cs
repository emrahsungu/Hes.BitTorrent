namespace Hes.BitTorrent.Test {

    using System;
    using BitTorrent;

    public static class Program {
        private static Client _client;

        public static void Main(string[] args) {
            var port = 6666;

            var path = @"c:\temp\a.torrent";
            _client = new Client(port, path, @"c:\temp");
            _client.Start();

            Console.ReadKey();
            _client.Stop();
        }
    }
}