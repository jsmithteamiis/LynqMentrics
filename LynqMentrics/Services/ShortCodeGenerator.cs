using System.Security.Cryptography;

namespace LynqMentrics.Services;

public class ShortCodeGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public string Generate(int length = 6)
    {
        Span<char> buffer = stackalloc char[length];
        byte[] randomBytes = new byte[length];

        RandomNumberGenerator.Fill(randomBytes);

        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[randomBytes[i] % Alphabet.Length];
        }

        return new string(buffer);
    }
}
