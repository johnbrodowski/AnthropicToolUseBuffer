using Newtonsoft.Json;

namespace AnthropicToolUseBuffer
{
    public class Error
    {
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string? type { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string? message { get; set; }
    }
}
