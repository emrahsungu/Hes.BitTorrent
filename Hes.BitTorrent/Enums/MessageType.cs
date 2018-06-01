namespace Hes.BitTorrent.Enums {

    public enum MessageType {
        Unknown = -3,
        Handshake = -2,
        KeepAlive = -1,
        Choke = 0,
        Unchoke = 1,
        Interested = 2,
        NotInterested = 3,
        Have = 4,
        Bitfield = 5,
        Request = 6,
        Piece = 7,
        Cancel = 8,
        Port = 9
    }
}