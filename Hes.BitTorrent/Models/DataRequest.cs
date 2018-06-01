namespace Hes.BitTorrent.Models {

    public class DataRequest {

        /// <summary>
        ///
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="index"></param>
        /// <param name="begin"></param>
        /// <param name="length"></param>
        public DataRequest(Peer peer, int index, int begin, int length) {
            Peer = peer;
            Piece = index;
            Begin = begin;
            Length = length;
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public int Begin { get; }

        /// <summary>
        ///
        /// </summary>
        public int Length { get; }

        /// <summary>
        ///
        /// </summary>
        public Peer Peer { get; }

        /// <summary>
        ///
        /// </summary>
        public int Piece { get; }

        /// <summary>
        ///
        /// </summary>
        public void Cancel() {
            IsCancelled = true;
        }
    }
}