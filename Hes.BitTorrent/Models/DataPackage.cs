namespace Hes.BitTorrent.Models {

    public class DataPackage {

        /// <summary>
        ///
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="data"></param>
        /// <param name="piece"></param>
        /// <param name="block"></param>
        public DataPackage(Peer peer, byte[] data, int piece, int block) {
            Peer = peer;
            Piece = piece;
            Block = block;
            Data = data;
        }

        /// <summary>
        ///
        /// </summary>
        public Peer Peer { get; }

        /// <summary>
        ///
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        ///
        /// </summary>
        public int Block { get; }

        /// <summary>
        ///
        /// </summary>
        public int Piece { get; }
    }
}