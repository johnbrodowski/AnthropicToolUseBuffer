# AnthropicToolUseBuffer

A C# implementation of asynchronous tool execution buffering for Anthropic's Claude API. This library enables **true parallel conversation and tool execution** - users can continue chatting while long-running tools execute in the background.
 
## 🚀 What Makes This Special

**Traditional AI tool implementations** block the conversation while tools execute:
```
User: "Run this analysis"
AI: [calls analysis tool]
[User must wait 2 minutes...]
AI: "Here are the results"
```

**AnthropicToolUseBuffer** enables parallel execution:
```
User: "Run this analysis"
AI: [calls analysis tool - buffers it]
User: "What's the weather like?"
AI: "Sunny and 72°F" [continues conversation while tool runs]
[Tool completes in background]
AI: "The analysis is complete. Here are the results..."
```

## ✨ Key Features

### 🎯 Core Innovation: Tool Use Buffering
- **Asynchronous tool execution** - Conversation continues while tools run
- **Queue-based message pairing** - Multiple concurrent tool calls supported
- **ID-based matching** - Each `tool_use` automatically paired with its `tool_result` by ID
- **Thread-safe buffering** - Concurrent tool execution without race conditions
- **Timeout handling** - Configurable timeout prevents stale buffers (default: 5 minutes)
- **Smart ping exclusion** - Cache-alive pings don't pollute message history

### 🛠️ Universal Tool Builder
- **Write once, use everywhere** - Define tools once, convert to any provider format
- **Type-safe tool definitions** - Strongly-typed parameter definitions
- **Nested object support** - Complex parameter structures
- **Provider agnostic** - Same interface works across all AI providers

### 💾 Advanced Features
- **Streaming responses** - Real-time SSE parsing with delta handling
- **Prompt caching** - Automatic keep-alive timer before 5-minute expiry
- **Message persistence** - SQLite database with conversation history
- **Permission system** - Control which tools can call other tools (tool chaining)
- **Message validation** - Automatic alternation validation and placeholder injection

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    FormAnthropicDemo                        │
│                     (WinForms UI)                           │
└────────────────────────────┬────────────────────────────────┘
                             │
                ┌────────────┴────────────┐
                │                         │
    ┌───────────▼──────────┐   ┌─────────▼──────────┐
    │  AnthropicApiClass   │   │  Tool Buffer       │
    │  (API Client)        │   │  (Message Pairing) │
    └───────────┬──────────┘   └─────────┬──────────┘
                │                         │
    ┌───────────▼──────────┐   ┌─────────▼──────────┐
    │  Streaming Parser    │   │  MessageDatabase   │
    │  (SSE Handling)      │   │  (SQLite)          │
    └──────────────────────┘   └────────────────────┘

    ┌──────────────────────────────────────────────┐
    │       Universal Tool Builder System          │
    │  (Provider-agnostic tool definitions)        │
    └──────────────────────────────────────────────┘
```

## 📦 Project Structure

```
AnthropicToolUseBuffer/
├── AIClassesAnthropic/          # Message and response classes
│   ├── MessageClass.cs          # Core message structures
│   ├── ContentClass.cs          # Message content types
│   ├── DeltaClass.cs            # Streaming delta handling
│   └── StreamBufferParser.cs    # SSE stream parser
├── ToolBuilder/                 # Universal tool definition system
│   ├── UniversalToolBuilder.cs  # Provider-agnostic builder
│   ├── ToolTransformerBuilderAnthropic.cs
│   ├── LoadTools.cs             # Tool registration
│   └── USAGE_EXAMPLE.cs         # Usage examples
├── ToolClasses/                 # Tool implementations
│   ├── Tool.cs                  # Base tool class
│   ├── ToolClass.cs             # Tool metadata
│   └── ToolBufferDemo.cs        # Demo tool
├── ApiAnthropic.cs              # Main API client
├── FormAnthropicDemo.cs         # WinForms UI implementation
├── MessageDatabase.cs           # SQLite persistence
├── AppSettings.cs               # Configuration management
└── NonBlockingTimerClass.cs     # Cache-alive timer

```

## 🚦 Getting Started

### Prerequisites
- .NET 10.0 or higher
- Anthropic API key ([Get one here](https://console.anthropic.com/))

### Installation

1. Clone the repository:
```bash
git clone https://github.com/johnbrodowski/AnthropicToolUseBuffer.git
```

2. Create `appsettings.json` in the project root:
```json
{
  "anthropic": {
    "apiKey": "YOUR_API_KEY_HERE",
    "defaultModel": "claude-sonnet-4-5",
    "cacheAliveIntervalMinutes": 4.75
  },
  "general": {
    "useTools": true,
    "toolPairTimeoutMinutes": 5
  },
  "database": {
    "defaultDatabaseName": "ToolBufferDemoMessageDatabase.db"
  }
}
```

3. Build and run:
```bash
dotnet build
dotnet run
```

## 💡 How Tool Buffering Works

### The Problem
Traditional implementations send messages immediately:
```csharp
// Traditional approach - blocks conversation
User message → API → Assistant with tool_use → Wait for tool → Send tool_result → API → Continue
```

### The Solution
AnthropicToolUseBuffer uses a queue-based buffer that supports **multiple concurrent tool calls**:
```csharp
// Queue-based buffering - supports concurrent tools
User: "Run analysis A"     → API → Assistant text saved
                                 → tool_use A buffered by ID
                                 → Tool A starts (30 sec)
