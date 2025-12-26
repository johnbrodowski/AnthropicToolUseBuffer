using Newtonsoft.Json;

namespace AnthropicToolUseBuffer
{
    public class Content
    {

        [JsonProperty("type")]
        public string type { get; set; } // e.g., "text", "server_tool_use"

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? id { get; set; } // For server_tool_use, this is the tool_use_id

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; } // For server_tool_use, this is "web_search"

        [JsonProperty("input", NullValueHandling = NullValueHandling.Ignore)]
        public ToolInput? input { get; set; } // For server_tool_use, this will contain the 'query'

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string? text { get; set; }    // For text blocks


        // Existing properties from your implied structure
        [JsonProperty("signature", NullValueHandling = NullValueHandling.Ignore)]
        public string? signature { get; set; }

        [JsonProperty("Index", NullValueHandling = NullValueHandling.Ignore)]
        public int Index { get; set; }

        // Property for redacted_thinking data
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public string? Data { get; set; }

    }

}
