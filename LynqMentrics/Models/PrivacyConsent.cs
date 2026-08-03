using System.ComponentModel.DataAnnotations;

namespace LynqMentrics.Models;

/// <summary>
/// Records a GDPR/CCPA consent decision for audit purposes. Consent records are
/// the evidential basis that user data was processed with valid consent.
/// </summary>
public class PrivacyConsent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who granted (or withdrew) consent, when authenticated.</summary>
    [MaxLength(128)]
    public string? UserId { get; set; }

    /// <summary>Hashed IP of the visitor, for unauthenticated consent decisions.</summary>
    [MaxLength(128)]
    public string? IpHash { get; set; }

    /// <summary>Type of consent, e.g. "analytics" or "necessary".</summary>
    [Required]
    [MaxLength(64)]
    public string ConsentType { get; set; } = string.Empty;

    /// <summary>Whether consent was granted (true) or withdrawn (false).</summary>
    public bool Granted { get; set; }

    /// <summary>Version of the consent/notice text that was shown to the user.</summary>
    [MaxLength(32)]
    public string ConsentVersion { get; set; } = "1.0";

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User agent string captured at the time of the decision (informational).</summary>
    [MaxLength(512)]
    public string? UserAgent { get; set; }
}
