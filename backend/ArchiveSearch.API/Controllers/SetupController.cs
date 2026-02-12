using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveSearch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetupController(SetupState setupState, ILogger<SetupController> logger) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new { configured = !setupState.IsSetupMode });
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return BadRequest(new { error = "API key is required." });

        // Validate the key by making a minimal API call
        try
        {
            var testClient = new AnthropicClient { ApiKey = request.ApiKey };
            await testClient.Messages.Create(new MessageCreateParams
            {
                Model = "claude-haiku-4-5-20251001",
                MaxTokens = 10,
                Messages = [new MessageParam { Role = Role.User, Content = "Hi" }]
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "API key validation failed");
            return BadRequest(new { error = "Invalid API key. Please check your key and try again." });
        }

        // Write the key to the local config file
        try
        {
            var config = new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = request.ApiKey };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.GetDirectoryName(setupState.LocalSettingsPath)!;
            Directory.CreateDirectory(dir);
            await System.IO.File.WriteAllTextAsync(setupState.LocalSettingsPath, json);

            // Exit setup mode so subsequent requests work immediately
            setupState.IsSetupMode = false;

            logger.LogInformation("API key saved to {Path}", setupState.LocalSettingsPath);
            return Ok(new { success = true, message = "API key saved. Please restart the application." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save API key");
            return StatusCode(500, new { error = "Failed to save configuration." });
        }
    }
}

public record SetupRequest(string ApiKey);
