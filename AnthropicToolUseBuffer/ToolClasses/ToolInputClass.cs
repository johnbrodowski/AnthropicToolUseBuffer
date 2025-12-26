using Newtonsoft.Json;



namespace AnthropicToolUseBuffer
{
    public class ToolInput
    {
        [JsonProperty("tool_buffer_demo_params", NullValueHandling = NullValueHandling.Ignore)]
        public ToolBufferDemo? tool_buffer_demo_params { get; set; } = null;

    }
}