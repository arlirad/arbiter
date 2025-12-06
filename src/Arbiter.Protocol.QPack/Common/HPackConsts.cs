namespace Arlirad.Infrastructure.QPack.Common;

public static class HPackConsts
{
    public static readonly Dictionary<int, Dictionary<long, int>> Code = new()
    {
        [5] = new Dictionary<long, int>
        {
            [0x0] = 48,  // 5
            [0x1] = 49,  // 5
            [0x2] = 50,  // 5
            [0x3] = 97,  // 5
            [0x4] = 99,  // 5
            [0x5] = 101, // 5
            [0x6] = 105, // 5
            [0x7] = 111, // 5
            [0x8] = 115, // 5
            [0x9] = 116, // 5
        },
        [6] = new Dictionary<long, int>
        {
            [0x14] = 32,  // 6
            [0x15] = 37,  // 6
            [0x16] = 45,  // 6
            [0x17] = 46,  // 6
            [0x18] = 47,  // 6
            [0x19] = 51,  // 6
            [0x1A] = 52,  // 6
            [0x1B] = 53,  // 6
            [0x1C] = 54,  // 6
            [0x1D] = 55,  // 6
            [0x1E] = 56,  // 6
            [0x1F] = 57,  // 6
            [0x20] = 61,  // 6
            [0x21] = 65,  // 6
            [0x22] = 95,  // 6
            [0x23] = 98,  // 6
            [0x24] = 100, // 6
            [0x25] = 102, // 6
            [0x26] = 103, // 6
            [0x27] = 104, // 6
            [0x28] = 108, // 6
            [0x29] = 109, // 6
            [0x2A] = 110, // 6
            [0x2B] = 112, // 6
            [0x2C] = 114, // 6
            [0x2D] = 117, // 6
        },
        [7] = new Dictionary<long, int>
        {
            [0x5C] = 58,  // 7
            [0x5D] = 66,  // 7
            [0x5E] = 67,  // 7
            [0x5F] = 68,  // 7
            [0x60] = 69,  // 7
            [0x61] = 70,  // 7
            [0x62] = 71,  // 7
            [0x63] = 72,  // 7
            [0x64] = 73,  // 7
            [0x65] = 74,  // 7
            [0x66] = 75,  // 7
            [0x67] = 76,  // 7
            [0x68] = 77,  // 7
            [0x69] = 78,  // 7
            [0x6A] = 79,  // 7
            [0x6B] = 80,  // 7
            [0x6C] = 81,  // 7
            [0x6D] = 82,  // 7
            [0x6E] = 83,  // 7
            [0x6F] = 84,  // 7
            [0x70] = 85,  // 7
            [0x71] = 86,  // 7
            [0x72] = 87,  // 7
            [0x73] = 89,  // 7
            [0x74] = 106, // 7
            [0x75] = 107, // 7
            [0x76] = 113, // 7
            [0x77] = 118, // 7
            [0x78] = 119, // 7
            [0x79] = 120, // 7
            [0x7A] = 121, // 7
            [0x7B] = 122, // 7
        },
        [8] = new Dictionary<long, int>
        {
            [0xF8] = 38, // 8
            [0xF9] = 42, // 8
            [0xFA] = 44, // 8
            [0xFB] = 59, // 8
            [0xFC] = 88, // 8
            [0xFD] = 90, // 8
        },
        [10] = new Dictionary<long, int>
        {
            [0x3F8] = 33, // 10
            [0x3F9] = 34, // 10
            [0x3FA] = 40, // 10
            [0x3FB] = 41, // 10
            [0x3FC] = 63, // 10
        },
        [11] = new Dictionary<long, int>
        {
            [0x7FA] = 39,  // 11
            [0x7FB] = 43,  // 11
            [0x7FC] = 124, // 11
        },
        [12] = new Dictionary<long, int>
        {
            [0xFFA] = 35, // 12
            [0xFFB] = 62, // 12
        },
        [13] = new Dictionary<long, int>
        {
            [0x1FF8] = 0,   // 13
            [0x1FF9] = 36,  // 13
            [0x1FFA] = 64,  // 13
            [0x1FFB] = 91,  // 13
            [0x1FFC] = 93,  // 13
            [0x1FFD] = 126, // 13
        },
        [14] = new Dictionary<long, int>
        {
            [0x3FFC] = 94,  // 14
            [0x3FFD] = 125, // 14
        },
        [15] = new Dictionary<long, int>
        {
            [0x7FFC] = 60,  // 15
            [0x7FFD] = 96,  // 15
            [0x7FFE] = 123, // 15
        },
        [19] = new Dictionary<long, int>
        {
            [0x7FFF0] = 92,  // 19
            [0x7FFF1] = 195, // 19
            [0x7FFF2] = 208, // 19
        },
        [20] = new Dictionary<long, int>
        {
            [0xFFFE6] = 128, // 20
            [0xFFFE7] = 130, // 20
            [0xFFFE8] = 131, // 20
            [0xFFFE9] = 162, // 20
            [0xFFFEA] = 184, // 20
            [0xFFFEB] = 194, // 20
            [0xFFFEC] = 224, // 20
            [0xFFFED] = 226, // 20
        },
        [21] = new Dictionary<long, int>
        {
            [0x1FFFDC] = 153, // 21
            [0x1FFFDD] = 161, // 21
            [0x1FFFDE] = 167, // 21
            [0x1FFFDF] = 172, // 21
            [0x1FFFE0] = 176, // 21
            [0x1FFFE1] = 177, // 21
            [0x1FFFE2] = 179, // 21
            [0x1FFFE3] = 209, // 21
            [0x1FFFE4] = 216, // 21
            [0x1FFFE5] = 217, // 21
            [0x1FFFE6] = 227, // 21
            [0x1FFFE7] = 229, // 21
            [0x1FFFE8] = 230, // 21
        },
        [22] = new Dictionary<long, int>
        {
            [0x3FFFD2] = 129, // 22
            [0x3FFFD3] = 132, // 22
            [0x3FFFD4] = 133, // 22
            [0x3FFFD5] = 134, // 22
            [0x3FFFD6] = 136, // 22
            [0x3FFFD7] = 146, // 22
            [0x3FFFD8] = 154, // 22
            [0x3FFFD9] = 156, // 22
            [0x3FFFDA] = 160, // 22
            [0x3FFFDB] = 163, // 22
            [0x3FFFDC] = 164, // 22
            [0x3FFFDD] = 169, // 22
            [0x3FFFDE] = 170, // 22
            [0x3FFFDF] = 173, // 22
            [0x3FFFE0] = 178, // 22
            [0x3FFFE1] = 181, // 22
            [0x3FFFE2] = 185, // 22
            [0x3FFFE3] = 186, // 22
            [0x3FFFE4] = 187, // 22
            [0x3FFFE5] = 189, // 22
            [0x3FFFE6] = 190, // 22
            [0x3FFFE7] = 196, // 22
            [0x3FFFE8] = 198, // 22
            [0x3FFFE9] = 228, // 22
            [0x3FFFEA] = 232, // 22
            [0x3FFFEB] = 233, // 22
        },
        [23] = new Dictionary<long, int>
        {
            [0x7FFFD8] = 1,   // 23
            [0x7FFFD9] = 135, // 23
            [0x7FFFDA] = 137, // 23
            [0x7FFFDB] = 138, // 23
            [0x7FFFDC] = 139, // 23
            [0x7FFFDD] = 140, // 23
            [0x7FFFDE] = 141, // 23
            [0x7FFFDF] = 143, // 23
            [0x7FFFE0] = 147, // 23
            [0x7FFFE1] = 149, // 23
            [0x7FFFE2] = 150, // 23
            [0x7FFFE3] = 151, // 23
            [0x7FFFE4] = 152, // 23
            [0x7FFFE5] = 155, // 23
            [0x7FFFE6] = 157, // 23
            [0x7FFFE7] = 158, // 23
            [0x7FFFE8] = 165, // 23
            [0x7FFFE9] = 166, // 23
            [0x7FFFEA] = 168, // 23
            [0x7FFFEB] = 174, // 23
            [0x7FFFEC] = 175, // 23
            [0x7FFFED] = 180, // 23
            [0x7FFFEE] = 182, // 23
            [0x7FFFEF] = 183, // 23
            [0x7FFFF0] = 188, // 23
            [0x7FFFF1] = 191, // 23
            [0x7FFFF2] = 197, // 23
            [0x7FFFF3] = 231, // 23
            [0x7FFFF4] = 239, // 23
        },
        [24] = new Dictionary<long, int>
        {
            [0xFFFFEA] = 9,   // 24
            [0xFFFFEB] = 142, // 24
            [0xFFFFEC] = 144, // 24
            [0xFFFFED] = 145, // 24
            [0xFFFFEE] = 148, // 24
            [0xFFFFEF] = 159, // 24
            [0xFFFFF0] = 171, // 24
            [0xFFFFF1] = 206, // 24
            [0xFFFFF2] = 215, // 24
            [0xFFFFF3] = 225, // 24
            [0xFFFFF4] = 236, // 24
            [0xFFFFF5] = 237, // 24
        },
        [25] = new Dictionary<long, int>
        {
            [0x1FFFFEC] = 199, // 25
            [0x1FFFFED] = 207, // 25
            [0x1FFFFEE] = 234, // 25
            [0x1FFFFEF] = 235, // 25
        },
        [26] = new Dictionary<long, int>
        {
            [0x3FFFFE0] = 192, // 26
            [0x3FFFFE1] = 193, // 26
            [0x3FFFFE2] = 200, // 26
            [0x3FFFFE3] = 201, // 26
            [0x3FFFFE4] = 202, // 26
            [0x3FFFFE5] = 205, // 26
            [0x3FFFFE6] = 210, // 26
            [0x3FFFFE7] = 213, // 26
            [0x3FFFFE8] = 218, // 26
            [0x3FFFFE9] = 219, // 26
            [0x3FFFFEA] = 238, // 26
            [0x3FFFFEB] = 240, // 26
            [0x3FFFFEC] = 242, // 26
            [0x3FFFFED] = 243, // 26
            [0x3FFFFEE] = 255, // 26
        },
        [27] = new Dictionary<long, int>
        {
            [0x7FFFFDE] = 203, // 27
            [0x7FFFFDF] = 204, // 27
            [0x7FFFFE0] = 211, // 27
            [0x7FFFFE1] = 212, // 27
            [0x7FFFFE2] = 214, // 27
            [0x7FFFFE3] = 221, // 27
            [0x7FFFFE4] = 222, // 27
            [0x7FFFFE5] = 223, // 27
            [0x7FFFFE6] = 241, // 27
            [0x7FFFFE7] = 244, // 27
            [0x7FFFFE8] = 245, // 27
            [0x7FFFFE9] = 246, // 27
            [0x7FFFFEA] = 247, // 27
            [0x7FFFFEB] = 248, // 27
            [0x7FFFFEC] = 250, // 27
            [0x7FFFFED] = 251, // 27
            [0x7FFFFEE] = 252, // 27
            [0x7FFFFEF] = 253, // 27
            [0x7FFFFF0] = 254, // 27
        },
        [28] = new Dictionary<long, int>
        {
            [0xFFFFFE2] = 2,   // 28
            [0xFFFFFE3] = 3,   // 28
            [0xFFFFFE4] = 4,   // 28
            [0xFFFFFE5] = 5,   // 28
            [0xFFFFFE6] = 6,   // 28
            [0xFFFFFE7] = 7,   // 28
            [0xFFFFFE8] = 8,   // 28
            [0xFFFFFE9] = 11,  // 28
            [0xFFFFFEA] = 12,  // 28
            [0xFFFFFEB] = 14,  // 28
            [0xFFFFFEC] = 15,  // 28
            [0xFFFFFED] = 16,  // 28
            [0xFFFFFEE] = 17,  // 28
            [0xFFFFFEF] = 18,  // 28
            [0xFFFFFF0] = 19,  // 28
            [0xFFFFFF1] = 20,  // 28
            [0xFFFFFF2] = 21,  // 28
            [0xFFFFFF3] = 23,  // 28
            [0xFFFFFF4] = 24,  // 28
            [0xFFFFFF5] = 25,  // 28
            [0xFFFFFF6] = 26,  // 28
            [0xFFFFFF7] = 27,  // 28
            [0xFFFFFF8] = 28,  // 28
            [0xFFFFFF9] = 29,  // 28
            [0xFFFFFFA] = 30,  // 28
            [0xFFFFFFB] = 31,  // 28
            [0xFFFFFFC] = 127, // 28
            [0xFFFFFFD] = 220, // 28
            [0xFFFFFFE] = 249, // 28
        },
        [30] = new Dictionary<long, int>
        {
            [0x3FFFFFFC] = 10, // 30
            [0x3FFFFFFD] = 13, // 30
            [0x3FFFFFFE] = 22, // 30
            [0x3FFFFFFF] = -2, // 30
        },
    };
}