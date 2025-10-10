namespace Arlirad.Http3.Enums;

internal enum StreamType
{
    Control = 0x00,
    Push = 0x01,
    Encoder = 0x02,
    Decoder = 0x03,
}