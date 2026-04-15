using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PetelATH.Api.Configuration;

namespace PetelATH.Api.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendOtpAsync(string toEmail, string code, string userName)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("מערכת פטל", _settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "קוד אימות - מערכת ניהול אגרות";

            var displayName = string.IsNullOrWhiteSpace(userName) ? "" : $" {userName}";

            message.Body = new TextPart("html")
            {
                Text = $"""
                    <div dir="rtl" style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px; border: 1px solid #dee2e6; border-radius: 8px;">
                        <h2 style="color: #333; margin-bottom: 8px;">קוד האימות שלך</h2>
                        <p style="color: #555;">שלום{displayName},</p>
                        <p style="color: #555;">השתמש בקוד הבא להתחברות למערכת:</p>
                        <div style="background: #f5f5f5; border-radius: 6px; padding: 20px; text-align: center; margin: 20px 0;">
                            <span style="font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #1a73e8; font-family: monospace;">{code}</span>
                        </div>
                        <p style="color: #888; font-size: 13px;">הקוד תקף ל-10 דקות. אל תשתף אותו עם אף אחד.</p>
                        <p style="color: #aaa; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 12px;">
                            אם לא ניסית להתחבר, התעלם מהודעה זו.
                        </p>
                    </div>
                    """
            };

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
                await client.SendAsync(message);
                _logger.LogInformation("OTP email sent to {Email}", MaskEmail(toEmail));
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }

        public static string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            var at = email.IndexOf('@');
            if (at <= 1) return email;
            return email[0] + new string('*', Math.Min(at - 1, 3)) + email[at..];
        }
    }
}
