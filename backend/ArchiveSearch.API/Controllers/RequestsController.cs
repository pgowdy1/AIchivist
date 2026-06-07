using System.Text.Json;
using ArchiveSearch.API.Services;
using ArchiveSearch.Core.Models;
using ArchiveSearch.Data;
using ArchiveSearch.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveSearch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RequestsController(
    ArchiveContext context,
    EmailService emailService,
    ILogger<RequestsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CollectionRequestResponse>> Submit(
        [FromBody] CollectionRequestSubmission request)
    {
        if (string.IsNullOrWhiteSpace(request.RequesterName))
            return BadRequest(new { error = "Requester name is required." });

        if (string.IsNullOrWhiteSpace(request.RequesterEmail))
            return BadRequest(new { error = "Requester email is required." });

        if (request.Collections is not { Count: > 0 })
            return BadRequest(new { error = "At least one collection must be selected." });

        try
        {
            var entity = new CollectionRequestEntity
            {
                RequesterName = request.RequesterName,
                RequesterEmail = request.RequesterEmail,
                RequesterPhone = request.RequesterPhone,
                Affiliation = request.Affiliation,
                Notes = request.Notes,
                CollectionsJson = JsonSerializer.Serialize(request.Collections),
                SubmittedAt = DateTime.UtcNow,
                EmailSent = false
            };

            context.CollectionRequests.Add(entity);
            await context.SaveChangesAsync();

            var emailSent = await emailService.SendCollectionRequestAsync(request);
            if (emailSent != entity.EmailSent)
            {
                entity.EmailSent = emailSent;
                await context.SaveChangesAsync();
            }

            logger.LogInformation(
                "Collection request submitted by {Name} ({Email}) for {Count} collections (email sent: {EmailSent})",
                request.RequesterName, request.RequesterEmail, request.Collections.Count, emailSent);

            return Ok(new CollectionRequestResponse
            {
                Success = true,
                EmailSent = emailSent,
                Message = emailSent
                    ? null
                    : "Your request was saved but could not be emailed. Please contact the archives directly."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit collection request for {Name} ({Email})",
                request.RequesterName, request.RequesterEmail);
            return StatusCode(500, new { error = "Failed to submit request. Please try again." });
        }
    }
}
