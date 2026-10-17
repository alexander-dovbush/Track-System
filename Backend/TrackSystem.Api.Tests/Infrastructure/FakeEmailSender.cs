// ConcurrentQueue -> a list that is safe if two requests add at the same time
using System.Collections.Concurrent;
// IEmailSender -> the contract the API uses
using TrackSystem.Api.Services;

namespace TrackSystem.Api.Tests.Infrastructure;

// test-only sender: sends nothing, just remembers every email -> tests read the link from it
public class FakeEmailSender : IEmailSender
{
    // every "sent" email, oldest first -> (To, Subject, HtmlBody)
    public ConcurrentQueue<(string To, string Subject, string HtmlBody)> Sent { get; } = new();

    // to, subject, htmlBody -> added to Sent; finished at once (no network)
    public Task SendAsync(string to, string subject, string htmlBody)
    {
        Sent.Enqueue((to, subject, htmlBody));
        return Task.CompletedTask;
    }
}