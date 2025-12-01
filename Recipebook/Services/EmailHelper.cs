using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net.Mail;
using System.Net;
using Recipebook.Models;
using Microsoft.Extensions.Options;

namespace Recipebook.Services
{
    public class EmailHelper : IEmailSender
    {
        private readonly SmtpOptions _options;

        public EmailHelper(IOptions<SmtpOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            using SmtpClient client = new SmtpClient(_options.Host, 587)
            {
                Credentials = new NetworkCredential(_options.Username, _options.Password),
                EnableSsl = _options.EnableSsl
            };

            MailMessage mail = new MailMessage
            {
                From = new MailAddress(_options.Username),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            mail.To.Add(email);

            await client.SendMailAsync(mail);
        }
    }
}
