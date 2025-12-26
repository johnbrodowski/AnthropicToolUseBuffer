using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public class ClaudeFile
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;


        [JsonProperty("mime_type")]
        public string MimeType { get; set; } = string.Empty;

        [JsonProperty("size")]
        public int Size { get; set; } = 0;

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