User: "Run analysis B"     → API → Assistant text saved
                                 → tool_use B buffered by ID
                                 → Tool B starts (20 sec)
User: "What's the status?" → API continues conversation
                                 ↓
Tool B completes (20 sec)  → tool_result B buffered
                           → Match found! Flush pair B
                           → API receives results for B
                                 ↓
Tool A completes (30 sec)  → tool_result A buffered
                           → Match found! Flush pair A
                           → API receives results for A
```

### Implementation
```csharp
// Queue-based buffering (thread-safe)
private readonly object _toolBufferLock = new object();

// Dictionaries indexed by tool_use ID for concurrent support
private readonly Dictionary<string, (MessageAnthropic message, DateTime timestamp)> _pendingToolUseMessages = new();
private readonly Dictionary<string, MessageAnthropic> _pendingToolResults = new();

// When tool_use received: buffer by ID
_pendingToolUseMessages[toolUseId] = (message, DateTime.Now);

// When tool_result received: find matching tool_use by ID
if (_pendingToolUseMessages.ContainsKey(toolUseId))
{
    // Match found - flush this pair only
    FlushPair(toolUseId);
}
```

## 🔧 Tool Definition Example

Define tools once using the Universal Tool Builder:

```csharp
var weatherTool = new UniversalToolBuilder()
    .AddToolName("get_weather")
    .AddDescription("Retrieves current weather information.")
    .AddNestedObject("weather_params", "Weather query parameters", isRequired: true)
        .AddProperty("location", "string", "City name or coordinates", isRequired: true)
        .AddProperty("units", "string", "Temperature units (celsius/fahrenheit)", isRequired: false)
    .EndNestedObject()
    .EndObject()
    .Build();

// Convert to Anthropic format
var anthropicTool = weatherTool.ToAnthropic();
```

See `ToolBuilder/USAGE_EXAMPLE.cs` for more comprehensive examples.

## 🎯 Use Cases

- **Long-running analysis tools** - Run data analysis while user continues conversation
- **Concurrent API calls** - Multiple API requests running simultaneously without blocking
- **Parallel data processing** - Process multiple datasets concurrently
- **Multi-step workflows** - Execute complex tool chains asynchronously
- **Enterprise chatbots** - Production-grade Claude integrations with concurrent tool support
- **AI agent frameworks** - Building blocks for autonomous agents with parallel task execution

## ⚙️ Configuration

### App Settings
All configuration is in `appsettings.json`:

| Setting | Description | Default |
|---------|-------------|---------|
| `anthropic.apiKey` | Your Anthropic API key | Required |
| `anthropic.defaultModel` | Claude model to use | `claude-sonnet-4-5` |
| `anthropic.cacheAliveIntervalMinutes` | Keep-alive ping interval | `4.75` |
| `general.useTools` | Enable tool support | `true` |
| `general.toolPairTimeoutMinutes` | Tool buffer timeout | `5` |
| `database.defaultDatabaseName` | SQLite database name | `ToolBufferDemoMessageDatabase.db` |

### Tool Permissions
Control which tools can call other tools:

```csharp
_toolPermissions.RegisterTool(
    toolName: "tool_buffer_demo",
    canInitiateToolChain: true,
    allowedTools: new[] { "tool_buffer_demo" }
);
```

## 🧪 Demo Tool

The included `tool_buffer_demo` demonstrates async tool execution:

```csharp
User: "Try the tool_buffer_demo"
AI: "I'll call the tool_buffer_demo function for you."
[Tool starts executing - 10 second delay]

User: "Is it working?"
AI: "Yes! The tool is still running in the background..."

[Tool completes]
AI: "The tool has completed! The test was successful."
```

## 📊 Message Flow

```
┌─────────────┐
│ User Input  │
└──────┬──────┘
       │
       ▼
┌─────────────────────┐
│ FlushMatchedToolPair│ ◄──── Check for pending pairs (all IDs)
└──────┬──────────────┘
       │
       ▼
┌──────────────────┐
│ Send to API      │
└──────┬───────────┘
       │
       ▼
┌────────────────────┐
│ Stream Response    │
│ (SSE Parsing)      │
└──────┬─────────────┘
       │
       ├─► text delta        → Display immediately
       ├─► thinking delta    → Show thinking process
       ├─► tool_use          → Buffer in _pendingToolUseMessages[toolUseId]
       └─► tool_result       → Buffer in _pendingToolResults[toolUseId]

       When IDs match:
       └─► FlushMatchedToolPair → Find matching pairs by ID
                                 → Flush matched pairs only
                                 → Keep unmatched pairs in queue
```

## 🔐 Security Notes

- **Never commit API keys** - Use environment variables or secure configuration
- **Validate tool inputs** - Always sanitize user-provided tool parameters
- **Permission system** - Use tool permissions to control tool chaining
- **Database security** - Encrypt sensitive data in message database

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the Apache License 2.0 - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🙏 Acknowledgments

- Built for [Anthropic's Claude](https://www.anthropic.com/claude) API
- Inspired by the need for better async tool execution in AI applications

## 📧 Contact & Author

**Author:** John Brodowski
**Project Link:** [https://github.com/johnbrodowski/AnthropicToolUseBuffer](https://github.com/johnbrodowski/AnthropicToolUseBuffer)
**Release Date:** December 25, 2025

---

**Note:** This is a demonstration implementation extracted from a larger project. The tool buffering mechanism represents a novel approach to handling Claude's tool use capabilities and is being open-sourced to benefit the AI development community.