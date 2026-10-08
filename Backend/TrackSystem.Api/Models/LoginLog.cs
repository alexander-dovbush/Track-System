// Library that gives us the [Key] attribute
using System.ComponentModel.DataAnnotations;

// Folder/namespace this class belongs to -> TrackSystem.Api.Models
namespace TrackSystem.Api.Models;

// One LoginLog object = one row in dbo.LoginLogs (one login attempt)
public class LoginLog
{
    // [Key] -> logID is the primary key (EF can't guess it: it's not "Id" or "LoginLogId")
    [Key]
    // logID (int, auto-numbered by the DB) -> LogID
    public int LogID { get; set; }

    // eNum (int, required) -> ENum   (the typed number, no FK - option B)
    public int ENum { get; set; }

    // loginDate (datetime, required) -> LoginDate   (Israel time)
    public DateTime LoginDate { get; set; }

    // ipAddress (nvarchar 45, can be NULL) -> IpAddress
    public string? IpAddress { get; set; }

    // success (bit = true/false, required) -> Success
    public bool Success { get; set; }
}