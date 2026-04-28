namespace Arlirad.WebSocket;

public readonly struct WebSocketFrame(WebSocketOpcode opcode, bool fin, ReadOnlyMemory<byte> payload)
{
    public WebSocketOpcode Opcode
    {
        get;
    } = opcode;
    public bool Fin
    {
        get;
    } = fin;
    public ReadOnlyMemory<byte> Payload
    {
        get;
    } = payload;
}
