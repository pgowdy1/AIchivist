using System.ComponentModel.DataAnnotations;

namespace ArchiveSearch.Core.Models;

public class ChatMessage
{
    [Required]
    public string Role { get; set; } = string.Empty; // "user" or "assistant"

    [Required]
    public string Content { get; set; } = string.Empty;
}

public class ChatRequest
{
    [Required]
    public List<ChatMessage> Messages { get; set; } = [];

    /// <summary>Context ID from the original search response, used to load result context.</summary>
    [Required]
    public string ContextId { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
}
