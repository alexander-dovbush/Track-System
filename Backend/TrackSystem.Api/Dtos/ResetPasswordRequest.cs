namespace TrackSystem.Api.Dtos;

// body {"token":"a3f0...","newPassword":"..."} -> ResetPasswordRequest (missing fields -> null, checked in the endpoint)
public record ResetPasswordRequest(string Token, string NewPassword);