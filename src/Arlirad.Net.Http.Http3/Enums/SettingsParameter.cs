namespace Arlirad.Http3.Enums;

internal enum SettingsParameter
{
    QPackMaxTableCapacity = 0x01,
    MaxFieldSectionSize = 0x06,
    QPackBlockedStreams = 0x07,
    EnableConnectProtocol = 0x08,
    H3Datagram = 0x33,
    EnableMetadata = 0x4D44,
}