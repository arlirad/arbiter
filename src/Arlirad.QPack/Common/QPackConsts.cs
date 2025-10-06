using Arlirad.QPack.Models;

namespace Arlirad.QPack.Common;

public static class QPackConsts
{
    public const int DeltaBaseSignMask = 0b1000_0000;
    public const int IndexedFieldLineMask = 0b1000_0000;
    public const int IndexedDynamicFieldLineMask = 0b1000_0000;
    public const int IndexedStaticFieldLine = 0b1100_0000;
    public const int IndexedFieldLinePostBaseIndex = 0b0001_0000;
    public const int LiteralFieldLineWithNameReference = 0b0100_0000;
    public const int LiteralIntermediaryFieldLineWithNameReference = 0b0110_0000;
    public const int LiteralDynamicFieldLineWithNameReference = 0b0100_0000;
    public const int LiteralStaticFieldLineWithNameReference = 0b0101_0000;
    public const int LiteralFieldLineWithPostBaseNameReference = 0b0000_0000;
    public const int LiteralFieldLineWithLiteralName = 0b0010_0000;
    public const int HuffmanStringMask = 0b1000_0000;

    public const int EncoderInstructionDynamicTableCapacity = 0b0010_0000;
    public const int EncoderInstructionInsertWithNameReference = 0b1000_0000;
    public const int EncoderInstructionInsertWithDynamicNameReference = 0b1000_0000;
    public const int EncoderInstructionInsertWithStaticNameReference = 0b1100_0000;
    public const int EncoderInstructionInsertWithLiteralName = 0b0100_0000;
    public const int EncoderInstructionDuplicate = 0b0000_0000;

    public const int DecoderInstructionSectionAcknowledgement = 0b1000_0000;
    public const int DecoderInstructionStreamCancellation = 0b0100_0000;
    public const int DecoderInstructionInsertCountIncrement = 0b0000_0000;

    public const int EntryAdditionalByteCount = 32;

