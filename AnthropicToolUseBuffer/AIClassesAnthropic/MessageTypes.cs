using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public static class MessageType
    {
        public const string Text = "text";
        public const string Image = "image";
        public const string ToolUse = "tool_use";
        public const string ToolResult = "tool_result";
        public const string WebSearch = "web_search_20250305";
        public const string Thinking = "thinking";
        public const string RedactedThinking = "redacted_thinking";
    }
}
