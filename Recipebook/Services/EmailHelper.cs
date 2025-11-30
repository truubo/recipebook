using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net.Mail;
using System.Net;

namespace Recipebook.Services
{
    public class EmailHelper : IEmailSender
    {
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            using var client = new SmtpClient("mail.lotta.red", 587)
            {
                Credentials = new NetworkCredential("recipebook@lotta.red", "insert_password_here"), // todo: use secret store
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress("recipebook@lotta.red"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            mail.To.Add(email);

            await client.SendMailAsync(mail);
        }
    }
}
