using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public class ThinkingDelta
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "thinking_delta";

        [JsonProperty("thinking")]
        public string? Thinking { get; set; }
    }
}
