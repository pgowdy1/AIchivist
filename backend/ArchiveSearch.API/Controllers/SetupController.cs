using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Models.Messages;
using ArchiveSearch.API.Models;
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
            // Read existing config to preserve connection string written by installer
            var node = new JsonObject();
            if (System.IO.File.Exists(setupState.LocalSettingsPath))
            {
                var existingJson = await System.IO.File.ReadAllTextAsync(setupState.LocalSettingsPath);
                node = JsonNode.Parse(existingJson)?.AsObject() ?? new JsonObject();
            }

            node["ANTHROPIC_API_KEY"] = request.ApiKey;

            var json = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.GetDirectoryName(setupState.LocalSettingsPath)!;
            Directory.CreateDirectory(dir);
            await System.IO.File.WriteAllTextAsync(setupState.LocalSettingsPath, json);

            // Exit setup mode — the scoped AnthropicClient factory will pick up the
            // new key from config (reloadOnChange: true) on subsequent requests.
            setupState.IsSetupMode = false;

            logger.LogInformation("API key saved to {Path}", setupState.LocalSettingsPath);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save API key");
            return StatusCode(500, new { error = "Failed to save configuration." });
        }
    }
}

public record SetupRequest(string ApiKey);
