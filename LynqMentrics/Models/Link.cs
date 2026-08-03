using System.ComponentModel.DataAnnotations;

namespace LynqMentrics.Models;

public class Link
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    [Required]
    [MaxLength(64)]
    public string ShortCode { get; set; } = string.Empty;

    [Required]
    // Sized to hold a tokenized URL: the AES-GCM token (base64 + "tok1:" prefix)
    // is larger than the plaintext, so the column is wider than the input limit.
    [MaxLength(4096)]
    public string OriginalUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Click> Clicks { get; set; } = new List<Click>();
}
