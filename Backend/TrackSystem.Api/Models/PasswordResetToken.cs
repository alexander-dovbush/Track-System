// [Key], [ForeignKey], [Column] attributes
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
// [Index] attribute (part of EF Core)
using Microsoft.EntityFrameworkCore;

namespace TrackSystem.Api.Models;

// TokenHash -> unique index (same as UQ_ in SQL; the test DB gets it too)
[Index(nameof(TokenHash), IsUnique = true)]
// One object = one row in dbo.PasswordResetTokens (one reset link)
public class PasswordResetToken
{
    // tokenID (int, auto-numbered) -> primary key
    [Key]
    public int TokenID { get; set; }

    // eNum (int, required) -> whose link it is
    public int ENum { get; set; }

    // ENum -> the Employee row (FK, cascade delete in the test DB too)
    [ForeignKey(nameof(ENum))]
    public Employee? Employee { get; set; }

    // tokenHash char(64) -> SHA-256 hex of the emailed token
    [Column(TypeName = "char(64)")]
    public string TokenHash { get; set; } = "";

    // expiresAt (UTC) -> CreatedAt + 30 min
    public DateTime ExpiresAt { get; set; }

    // usedAt (UTC, NULL = still usable) -> set when used or replaced
    public DateTime? UsedAt { get; set; }

    // createdAt (UTC) -> when the link was made
    public DateTime CreatedAt { get; set; }
}