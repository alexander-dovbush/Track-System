// Library that gives us the [Key] attribute
using System.ComponentModel.DataAnnotations;

// Folder/namespace this class belongs to -> TrackSystem.Api.Models
namespace TrackSystem.Api.Models;

// One Employee object = one row in dbo.Employees
public class Employee
{
    // [Key] tells EF Core: eNum is the primary key (the name isn't "Id", so EF can't guess it)
    [Key]
    // eNum (int, auto-numbered by the DB) -> ENum
    public int ENum { get; set; }

    // firstName (nvarchar 50, required) -> FirstName
    public string FirstName { get; set; } = string.Empty;

    // lastName (nvarchar 50, required) -> LastName
    public string LastName { get; set; } = string.Empty;

    // email (nvarchar 100, required, unique) -> Email
    public string Email { get; set; } = string.Empty;

    // password (nvarchar 255, required) -> Password  (used later for login, never sent to the browser)
    public string Password { get; set; } = string.Empty;

    // position (nvarchar 100, can be NULL) -> Position   (the ? means "may be empty")
    public string? Position { get; set; }

    // role (nvarchar 20, required, DB default 'Employee') -> Role
    public string Role { get; set; } = string.Empty;

    // userName (nvarchar 50, required, unique) -> UserName
    public string UserName { get; set; } = string.Empty;

    // teamNum (int, can be NULL, links to Teams) -> TeamNum
    public int? TeamNum { get; set; }

    // status (nvarchar 20, required, DB default 'pending') -> Status
    public string Status { get; set; } = string.Empty;

    // isFirstLogin (bit = true/false, required) -> IsFirstLogin
    public bool IsFirstLogin { get; set; }

    // failedLoginAttempts (int, required) -> FailedLoginAttempts
    public int FailedLoginAttempts { get; set; }

    // lockoutEnd (datetime, can be NULL) -> LockoutEnd
    public DateTime? LockoutEnd { get; set; }
}