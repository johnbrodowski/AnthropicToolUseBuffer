using System;
using System.Collections.Generic;

namespace AnthropicToolUseBuffer
{
    /// <summary>
    /// Provider-agnostic tool definition that can be converted to any API provider format.
    /// </summary>
    public class UniversalToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Keywords { get; set; } = new();
        public List<string> Constraints { get; set; } = new();
        public string InstructionHeader { get; set; }
        public List<string> Instructions { get; set; } = new();
        public Dictionary<string, UniversalProperty> Properties { get; set; } = new();
        public List<string> RequiredFields { get; set; } = new();
        public bool Strict { get; set; } = false;
        public bool AdditionalProperties { get; set; } = false;

        public UniversalToolDefinition(string name)
        {
            Name = name ?? throw new ArgumentException("Tool name cannot be null.");
        }
    }

    /// <summary>
    /// Represents a property in the universal tool definition.
    /// </summary>
    public class UniversalProperty
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Items { get; set; }
        public Dictionary<string, UniversalProperty> NestedProperties { get; set; }
        public List<string> RequiredFields { get; set; }
        public bool IsArray { get; set; }

        public UniversalProperty(string type, string description)
        {
            Type = type;
            Description = description;
        }
    }
}
