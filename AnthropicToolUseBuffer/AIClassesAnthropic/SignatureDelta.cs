using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public class SignatureDelta
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "signature_delta";

        [JsonProperty("signature")]
        public string? Signature { get; set; }
    }
}
