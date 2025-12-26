using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public class InputSchema
    {
        public string type { get; set; }  // Use lowercase
        public Dictionary<string, object> properties { get; set; }  // Use lowercase
        public List<string>? required { get; set; }  // Use lowercase

        public InputSchema(string type, Dictionary<string, object> properties, List<string>? required)
        {
            this.type = type;  // Use lowercase
            this.properties = properties;
            this.required = required;
        }
    }
}
