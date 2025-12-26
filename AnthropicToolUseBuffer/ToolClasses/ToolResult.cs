using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer.ToolClasses
{
    public class ToolResult
    {
 
        [JsonProperty("success", NullValueHandling = NullValueHandling.Ignore)]
        public bool success { get; set; }

        [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)]
        public bool is_error { get; set; }
 
        [JsonProperty("output_content", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> output_content { get; set; } = new();
 
    }

}
