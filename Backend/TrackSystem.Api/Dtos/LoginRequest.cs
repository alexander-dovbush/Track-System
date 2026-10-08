// the "address" of this class, so Program.cs can find it with: using TrackSystem.Api.Dtos;
namespace TrackSystem.Api.Dtos;

// JSON body {"eNum":1,"password":"..."} -> LoginRequest object with ENum and Password
public record LoginRequest(int ENum, string Password);