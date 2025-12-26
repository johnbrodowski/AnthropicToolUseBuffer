using System.Text.Json.Serialization;


namespace AnthropicToolUseBuffer
{
    public class Delta
    {
        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? type { get; set; }

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? text { get; set; }

        [JsonPropertyName("partial_json")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? partial_json { get; set; }

        [JsonPropertyName("stop_reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? stop_reason { get; set; }

        [JsonPropertyName("stop_sequence")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? stop_sequence { get; set; }

        [JsonPropertyName("usage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Usage? usage { get; set; }

        [JsonPropertyName("thinking")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? thinking { get; set; }

        [JsonPropertyName("signature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? signature { get; set; }

    }
}
