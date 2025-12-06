namespace Arlirad.Http3.Enums;

public enum ErrorCode
{
    NoError = 0x0100,
    GeneralProtectionError = 0x0101,
    InternalError = 0x0102,
    StreamCreationError = 0x0103,
    ClosedCriticalStream = 0x0104,
    FrameUnexpected = 0x0105,
    FrameError = 0x0106,
    ExcessiveLoad = 0x0107,
    IdError = 0x108,
    SettingsError = 0x109,
    MissingSettings = 0x10A,
    RequestRejected = 0x010B,
    RequestCancelled = 0x010C,
    RequestIncomplete = 0x010D,
    MessageError = 0x010E,
    ConnectError = 0x10F,
    VersionFallback = 0x110,
}