namespace TrackSystem.Api.Dtos;

// body {"eNum": 5} -> ForgotPasswordRequest with ENum = 5 (missing -> 0 = unknown, same answer)
public record ForgotPasswordRequest(int ENum);