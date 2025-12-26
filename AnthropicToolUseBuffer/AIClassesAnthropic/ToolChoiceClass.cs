using System;

namespace AnthropicToolUseBuffer
{
    public class ToolChoice
    {
        private ToolChoice(string value, string name = null)
        {
            Value = value;
            Name = name;
        }

        public string Value { get; }
        public string Name { get; }

        public static ToolChoice Auto => new ToolChoice("auto");
        public static ToolChoice Any => new ToolChoice("any");
        public static ToolChoice None => null;
        public static ToolChoice Tool(string name) => new ToolChoice("tool", name);

        public static implicit operator string(ToolChoice toolChoice) => toolChoice.Value;

        public override string ToString() => Value;
    }
}
