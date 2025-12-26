using System.Text.Json.Serialization;

namespace AnthropicToolUseBuffer
{
    public class Usage
    {
        [JsonPropertyName("input_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? input_tokens { get; set; } = null;

        [JsonPropertyName("cache_creation_input_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? cache_creation_input_tokens { get; set; } = null;

        [JsonPropertyName("cache_read_input_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? cache_read_input_tokens { get; set; } = null;

        [JsonPropertyName("output_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? output_tokens { get; set; } = null;

        [JsonPropertyName("cache_creation")]
        public CacheCreation? cache_creation { get; set; }
    }
}
