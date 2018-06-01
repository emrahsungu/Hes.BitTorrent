namespace Hes.BitTorrent.Models {

    public class FileItem {

        /// <summary>
        ///
        /// </summary>
        public FileItem(string path, long size,long offset=0) {
            Path = path;
            Size = size;
            Offset = offset;
            FormattedSize = Torrent.BytesToString(Size);
        }

        /// <summary>
        ///
        /// </summary>
        public long Offset { get; }

        /// <summary>
        ///
        /// </summary>
        public string Path { get; }

        /// <summary>
        ///
        /// </summary>
        public long Size { get; }

        /// <summary>
        ///
        /// </summary>
        public string FormattedSize { get; }
    }
}