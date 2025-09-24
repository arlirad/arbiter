using System;
using System.Dynamic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Arbiter;

public class Account : ISlottable
{
    public Guid Guid;   // +0, 16 bytes 
                        // +16, 4 bytes - Name length (actually 1 byte)
    public string Name; // +20, 128 bytes
    public byte[] Salt; // +148, 16 bytes
    public byte[] Hash; // +164, 64 bytes
    public uint Flags;  // +228, 4 bytes

    static readonly byte[] Zeroes = new byte[256];

    public Account()
    {

    }

    public Account(Guid guid, string name, string password)
    {
        Salt = new byte[16];
        Random.Shared.NextBytes(Salt);

        Guid = guid;
        Name = name;
        SetPassword(password);
    }

    public void SetPassword(string pass)
    {
        Hash = HashPassword(pass);
    }

    public bool CheckPassword(string pass)
    {
        var challenger = HashPassword(pass);

        for (int i = 0; i < 64; i++)
            if (challenger[i] != Hash[i])
                return false;

        return true;
    }

    private byte[] HashPassword(string pass)
    {
        pass = Name + pass + Name + pass + Name;
        return new Rfc2898DeriveBytes(pass, Salt, 1000).GetBytes(64);
    }

    public async Task Serialize(Stream destination)
    {
        await destination.WriteAsync(Guid.ToByteArray());

        int nameBytes = Encoding.UTF8.GetByteCount(Name);

        if (nameBytes >= 128)
            throw new IndexOutOfRangeException();

        byte[] buffer = new byte[4] { (byte)nameBytes, 0, 0, 0 };

        await destination.WriteAsync(buffer);
        await destination.WriteAsync(Encoding.UTF8.GetBytes(Name));
        await destination.WriteAsync(Zeroes, 0, 128 - nameBytes);
        await destination.WriteAsync(Salt);
        await destination.WriteAsync(Hash);

        buffer[0] = (byte)(Flags);
        buffer[1] = (byte)(Flags >> 8);
        buffer[2] = (byte)(Flags >> 16);
        buffer[3] = (byte)(Flags >> 24);

        await destination.WriteAsync(buffer);
        await destination.WriteAsync(Zeroes, 0, 24);
    }

    public async Task Deserialize(Stream source)
    {
        byte[] guidBuffer = new byte[16];
        byte[] buffer = new byte[240];

        Salt = new byte[16];
        Hash = new byte[64];

        await source.ReadAsync(guidBuffer, 0, 16);
        await source.ReadAsync(buffer, 0, 240);

        Guid = new Guid(guidBuffer);
        Name = Encoding.UTF8.GetString(buffer, 4, buffer[0]);

        Array.Copy(buffer, 132, Salt, 0, Salt.Length);
        Array.Copy(buffer, 148, Hash, 0, Hash.Length);

        Flags = ((uint)buffer[228]) << 0 | ((uint)buffer[229]) << 8 | ((uint)buffer[230]) << 16 | ((uint)buffer[231]) << 24;
    }
}

public class AccountToken : ISlottable
{
    public byte[] Token = new byte[104];
    public Guid Guid;

    public string ToCookieValue()
    {
        return Convert.ToHexString(Token);
    }

    public async Task Serialize(Stream destination)
    {
        await destination.WriteAsync(Token);
        await destination.WriteAsync(Token, 0, 8);
        await destination.WriteAsync(Guid.ToByteArray());
    }

    public async Task Deserialize(Stream source)
    {
        byte[] guidBuffer = new byte[16];

        await source.ReadAsync(Token, 0, 104);
        await source.ReadAsync(guidBuffer, 0, 8); // possible expiry date
        await source.ReadAsync(guidBuffer, 0, 16);

        Guid = new Guid(guidBuffer);
    }
}