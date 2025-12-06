namespace Arlirad.Http3.Enums;

internal enum FrameType
{
    Data = 0x00,
    Headers = 0x01,
    CancelPush = 0x03,
    Settings = 0x04,
    PushPromise = 0x05,
    SettingsMaxFieldSectionSize = 0x06,
    GoAway = 0x07,
    MMaxPushId = 0x0d,
}