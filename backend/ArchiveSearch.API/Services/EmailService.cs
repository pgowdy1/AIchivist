using System.Text;
using ArchiveSearch.Core.Models;
using MailKit.Net.Smtp;
using MimeKit;

namespace ArchiveSearch.API.Services;

public class EmailService(IConfiguration configuration, ILogger<EmailService> logger)
{
    public async Task<bool> SendCollectionRequestAsync(CollectionRequestSubmission request)
    {
        var smtpHost = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            logger.LogWarning("Email not configured (SmtpHost is empty). Skipping email send.");
            return false;
        }

        try
        {
            var smtpPort = configuration.GetValue("Email:SmtpPort", 587);
            var smtpUsername = configuration["Email:SmtpUsername"] ?? "";
            var smtpPassword = configuration["Email:SmtpPassword"] ?? "";
            var fromAddress = configuration["Email:FromAddress"] ?? "aichivist@wsu.edu";
            var archivistAddress = configuration["Email:ArchivistAddress"] ?? "archives@wsu.edu";
            var useSsl = configuration.GetValue("Email:UseSsl", true);

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(fromAddress));
            message.To.Add(MailboxAddress.Parse(archivistAddress));
            message.Subject = $"Collection Request from {request.RequesterName}";
            message.Body = new TextPart("plain") { Text = BuildEmailBody(request) };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, useSsl);

            if (!string.IsNullOrWhiteSpace(smtpUsername))
                await client.AuthenticateAsync(smtpUsername, smtpPassword);

            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);

            logger.LogInformation("Collection request email sent for {Name} ({Email})",
                request.RequesterName, request.RequesterEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send collection request email for {Name} ({Email})",
                request.RequesterName, request.RequesterEmail);
            return false;
        }
    }

    private static string BuildEmailBody(CollectionRequestSubmission request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("COLLECTION REQUEST");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();
        sb.AppendLine($"Name:  {request.RequesterName}");
        sb.AppendLine($"Email: {request.RequesterEmail}");

        if (!string.IsNullOrWhiteSpace(request.RequesterPhone))
            sb.AppendLine($"Phone: {request.RequesterPhone}");

        if (!string.IsNullOrWhiteSpace(request.Affiliation))
            sb.AppendLine($"Affiliation: {request.Affiliation}");

        sb.AppendLine();
        sb.AppendLine($"REQUESTED COLLECTIONS ({request.Collections.Count})");
        sb.AppendLine(new string('-', 50));

        foreach (var collection in request.Collections)
        {
            sb.AppendLine();
            sb.AppendLine($"  Unit ID:    {collection.CollectionUnitId}");
            sb.AppendLine($"  Title:      {collection.Title}");

            if (!string.IsNullOrWhiteSpace(collection.Repository))
                sb.AppendLine($"  Repository: {collection.Repository}");

            if (!string.IsNullOrWhiteSpace(collection.DateRange))
                sb.AppendLine($"  Dates:      {collection.DateRange}");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            sb.AppendLine();
            sb.AppendLine("NOTES");
            sb.AppendLine(new string('-', 50));
            sb.AppendLine(request.Notes);
        }

        sb.AppendLine();
        sb.AppendLine(new string('=', 50));
        sb.AppendLine("Sent by AIchivist — WSU Archive Search Tool");

        return sb.ToString();
    }
}