    public static readonly Dictionary<int, QPackField> StaticTable = new()
    {
        [0] = new QPackField(":authority"),
        [1] = new QPackField(":path", "/"),
        [2] = new QPackField("age", "0"),
        [3] = new QPackField("content-disposition"),
        [4] = new QPackField("content-length", "0"),
        [5] = new QPackField("cookie"),
        [6] = new QPackField("date"),
        [7] = new QPackField("etag"),
        [8] = new QPackField("if-modified-since"),
        [9] = new QPackField("if-none-match"),
        [10] = new QPackField("last-modified"),
        [11] = new QPackField("link"),
        [12] = new QPackField("location"),
        [13] = new QPackField("referer"),
        [14] = new QPackField("set-cookie"),
        [15] = new QPackField(":method", "CONNECT"),
        [16] = new QPackField(":method", "DELETE"),
        [17] = new QPackField(":method", "GET"),
        [18] = new QPackField(":method", "HEAD"),
        [19] = new QPackField(":method", "OPTIONS"),
        [20] = new QPackField(":method", "POST"),
        [21] = new QPackField(":method", "PUT"),
        [22] = new QPackField(":scheme", "http"),
        [23] = new QPackField(":scheme", "https"),
        [24] = new QPackField(":status", "103"),
        [25] = new QPackField(":status", "200"),
        [26] = new QPackField(":status", "304"),
        [27] = new QPackField(":status", "404"),
        [28] = new QPackField(":status", "503"),
        [29] = new QPackField("accept", "*/*"),
        [30] = new QPackField("accept", "application/dns-message"),
        [31] = new QPackField("accept-encoding", "gzip, deflate, br"),
        [32] = new QPackField("accept-ranges", "bytes"),
        [33] = new QPackField("access-control-allow-headers", "cache-control"),
        [34] = new QPackField("access-control-allow-headers", "content-type"),
        [35] = new QPackField("access-control-allow-origin", "*"),
        [36] = new QPackField("cache-control", "max-age=0"),
        [37] = new QPackField("cache-control", "max-age=2592000"),
        [38] = new QPackField("cache-control", "max-age=604800"),
        [39] = new QPackField("cache-control", "no-cache"),
        [40] = new QPackField("cache-control", "no-store"),
        [41] = new QPackField("cache-control", "public, max-age=31536000"),
        [42] = new QPackField("content-encoding", "br"),
        [43] = new QPackField("content-encoding", "gzip"),
        [44] = new QPackField("content-type", "application/dns-message"),
        [45] = new QPackField("content-type", "application/javascript"),
        [46] = new QPackField("content-type", "application/json"),
        [47] = new QPackField("content-type", "application/x-www-form-urlencoded"),
        [48] = new QPackField("content-type", "image/gif"),
        [49] = new QPackField("content-type", "image/jpeg"),
        [50] = new QPackField("content-type", "image/png"),
        [51] = new QPackField("content-type", "text/css"),
        [52] = new QPackField("content-type", "text/html; charset=utf-8"),
        [53] = new QPackField("content-type", "text/plain"),
        [54] = new QPackField("content-type", "text/plain;charset=utf-8"),
        [55] = new QPackField("range", "bytes=0-"),
        [56] = new QPackField("strict-transport-security", "max-age=31536000"),
        [57] = new QPackField("strict-transport-security", "max-age=31536000; includesubdomains"),
        [58] = new QPackField("strict-transport-security", "max-age=31536000; includesubdomains; preload"),
        [59] = new QPackField("vary", "accept-encoding"),
        [60] = new QPackField("vary", "origin"),
        [61] = new QPackField("x-content-type-options", "nosniff"),
        [62] = new QPackField("x-xss-protection", "1; mode=block"),
        [63] = new QPackField(":status", "100"),
        [64] = new QPackField(":status", "204"),
        [65] = new QPackField(":status", "206"),
        [66] = new QPackField(":status", "302"),
        [67] = new QPackField(":status", "400"),
        [68] = new QPackField(":status", "403"),
        [69] = new QPackField(":status", "421"),
        [70] = new QPackField(":status", "425"),
        [71] = new QPackField(":status", "500"),
        [72] = new QPackField("accept-language"),
        [73] = new QPackField("access-control-allow-credentials", "FALSE"),
        [74] = new QPackField("access-control-allow-credentials", "TRUE"),
        [75] = new QPackField("access-control-allow-headers", "*"),
        [76] = new QPackField("access-control-allow-methods", "get"),
        [77] = new QPackField("access-control-allow-methods", "get, post, options"),
        [78] = new QPackField("access-control-allow-methods", "options"),
        [79] = new QPackField("access-control-expose-headers", "content-length"),
        [80] = new QPackField("access-control-request-headers", "content-type"),
        [81] = new QPackField("access-control-request-method", "get"),
        [82] = new QPackField("access-control-request-method", "post"),
        [83] = new QPackField("alt-svc", "clear"),
        [84] = new QPackField("authorization"),
        [85] = new QPackField("content-security-policy", "script-src 'none'; object-src 'none'; base-uri 'none'"),
        [86] = new QPackField("early-data", "1"),
        [87] = new QPackField("expect-ct"),
        [88] = new QPackField("forwarded"),
        [89] = new QPackField("if-range"),
        [90] = new QPackField("origin"),
        [91] = new QPackField("purpose", "prefetch"),
        [92] = new QPackField("server"),
        [93] = new QPackField("timing-allow-origin", "*"),
        [94] = new QPackField("upgrade-insecure-requests", "1"),
        [95] = new QPackField("user-agent"),
        [96] = new QPackField("x-forwarded-for"),
        [97] = new QPackField("x-frame-options", "deny"),
        [98] = new QPackField("x-frame-options", "sameorigin"),
    };

    public static bool Is(byte b, int mask, int instruction) => (b & mask) == instruction;
    public static bool Is(int b, int mask, int instruction) => (b & mask) == instruction;
}