using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ltwnc.Services.Auth;

public interface IEmailMessageSender
{
    Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}

public sealed class SmtpEmailMessageSender : IEmailMessageSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailMessageSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.UserName) ||
            string.IsNullOrWhiteSpace(_options.Password) ||
            string.IsNullOrWhiteSpace(_options.From))
        {
            throw new InvalidOperationException("SMTP chưa được cấu hình đầy đủ.");
        }

        using var message = new MailMessage(_options.From, recipient, subject, body);
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = new NetworkCredential(_options.UserName, _options.Password)
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
