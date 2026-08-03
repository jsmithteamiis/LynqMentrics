using System.Security.Cryptography;
using System.Text;

namespace LynqMentrics.Services;

/// <summary>
/// Tokenizes (encrypts) personally identifiable information (PII) at rest using AES-GCM.
/// PII fields such as original URLs, referrers, and user agents are stored as opaque
/// tokens in the database and detokenized (decrypted) only when needed at runtime.
/// </summary>
/// <remarks>
/// Tokens are prefixed with <c>tok1:</c> so that legacy plaintext values already stored
/// in the database can be detected and passed through unchanged. This makes the rollout
/// of tokenization non-breaking for existing data.
/// </remarks>
public class PiiTokenizationService
{
    public const string TokenPrefix = "tok1:";
    private const int KeySizeBytes = 32; // AES-256
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public PiiTokenizationService(IConfiguration configuration)
    {
        var keyHex = configuration["PiiEncryptionKey"];
        if (string.IsNullOrWhiteSpace(keyHex))
        {
            throw new InvalidOperationException(
                "PiiEncryptionKey is not configured. Add a 64-character hex key to appsettings.json.");
        }

        _key = Convert.FromHexString(keyHex);
        if (_key.Length != KeySizeBytes)
        {
            throw new InvalidOperationException(
                "PiiEncryptionKey must be exactly 32 bytes (64 hexadecimal characters).");
        }
    }

    /// <summary>
    /// Encrypts a plaintext value into a token. Returns null for null/whitespace input
    /// and returns already-tokenized values untouched (idempotent).
    /// </summary>
    public string? Tokenize(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        if (IsTokenized(plaintext))
        {
            return plaintext;
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var result = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSizeBytes + TagSizeBytes, ciphertext.Length);

        return TokenPrefix + Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a token back to its original plaintext. Returns null for null/empty input
    /// and passes legacy plaintext values through unchanged.
    /// </summary>
    public string? Detokenize(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        if (!IsTokenized(token))
        {
            return token;
        }

        try
        {
            var data = Convert.FromBase64String(token[TokenPrefix.Length..]);
            if (data.Length < NonceSizeBytes + TagSizeBytes)
            {
                return null;
            }

            var nonce = data.AsSpan(0, NonceSizeBytes).ToArray();
            var tag = data.AsSpan(NonceSizeBytes, TagSizeBytes).ToArray();
            var ciphertext = data.AsSpan(NonceSizeBytes + TagSizeBytes).ToArray();

            var plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(_key, TagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException)
        {
            // The token cannot be decrypted (e.g. the key was rotated). Treat the
            // value as unavailable rather than crashing the request.
            return null;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the value is a token managed by this service.
    /// </summary>
    public static bool IsTokenized(string value) =>
        value.StartsWith(TokenPrefix, StringComparison.Ordinal);
}