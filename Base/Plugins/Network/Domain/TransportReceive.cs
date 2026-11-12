namespace Network.Domain;

public delegate void TransportReceive(ReadOnlySpan<byte> borrowedBytes);
