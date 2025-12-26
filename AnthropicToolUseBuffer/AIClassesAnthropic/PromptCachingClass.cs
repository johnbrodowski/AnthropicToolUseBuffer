//using Newtonsoft.Json;
//[JsonProperty("type")]


using System.Text.Json.Serialization;

namespace AnthropicToolUseBuffer
{
    public class CacheControl
    {
        [JsonPropertyName("type")]
        public string? type { get; set; } = "ephemeral";

        [JsonPropertyName("ttl")]
        public string? ttl { get; set; } = "5m"; //  "5m" for 5 minutes, "1h" for 1 hour
    }

    public class CacheCreation
    {
        [JsonPropertyName("ephemeral_1h_input_tokens")]
        public int? Ephemeral1hInputTokens { get; set; }

        [JsonPropertyName("ephemeral_5m_input_tokens")]
        public int? Ephemeral5mInputTokens { get; set; }
    }
}