using AnthropicToolUseBuffer.AIClassesAnthropic;

using Newtonsoft.Json;

using System.Collections.Generic;

namespace AnthropicToolUseBuffer.ToolClasses
{


    public class Tool
    {
        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string? description { get; set; }

        [JsonProperty("input_schema")]
        public InputSchema input_schema { get; set; }

        // This 'type' is for the tool definition, e.g., "custom" or "web_search_20250305"
        // It should always be serialized for Anthropic tools.
        [JsonProperty("type")]
        public string ToolDefinitionType { get; set; }
         

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? cache_control { get; set; }
 


        // Constructor for standard/custom client-side tools
        public Tool(string name, string description, InputSchema inputSchema, CacheControl? cacheControl = null)
        {
            this.name = name;
            this.description = description;
            this.input_schema = inputSchema;
            this.cache_control = cacheControl;
            this.ToolDefinitionType = "custom"; // *** FIXED: Set to "custom" for client-side tools ***
        }

 
    }
}