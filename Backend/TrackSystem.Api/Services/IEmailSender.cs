// Folder/namespace -> TrackSystem.Api.Services
namespace TrackSystem.Api.Services;

// Contract: "something that can send an email" (real = SMTP, tests = fake)
public interface IEmailSender
{
    // to (address), subject, htmlBody (HTML text) -> Task that ends when the mail is sent
    Task SendAsync(string to, string subject, string htmlBody);
}