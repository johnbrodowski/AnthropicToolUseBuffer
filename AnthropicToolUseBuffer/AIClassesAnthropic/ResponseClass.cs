using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
 


namespace AnthropicToolUseBuffer
{


    public class AnthropicResponse
    {
        [JsonProperty("id")]
        public string id { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string type { get; set; } = string.Empty;

        [JsonProperty("role")]
        public string role { get; set; } = string.Empty;

        [JsonProperty("model")]
        public string? model { get; set; }

        [JsonProperty("content"/*, NullValueHandling = NullValueHandling.Ignore*/)]
        public List<Content> content { get; set; } = new List<Content>();

        [JsonProperty("stop_reason")]
        public string? stop_reason { get; set; }

        [JsonProperty("stop_sequence")]
        public string? stop_sequence { get; set; }

        [JsonProperty("usage")]
        public Usage? usage { get; set; }

        [JsonIgnore]
        public bool ToolUsed { get; set; } = false;

        [JsonIgnore]
        public string? RequestID { get; set; }
    }



}
