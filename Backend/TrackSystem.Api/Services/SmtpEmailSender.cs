// MimeKit -> builds the email (MimeMessage, MailboxAddress, TextPart)
using MimeKit;
// MailKit's SMTP client -> connects to the mail server and sends
using MailKit.Net.Smtp;
// SecureSocketOptions -> None (Mailpit) or StartTls (Gmail)
using MailKit.Security;

namespace TrackSystem.Api.Services;

// Real sender: implements the IEmailSender contract
public class SmtpEmailSender : IEmailSender
{
    // all settings (appsettings.json + User Secrets) -> kept for SendAsync
    private readonly IConfiguration _config;

    // config (given by ASP.NET automatically) -> stored in _config
    public SmtpEmailSender(IConfiguration config)
    {
        _config = config;
    }

    // to, subject, htmlBody -> one email sent (sending is added in 6.4e)
    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        // empty email object
        var message = new MimeMessage();
        // Email:FromAddress missing -> clear error now, instead of a strange MailKit crash
        var fromAddress = _config["Email:FromAddress"] ?? throw new InvalidOperationException("Email:FromAddress is missing in settings.");
        message.From.Add(new MailboxAddress(_config["Email:FromName"], fromAddress));
        // "x@y.com" -> recipient (throws if the address is invalid)
        message.To.Add(MailboxAddress.Parse(to));
        // subject line
        message.Subject = subject;
        // htmlBody -> HTML body, so the link is clickable
        message.Body = new TextPart("html") { Text = htmlBody };
        
        // new SMTP connection object; "using" -> closed/freed at the end of the method
        using var client = new SmtpClient();
        // Email:UseStartTls true -> encrypted (StartTls), false -> plain (None)
        var security = _config.GetValue<bool>("Email:UseStartTls") ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        // Email:SmtpHost missing -> clear error
        var host = _config["Email:SmtpHost"] ?? throw new InvalidOperationException("Email:SmtpHost is missing in settings.");
        // host + port + security -> open the connection (error if the server is down)
        await client.ConnectAsync(host, _config.GetValue<int>("Email:SmtpPort"), security);

        // Email:UserName set (Gmail) -> log in; empty (Mailpit) -> skip
        var userName = _config["Email:UserName"];
        if (!string.IsNullOrEmpty(userName))
        {
            // username set but no password -> clear error (password lives in User Secrets)
            var password = _config["Email:Password"] ?? throw new InvalidOperationException("Email:Password is missing (User Secrets).");
            await client.AuthenticateAsync(userName, password);
        }

        // message -> delivered to the server
        await client.SendAsync(message);
        // true -> say goodbye politely (QUIT) and close
        await client.DisconnectAsync(true);
    }
}