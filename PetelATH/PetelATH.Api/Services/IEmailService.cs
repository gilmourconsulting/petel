namespace PetelATH.Api.Services
{
    public interface IEmailService
    {
        Task SendOtpAsync(string toEmail, string code, string userName);
        Task SendPasswordChangedAsync(string toEmail, string userName);
    }
}
