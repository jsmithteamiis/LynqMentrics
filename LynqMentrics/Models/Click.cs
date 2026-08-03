using System.ComponentModel.DataAnnotations;

namespace LynqMentrics.Models;

public class Click
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LinkId { get; set; }
    public Link? Link { get; set; }

    public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(4096)]
    public string? Referrer { get; set; }

    [MaxLength(2048)]
    public string? UserAgent { get; set; }

    [MaxLength(128)]
    public string? IpHash { get; set; }

    [MaxLength(128)]
    public string? Country { get; set; }

    [MaxLength(64)]
    public string? Device { get; set; }

    [MaxLength(64)]
    public string? Browser { get; set; }
}
