namespace RoboCare.UGS
{
using System;
using System.Text;

public static class ApiKeyProvider
{
    // XOR 마스킹된 바이트 배열
    private static readonly byte[] Encoded = new byte[]
    {
        27,10,19,5,25,59,54,54,5,63,63,63,109,109,59,109,
        110,119,98,98,98,109,98,42,99,119,110,41,102,99,109,
        42,109,98,109,103,99,98,110,119,109,110,119,109
    };

    private const byte Mask = 0x5A; // 마스킹 값

    public static string GetKey()
    {
        byte[] buf = new byte[Encoded.Length];
        for (int i = 0; i < Encoded.Length; i++)
            buf[i] = (byte)(Encoded[i] ^ Mask);

        string key = Encoding.UTF8.GetString(buf);

        // 메모리 흔적 제거
        Array.Clear(buf, 0, buf.Length);

        return key;
    }
}
}
