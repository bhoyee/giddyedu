using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GiddyEdu.Infrastructure.Messaging;

public sealed class SmtpOptions
{
    public const string SectionName = "SMTP";
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Security { get; init; } = "StartTls";
    public bool Authentication { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "GiddyEdu";

    public SecureSocketOptions SocketOptions => Security.Trim().ToLowerInvariant() switch
    {
        "none" => SecureSocketOptions.None,
        "ssl" or "sslontconnect" => SecureSocketOptions.SslOnConnect,
        "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
        "auto" => SecureSocketOptions.Auto,
        _ => SecureSocketOptions.StartTls
    };
}

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default);
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions settings = options.Value;

    public async Task SendAsync(string recipient, string subject, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default)
    {
        ValidateSettings(settings);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.SocketOptions, cancellationToken);
        if (settings.Authentication) await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    internal static void ValidateSettings(SmtpOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Host)) throw new InvalidOperationException("SMTP host is required.");
        if (settings.Port is < 1 or > 65535) throw new InvalidOperationException("SMTP port is invalid.");
        if (string.IsNullOrWhiteSpace(settings.FromEmail)) throw new InvalidOperationException("SMTP sender address is required.");
        if (settings.Authentication && (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password)))
            throw new InvalidOperationException("SMTP credentials are required when authentication is enabled.");
    }
}

public sealed class SmtpHealthCheck(IOptions<SmtpOptions> options) : IHealthCheck
{
    private readonly SmtpOptions settings = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            SmtpEmailSender.ValidateSettings(settings);
            using var client = new SmtpClient();
            await client.ConnectAsync(settings.Host, settings.Port, settings.SocketOptions, cancellationToken);
            if (settings.Authentication) await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return HealthCheckResult.Healthy("SMTP connection and authentication succeeded.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SMTP connection or authentication failed.", exception);
        }
    }
}
