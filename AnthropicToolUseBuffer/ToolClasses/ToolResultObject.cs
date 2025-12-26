using Newtonsoft.Json;

namespace AnthropicToolUseBuffer.ToolClasses
{

    public class ToolResultObject
    {
        [JsonIgnore]
        public ToolResult tool_result { get; set; } = new();
    }
}