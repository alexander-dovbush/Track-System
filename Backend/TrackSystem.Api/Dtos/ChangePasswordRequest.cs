// the "address" of this class -> Program.cs already has: using TrackSystem.Api.Dtos;
namespace TrackSystem.Api.Dtos;

// JSON body {"currentPassword":"...","newPassword":"..."} -> object with CurrentPassword and NewPassword
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);