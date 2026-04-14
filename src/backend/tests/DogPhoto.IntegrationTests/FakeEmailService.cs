using System.Collections.Concurrent;
using DogPhoto.SharedKernel.Email;

namespace DogPhoto.IntegrationTests;

/// <summary>
/// In-memory email service for integration tests.
/// Captures all sent emails so tests can assert on recipients, subjects, and content.
/// </summary>
public sealed class FakeEmailService : IEmailService
{
    public ConcurrentBag<SentEmail> SentEmails { get; } = [];

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        SentEmails.Add(new SentEmail(to, subject, htmlBody));
        return Task.CompletedTask;
    }

    public void Clear() => SentEmails.Clear();

    public record SentEmail(string To, string Subject, string HtmlBody);
}
