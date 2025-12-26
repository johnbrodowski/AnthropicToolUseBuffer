using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Text;

namespace AnthropicToolUseBuffer
{
    public class ToolBufferDemo
    {
        [JsonProperty("sample_data", NullValueHandling = NullValueHandling.Ignore)]
        public string? sample_data { get; set; } = null;

 
    }
}
