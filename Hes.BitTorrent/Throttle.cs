namespace Hes.BitTorrent {

    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Throttle {

        /// <summary>
        ///
        /// </summary>
        private readonly object _itemLock = new object();

        /// <summary>
        ///
        /// </summary>
        private readonly List<Item> _items = new List<Item>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="maxSize"></param>
        /// <param name="maxWindow"></param>
        public Throttle(int maxSize, TimeSpan maxWindow) {
            MaximumSize = maxSize;
            MaximumWindow = maxWindow;
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsThrottled {
            get {
                lock(_itemLock) {
                    var cutoff = DateTime.UtcNow.Add(-MaximumWindow);
                    _items.RemoveAll(x => x.Time < cutoff);
                    return _items.Sum(x => x.Size) >= MaximumSize;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public long MaximumSize { get; }

        /// <summary>
        ///
        /// </summary>
        public TimeSpan MaximumWindow { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="size"></param>
        public void Add(long size) {
            lock(_itemLock) {
                _items.Add(new Item {Time = DateTime.UtcNow, Size = size});
            }
        }

        /// <summary>
        ///
        /// </summary>
        internal struct Item {
            public long Size;
            public DateTime Time;
        }
    }
}