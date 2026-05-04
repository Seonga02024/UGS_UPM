using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RoboCare.UGS
{
    public static class UgsCredentialGenerator
{
    private const string UsernamePrefix = "u";
    private const int UsernameMaxLength = 18;
    private const int PasswordLength = 20;

    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string Symbols = ".-_@!#$%^&*";
    private const string AllPasswordChars = Upper + Lower + Digits + Symbols;

    public static string CreateUsername(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Value cannot be null or empty.", nameof(raw));

        string hex = ToHex(Sha256("ugs-user:" + raw));
        return UsernamePrefix + hex.Substring(0, UsernameMaxLength - UsernamePrefix.Length);
    }

    public static string CreatePassword(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Value cannot be null or empty.", nameof(raw));

        byte[] seed = Sha256("ugs-pass:" + raw);
        char[] password = new char[PasswordLength];

        // UGS password rules: at least 1 upper, 1 lower, 1 digit, 1 symbol
        password[0] = Pick(Upper, seed[0]);
        password[1] = Pick(Lower, seed[1]);
        password[2] = Pick(Digits, seed[2]);
        password[3] = Pick(Symbols, seed[3]);

        for (int i = 4; i < PasswordLength; i++)
        {
            password[i] = Pick(AllPasswordChars, seed[i % seed.Length]);
        }

        DeterministicShuffle(password, seed);
        return new string(password);
    }

    public static (string Username, string Password) CreateCredentials(string raw)
    {
        return (CreateUsername(raw), CreatePassword(raw));
    }

    private static byte[] Sha256(string value)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static char Pick(string chars, byte value)
    {
        return chars[value % chars.Length];
    }

    private static void DeterministicShuffle(char[] array, byte[] seed)
    {
        int seedIndex = 0;
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = seed[seedIndex % seed.Length] % (i + 1);
            (array[i], array[j]) = (array[j], array[i]);
            seedIndex++;
        }
    }
}
}
