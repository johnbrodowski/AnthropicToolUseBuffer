/*
 * FormAnthropicDemo.cs
 *
 * Main form for the Anthropic Tool Use Buffer demonstration application.
 *
 * This application demonstrates a novel tool buffering pattern for Anthropic's Claude API that enables
 * non-blocking tool execution. Traditional implementations block the conversation while tools execute,
 * but this pattern buffers tool_use/tool_result message pairs, allowing the conversation to continue
 * while tools run asynchronously in the background.
 *
 * Key Features:
 * - Streaming API integration with real-time response display
 * - Non-blocking tool execution with intelligent message buffering
 * - Prompt caching support with automatic keepalive mechanism
 * - Conversation history persistence with database integration
 * - Thread-safe UI updates and error handling
 * - Extended thinking mode support
 *
 * Architecture:
 * - Message buffering uses two dictionaries (tool_use and tool_result) with thread-safe locks
 * - When tool_use arrives, it's buffered until the matching tool_result is ready
 * - Once paired, both messages are sent together to maintain conversation flow
 * - Timeout mechanism prevents indefinite buffering of orphaned messages
 *
 * Author: John Brodowski
 * License: Apache 2.0
 */

using AnthropicToolUseBuffer.AIClassesAnthropic;
using AnthropicToolUseBuffer.Helpers;
using AnthropicToolUseBuffer.ToolClasses;

using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AnthropicToolUseBuffer
{
    /// <summary>
    /// Main form for the Anthropic API demonstration application.
    /// Handles AI conversation, tool execution, and message buffering.
    /// </summary>
    public partial class FormAnthropicDemo : Form
    {
        #region Fields

        // Application configuration loaded from appsettings.json
        private AppSettings _settings;

        // System message sent to Claude with every conversation
        private string systemMessage { get; } = @"You are a helpful AI assistant.

Always inform the user of the tool being used.";

        // Indicates if the AI is in standby mode (ping/pong keepalive state)
        public bool standByMode { get; private set; }

        // Core API client for communicating with Anthropic's Claude API
        private AnthropicApiClass _anthropicApi;

        // System-level messages (like the system prompt) sent with each request
        private List<SystemMessage> _systemMessageList;

        // Conversation history containing all user and assistant messages
        private List<MessageAnthropic> _userMessageList;

        // Manages which tools are allowed to be executed in the current context
        private ToolPermissionManager _anthropicToolPermissions = new ToolPermissionManager();

        // Collection of all available tools that Claude can call
        private List<Tool> tools;

        // Rich text box logger for styled console output
        private LoggerRtb _logRtf = new LoggerRtb();

        // Timer for sending periodic keepalive pings to maintain prompt cache
        private NonBlockingTimer _timerCacheAlive;

        // Database interface for persisting conversation history
        private MessageDatabase _messageDb;

        // Tracks if this is the first API request (for system message initialization)
        private bool _isFirstRequest = true;

        // Streaming state: tracks if we've started receiving content from Claude
        private bool _hasStartedContent = false;

        // Streaming state: tracks if Claude is currently in "thinking" mode
        private bool _hasStartedThinking = false;

        // Global error flag for compilation/runtime error detection
        public bool isError = false;

        // Thread synchronization lock for tool buffering operations
        private readonly object _toolBufferLock = new object();

        // Buffered tool_use messages waiting for their corresponding tool_result
        // Key: tool_use_id, Value: (message containing tool_use, timestamp when buffered)
        private readonly Dictionary<string, (MessageAnthropic message, DateTime timestamp)> _pendingToolUseMessages = new();

        // Buffered tool_result messages waiting for their corresponding tool_use
        // Key: tool_use_id, Value: message containing tool_result
        private readonly Dictionary<string, MessageAnthropic> _pendingToolResults = new();

        // Maximum time to wait for matching tool_use/tool_result pairs before discarding
        private TimeSpan _toolPairTimeout = TimeSpan.FromMinutes(5);

        #endregion Fields

        public FormAnthropicDemo()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes the form when it loads. Sets up API client, tools, database, and loads conversation history.
        /// </summary>
        private async void FormAnthropicDemo_Load(object sender, EventArgs e)
        {
            try
            {
                // Load application settings from appsettings.json
                _settings = SettingsManager.LoadSettings();

                // Validate API key is configured
                if (_settings.Anthropic.ApiKey == "YOUR_API_KEY_HERE")
                {
                    await ChatMessage(ChatUser.Error, "⚠️ API Key not configured! Please edit appsettings.json and add your Anthropic API key.");
                    await ChatMessage(ChatUser.System, $"Settings file location: {SettingsManager.GetSettingsPath()}");
                }

                // Initialize available tools for Claude to use
                await InitializeTools();

                // Initialize the Anthropic API client and wire up event handlers
                await InitializeAnthropic();

                // Initialize empty system and user message lists
                _systemMessageList = new List<SystemMessage>();
                _userMessageList = new List<MessageAnthropic>();

                // Set up the cache keepalive timer
                await InitializeCacheAliveTimer();

                // Initialize database connection for message persistence
                _messageDb = new MessageDatabase(_settings.Database.DefaultDatabaseName);

                // Load previous conversation history from database (truncated to prevent context overflow)
                await LoadContextHistory(
                  truncateAfter: 3000,        // Maximum character length for individual messages
                  msgCount: 100,              // Maximum number of messages to load
                  includeToolMessages: true,  // Include tool_use and tool_result messages
                  databaseName: _settings.Database.DefaultDatabaseName
                  );
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "FormAnthropicDemo_Load", this);
            }
        }

        /// <summary>
        /// Sends a user message to the Anthropic API. Handles pending tool messages and initiates the request.
        /// </summary>
        /// <param name="_userMessage">The user's message text</param>
        /// <param name="displayInChat">Whether to display the message in the chat UI</param>
        /// <param name="saveToDb">Whether to save the message to the database</param>
        private async void SendAnthropic(string _userMessage, bool displayInChat = true, bool saveToDb = true)
        {
            try
            {
                // Flush any timed-out tool pairs before processing new message
                await FlushMatchedToolPair();

                string messageText = _userMessage;

                // Check if there are pending tool_use messages and add informational header (thread-safe)
                lock (_toolBufferLock)
                {
                    if (_pendingToolUseMessages.Any())
                    {
                        // Extract tool names from all pending tool_use messages
                        var toolNames = _pendingToolUseMessages.Values
                            .SelectMany(pair => pair.message.content
                                .Where(c => c is ToolUseContent)
                                .Cast<ToolUseContent>()
                                .Select(t => t.name))
                            .Distinct()
                            .ToList();

                        if (toolNames.Any())
                        {
                            // Prepend a note to inform Claude that certain tools are still processing
                            string toolList = string.Join(", ", toolNames);
                            messageText = $"[NOTE: Tool(s) '{toolList}' are still processing.]\n\n{_userMessage}";
                            Task.Run(async () => await ChatMessage(ChatUser.Debug, $"Added pending tool header to user message."));
                        }
                    }
                }

                // Create the user message object
                var msg = MessageAnthropic.CreateUserMessage(messageText);

                // Display in chat UI if requested
                if (displayInChat) await ChatMessage(ChatUser.User, $"{messageText}");

                // Reset tool permissions to treat this as a fresh user request
                _anthropicToolPermissions.StartToolChain(null);

                // Send the request to the Anthropic API
                await AnthropicSendRequestAsync(new AiRequestParametersAnthropic { UserMessage = msg }, saveToDb);
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "SendAnthropic", this);
            }
        }

        /// <summary>
        /// Sends a keepalive ping to maintain the prompt cache. Does not display in chat or save to database.
        /// </summary>
        private async Task SendKeepAlive()
        {
            try
            {
                // Send a ping message to reset the cache TTL (Time To Live)
                // This is excluded from chat display and database to avoid cluttering history
                SendAnthropic("This is a 'ping' to reset cache ttl, respond with 'ping ack'", displayInChat: false, saveToDb: false);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Medium, "SendKeepAlive", this);
            }
        }

        #region Initializers

        /// <summary>
        /// Initializes the Anthropic API client and wires up event handlers for streaming and response completion.
        /// </summary>
        private async Task InitializeAnthropic()
        {
            try
            {
                // Create API client with configured API key
                _anthropicApi = new AnthropicApiClass(_settings.Anthropic.ApiKey);

                // Wire up event handlers for streaming responses and completion
                _anthropicApi.StreamingEvent += AnthropicOnStreamingEvent;
                _anthropicApi.ResponseCompleted += AnthropicResponseCompletedAsync;

                await ChatMessage(ChatUser.Debug, "✅ Anthropic Client initialized.");
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "InitializeAnthropic", this);
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Initializes the tool system and loads all available tools for Claude to use.
        /// </summary>
        private async Task InitializeTools()
        {
            try
            {
                // Load tool pair timeout from settings
                _toolPairTimeout = TimeSpan.FromMinutes(_settings.General.ToolPairTimeoutMinutes);

                // Load all available tools with full access permissions
                tools = await LoadTools.AnthropicUITools(
                    _toolPermissions: _anthropicToolPermissions,
                    outputPreview: false,
                    FullAccess: true);

                await ChatMessage(ChatUser.Debug, $"✅ Tools initialized.");
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "InitializeTools", this);
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Initializes the cache keepalive timer that sends periodic pings to maintain the prompt cache.
        /// </summary>
        private async Task InitializeCacheAliveTimer()
        {
            try
            {
                _timerCacheAlive = new NonBlockingTimer();

                await ChatMessage(ChatUser.Debug, "✅ CacheAliveTimer initialized.");

                // Wire up timer event handlers
                _timerCacheAlive.TimerCompleted += Timer_TimerCompleted;
                _timerCacheAlive.TimerTick += Timer_TimerTick;
                _timerCacheAlive.ErrorOccurred += Timer_TimerError;

                // Set up timer interval on a background thread to avoid blocking UI
                await ThreadHelper.RunOnThreadAsync(async () =>
                    await _timerCacheAlive.SetIntervalAsync(_settings.Anthropic.CacheAliveIntervalMinutes, NonBlockingTimer.IntervalUnit.Minutes, true),
                    ex => ThreadHelper.InvokeOnUIThread(this, () => MessageBox.Show($"Timer setup failed: {ex.Message}")));

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "InitializeCacheAliveTimer", this);
            }
        }

        #endregion Initializers

        #region Anthropic Send Request

        /// <summary>
        /// Sends a request to the Anthropic API with the specified parameters.
        /// Handles system message initialization, message history, and streaming.
        /// </summary>
        /// <param name="requestParameters">The request parameters including user message, model, and tools</param>
        /// <param name="saveToDb">Whether to save the message to the database</param>
        private async Task AnthropicSendRequestAsync(AiRequestParametersAnthropic requestParameters, bool saveToDb = true)
        {
            // Reset cache keepalive timer when sending a request
            if (_timerCacheAlive.IsRunning)
            {
                _ = ResetTimerAsync();
            }

            try
            {
                // On first request, initialize system message list
                if (_isFirstRequest)
                {
                    _isFirstRequest = false;
                    _systemMessageList.Clear();
                    _systemMessageList.Add(new SystemMessage(systemMessage));
                }

                // Save the user message to history and optionally to database
                SaveMessage(requestParameters.UserMessage, saveToDb);

                // Configure request parameters
                requestParameters.Model = ModelOption.Claude45Haiku;
                requestParameters.SystemMessageList = _systemMessageList.Any() ? _systemMessageList : null;
                requestParameters.UserMessageList = _userMessageList;
                requestParameters.Tools = tools;

                // Send the streaming request to Anthropic
                await _anthropicApi.SendAnthropicApiRequestStreamAsync(requestParameters);
            }
            catch (Exception ex)
            {
                // Ensure UI button is reset on error
                ThreadHelper.InvokeOnUIThread(this, () => btnSend.Text = "Send");
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "AnthropicSendRequestAsync", this);
            }
            finally
            {
                // Always reset the send button state
                ThreadHelper.InvokeOnUIThread(this, () => btnSend.Text = "Send");
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Serializes a list of messages to formatted JSON for debugging purposes.
        /// </summary>
        /// <param name="messages">The messages to serialize</param>
        /// <returns>Formatted JSON string</returns>
        public async Task<string> GetMessagesAsJson(List<MessageAnthropic> messages)
        {
            try
            {
                var settings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                    DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore
                };

                return await Task.FromResult(Newtonsoft.Json.JsonConvert.SerializeObject(messages, settings));
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Low, "GetMessagesAsJson", this);
                return "{}";
            }
        }

        /// <summary>
        /// Serializes a single message to formatted JSON for debugging purposes.
        /// </summary>
        /// <param name="messages">The message to serialize</param>
        /// <returns>Formatted JSON string</returns>
        public async Task<string> GetMessageAsJson(MessageAnthropic messages)
        {
            try
            {
                var settings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                    DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore
                };

                return await Task.FromResult(Newtonsoft.Json.JsonConvert.SerializeObject(messages, settings));
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Low, "GetMessageAsJson", this);
                return "{}";
            }
        }

        /// <summary>
        /// Buffers all tool results and attempts to match them with their corresponding tool_use messages.
        /// This enables non-blocking tool execution where tools can complete in any order.
        /// </summary>
        /// <param name="allToolResults">List of tool results with their IDs, outputs, and error status</param>
        private async Task SendAllToolResults(List<(string toolUseId, List<string> output, bool isError)> allToolResults)
        {
            try
            {
                // Buffer each tool_result individually in the queue
                foreach (var (toolUseId, output, isError) in allToolResults)
                {
                    await ChatMessage(ChatUser.Debug, $"Tool result Log (ID: {toolUseId}):");

                    // Convert output strings to MessageContent objects
                    var contentList = output
                        .Select(msg => new MessageContent { text = msg })
                        .Cast<IMessageContentAnthropic>()
                        .ToList();

                    // Create a user message containing this tool_result
                    var userMessage = new MessageAnthropic("user");
                    userMessage.content.Add(new ToolResultContentList
                    {
                        tool_use_id = toolUseId,
                        content = contentList,
                        is_error = isError
                    });

                    // Buffer this tool_result in the pending dictionary (thread-safe)
                    lock (_toolBufferLock)
                    {
                        _pendingToolResults[toolUseId] = userMessage;
                    }
                    await ChatMessage(ChatUser.Debug, $"Buffered tool_result (ID: {toolUseId}). Checking for matching tool_use...");
                }

                // Attempt to flush any matched tool_use/tool_result pairs
                await FlushMatchedToolPair();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "SendAllToolResults", this);
            }
        }

        /// <summary>
        /// Matches buffered tool_use messages with their corresponding tool_result messages and sends them to the API.
        /// Also handles timeout cleanup for orphaned tool messages.
        /// </summary>
        private async Task FlushMatchedToolPair()
        {
            try
            {
                List<(string toolUseId, MessageAnthropic toolUseMsg, MessageAnthropic toolResultMsg)> matchedPairs = new();
                List<string> timedOutIds = new();

                // Find matched pairs and timeouts (thread-safe)
                lock (_toolBufferLock)
                {
                    // Iterate through all pending tool_use messages
                    foreach (var toolUseId in _pendingToolUseMessages.Keys.ToList())
                    {
                        if (_pendingToolResults.ContainsKey(toolUseId))
                        {
                            // Found a matching tool_result for this tool_use
                            matchedPairs.Add((
                                toolUseId,
                                _pendingToolUseMessages[toolUseId].message,
                                _pendingToolResults[toolUseId]
                            ));

                            // Remove both from their respective dictionaries
                            _pendingToolUseMessages.Remove(toolUseId);
                            _pendingToolResults.Remove(toolUseId);
                        }
                        else
                        {
                            // Check if this tool_use has timed out waiting for its result
                            if (DateTime.Now - _pendingToolUseMessages[toolUseId].timestamp > _toolPairTimeout)
                            {
                                timedOutIds.Add(toolUseId);
                                _pendingToolUseMessages.Remove(toolUseId);
                            }
                        }
                    }
                }

                // Process matched pairs outside the lock to avoid blocking
                foreach (var (toolUseId, toolUseMsg, toolResultMsg) in matchedPairs)
                {
                    await ChatMessage(ChatUser.Debug, $"Flushing matched pair (ID: {toolUseId}).");

                    // Determine if this is a ping response (to exclude from database)
                    bool isPingResponse = false;
                    if (_userMessageList.Count > 0)
                    {
                        var lastUserMessage = _userMessageList.LastOrDefault(m => m.role == "user");
                        if (lastUserMessage != null)
                        {
                            var textContent = lastUserMessage.content
                                .OfType<MessageContent>()
                                .FirstOrDefault()?.text ?? "";
                            isPingResponse = textContent.Contains("This is a 'ping'");
                        }
                    }

                    // Save the buffered tool_use message to conversation history
                    SaveMessage(toolUseMsg, saveToDb: !isPingResponse);
                    await ChatMessage(ChatUser.Debug, $"Saved buffered tool_use (ID: {toolUseId}) to history.");

                    // Send the tool_result to Claude to continue the conversation
                    await AnthropicSendRequestAsync(new AiRequestParametersAnthropic { UserMessage = toolResultMsg }, saveToDb: !isPingResponse);
                }

                // Log any timed-out tool pairs that were discarded
                foreach (var toolUseId in timedOutIds)
                {
                    await ChatMessage(ChatUser.Debug, $"Tool pair timeout (ID: {toolUseId}). Discarding.");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "FlushMatchedToolPair", this);
            }
        }

        #endregion Anthropic Send Request

        #region Anthropic Event Handlers

        /// <summary>
        /// Handles streaming events from the Anthropic API including content deltas, thinking, tool use, and status updates.
        /// All UI updates are marshalled to the UI thread for thread safety.
        /// </summary>
        private async void AnthropicOnStreamingEvent(object? sender, (StreamingEventType EventType, string? Content, string? Message, string? Json) args)
        {
            // Run on background thread to prevent blocking the streaming connection
            await Task.Run(async () =>
            {
                // Marshal all operations to UI thread since we update UI controls and call async chat methods
                await ThreadHelper.InvokeOnUIThreadAsync(this, async () =>
                {
                    switch (args.EventType)
                    {
                        // Raw SSE (Server-Sent Events) data for debugging
                        case StreamingEventType.RawData:
                            if (args.Content != null) await ChatMessage(ChatUser.RawData, args.Content);
                            break;

                        // Debug messages from the API client
                        case StreamingEventType.Debug:
                            await ChatMessage(ChatUser.Debug, $"{args.Content}");
                            break;

                        // Warning messages from the API client
                        case StreamingEventType.Warning:
                            await ChatMessage(ChatUser.Warning, $"{args.Content}");
                            break;

                        // Start of a new message from Claude
                        case StreamingEventType.MessageStart:
                            // Message metadata received, content will follow
                            break;

                        // Start of a new content block (text, thinking, or tool_use)
                        case StreamingEventType.ContentBlockStart:
                            // Content block metadata received, deltas will follow
                            break;

                        // Incremental content deltas (thinking or text)
                        case StreamingEventType.ContentBlockDelta:
                            if (args.Message == "thinking")
                            {
                                // Handle extended thinking mode - Claude is reasoning before responding
                                if (!_hasStartedThinking)
                                {
                                    await ChatMessage(ChatUser.Assistant, $"Thinking.");
                                    _hasStartedThinking = true;
                                }
                                else
                                {
                                    // Append dots to show thinking progress
                                    await ChatMessage(ChatUser.AssistantStream, " . ");
                                }
                            }
                            else if (args.Message == "text")
                            {
                                // Handle text content streaming
                                if (_hasStartedThinking)
                                {
                                    _hasStartedThinking = false;
                                }

                                if (!_hasStartedContent)
                                {
                                    // First chunk of text - start new message
                                    await ChatMessage(ChatUser.Assistant, $"{args.Content}");
                                    _hasStartedContent = true;
                                }
                                else
                                {
                                    // Subsequent chunks - append to existing message
                                    await ChatMessage(ChatUser.AssistantStream, $"{args.Content}");
                                }
                            }
                            break;

                        // Thinking block completed
                        case StreamingEventType.ContentBlockStop when args.Message == "thinking":
                            await ChatMessage(ChatUser.Debug, "[Thinking Complete]");
                            ResetMessageStates();
                            break;

                        // Text content block completed
                        case StreamingEventType.ContentBlockStop when (args.Message == "text" && !string.IsNullOrEmpty(args.Content) && !args.Content.Contains("DONE")):
                            ResetMessageStates();
                            break;

                        // Tool input JSON block completed
                        case StreamingEventType.ContentBlockStop when (args.Message == "json" && !string.IsNullOrEmpty(args.Content) && !args.Content.Contains("DONE")):
                            // Tool input parameters received and complete
                            Debug.WriteLine($"Tool Input JSON:\n{args.Content}\n");
                            break;

                        // Message delta containing metadata like stop_reason or usage updates
                        case StreamingEventType.MessageDelta:
                            // Additional message metadata received during streaming
                            break;

                        // End of the entire message stream
                        case StreamingEventType.MessageStop:
                            // Stream completed, ResponseCompleted event will fire next
                            break;

                        // Keepalive ping from the server
                        case StreamingEventType.Ping:
                            Debug.WriteLine($"[Ping Received]");
                            await ChatMessage(ChatUser.Debug, "Ping Received");
                            break;

                        // API status information
                        case StreamingEventType.Status:
                            await ChatMessage(ChatUser.System, "Status:", args.Content);
                            break;

                        // Token usage information
                        case StreamingEventType.Usage:
                            await ChatMessage(ChatUser.System, "Usage:", args.Content);
                            break;

                        // Streaming interaction complete
                        case StreamingEventType.InteractionComplete:
                            // Reset cache keepalive timer since we just interacted
                            if (_timerCacheAlive.IsRunning)
                            {
                                _ = ResetTimerAsync();
                            }
                            await ChatMessage(ChatUser.Debug, "Anthropic Interaction Complete");
                            break;

                        // Stream was cancelled or encountered an error
                        case StreamingEventType.Cancelled:
                        case StreamingEventType.Error:
                            // Reset UI state
                            btnSend.Text = "Send";
                            _hasStartedContent = false;
                            _hasStartedThinking = false;

                            // Log the error or cancellation
                            if (args.EventType == StreamingEventType.Error)
                            {
                                await ChatMessage(ChatUser.Error, $"Anthropic Stream Error:\n\n{args.Content}");
                            }
                            else
                            {
                                await ChatMessage(ChatUser.Warning, $"Anthropic Stream Cancelled.");
                            }
                            break;

                        default:
                            // Log any unexpected event types for debugging
                            await ChatMessage(ChatUser.Warning, $"Unhandled Anthropic Streaming Event: {args.EventType}");
                            break;
                    }
                }); // End InvokeOnUIThreadAsync
            }); // End Task.Run
        }

        /// <summary>
        /// Handles the completion of a streaming response from Claude. Processes the full response,
        /// handles tool execution, and manages message buffering for tool use/result pairing.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="aiResponse">The complete AI response with all content blocks</param>
        /// <param name="updatedMessageList">The updated conversation history</param>
        /// <param name="requestId">Unique identifier for this request</param>
        private async Task AnthropicResponseCompletedAsync(object sender, AnthropicResponse aiResponse, List<MessageAnthropic> updatedMessageList, string requestId)
        {
            List<(string toolUseId, List<string> output, bool isError)> allToolResults = new();

            // Run on background thread to prevent blocking
            await Task.Run(async () =>
            {
                await ThreadHelper.InvokeOnUIThreadAsync(this, async () =>
                {
                    // Reset cache keepalive timer since we just received a response
                    if (_timerCacheAlive.IsRunning)
                    {
                        _ = ResetTimerAsync();
                    }

                    ToolResultObject result = new();
                    StringBuilder toolResponseLog = new(); // Log of all tool processing steps

                    try
                    {
                        // Create entry to hold all content from this response
                        var entry = new MessageAnthropic(aiResponse.role)
                        {
                            content = new List<IMessageContentAnthropic>()
                        };

                        // These will hold split messages when tools are used
                        MessageAnthropic? textOnlyMessage = null;
                        MessageAnthropic? toolUseOnlyMessage = null;

                        // Process each content block in the response
                        foreach (var item in aiResponse.content)
                        {
                            if (item.type == "thinking")
                            {
                                // Extended thinking content
                                entry.content.Add(new ThinkingContent
                                {
                                    ThinkingText = item.text,
                                    Signature = item.signature
                                });
                            }
                            else if (item.type == "text")
                            {
                                if (string.IsNullOrEmpty(item.text)) continue;

                                // Check for ping acknowledgment (keepalive response)
                                if (item.text.Contains("ping_ttl_ack")) return;

                                standByMode = false;

                                entry.content.Add(new MessageContent
                                {
                                    text = item.text
                                });
                            }
                            else if (item.type == "tool_use")
                            {
                                // Claude wants to execute a tool
                                aiResponse.ToolUsed = true;

                                entry.content.Add(new ToolUseContent
                                {
                                    id = item.id,
                                    name = item.name,
                                    input = item.input
                                });
                            }
                        }

                        // Update conversation history with the latest message list
                        _userMessageList = updatedMessageList;

                        // CRITICAL: Split tool_use responses to enable buffering
                        // When Claude uses tools, we split the response into two messages:
                        // 1. Text-only message (saved immediately to history)
                        // 2. Tool-use-only message (buffered until tool completes)
                        // This prevents conversation blocking while tools execute
                        if (aiResponse.ToolUsed)
                        {
                            // Extract text/thinking content (excludes tool_use)
                            var textContent = entry.content
                                .Where(c => c is MessageContent || c is ThinkingContent)
                                .ToList();

                            // Extract tool_use content (excludes text/thinking)
                            var toolUseContent = entry.content
                                .Where(c => c is ToolUseContent)
                                .ToList();

                            // Create text message if Claude provided explanatory text
                            if (textContent.Any())
                            {
                                textOnlyMessage = new MessageAnthropic(aiResponse.role)
                                {
                                    content = textContent
                                };
                            }
                            else if (toolUseContent.Any())
                            {
                                // CRITICAL: Claude called a tool without sending text first
                                // Create placeholder text to maintain user/assistant alternation
                                // The API requires alternating roles, so we can't have User -> User
                                textOnlyMessage = new MessageAnthropic(aiResponse.role)
                                {
                                    content = new List<IMessageContentAnthropic>
                                    {
                                        new MessageContent { text = "[Tool called]" }
                                    }
                                };
                                await ChatMessage(ChatUser.Debug, $"AI sent tool_use without text. Created placeholder assistant message to maintain alternation.");
                            }

                            // Create tool_use-only message for buffering
                            if (toolUseContent.Any())
                            {
                                toolUseOnlyMessage = new MessageAnthropic(aiResponse.role)
                                {
                                    content = toolUseContent
                                };
                            }

                            // Detect ping responses to exclude from database
                            bool isPingResponse = false;
                            if (_userMessageList.Count > 0)
                            {
                                var lastUserMessage = _userMessageList.LastOrDefault(m => m.role == "user");
                                if (lastUserMessage != null)
                                {
                                    var userTextContent = lastUserMessage.content
                                        .OfType<MessageContent>()
                                        .FirstOrDefault()?.text ?? "";
                                    isPingResponse = userTextContent.Contains("This is a 'ping'");
                                }
                            }

                            // Save text message immediately to allow conversation to continue
                            if (textOnlyMessage != null)
                            {
                                SaveMessage(textOnlyMessage, saveToDb: !isPingResponse);
                                await ChatMessage(ChatUser.Debug, $"Saved text portion of assistant message to history.");
                            }

                            // Buffer tool_use message until tool execution completes
                            if (toolUseOnlyMessage != null)
                            {
                                // Get the tool_use ID for matching with tool_result later
                                var toolUseId = toolUseOnlyMessage.content
                                    .OfType<ToolUseContent>()
                                    .FirstOrDefault()?.id;

                                if (!string.IsNullOrEmpty(toolUseId))
                                {
                                    lock (_toolBufferLock)
                                    {
                                        _pendingToolUseMessages[toolUseId] = (toolUseOnlyMessage, DateTime.Now);
                                    }
                                    await ChatMessage(ChatUser.Debug, $"Buffered tool_use (ID: {toolUseId}). Waiting for tool_result...");
                                }
                            }
                        }
                        else
                        {
                            // No tools used - save message normally
                            bool isPingResponse = false;
                            if (_userMessageList.Count > 0)
                            {
                                var lastUserMessage = _userMessageList.LastOrDefault(m => m.role == "user");
                                if (lastUserMessage != null)
                                {
                                    var textContent = lastUserMessage.content
                                        .OfType<MessageContent>()
                                        .FirstOrDefault()?.text ?? "";
                                    isPingResponse = textContent.Contains("This is a 'ping'");
                                }
                            }

                            // Save the complete message (skip database for ping responses)
                            SaveMessage(entry, saveToDb: !isPingResponse);
                        }

                        // Execute all requested tools
                        if (aiResponse?.content != null && aiResponse.ToolUsed)
                        {
                            foreach (var item in aiResponse.content)
                            {
                                // Validate tool_use block has required fields
                                if (item != null && item.type == "tool_use" && !string.IsNullOrEmpty(item.id) && !string.IsNullOrEmpty(item.name))
                                {
                                    // Process the tool (checks permissions, executes handler)
                                    var (isAllowed, toolResult) = await ProcessToolUse(item, toolResponseLog);

                                    // Collect result for batched sending
                                    allToolResults.Add((
                                        item.id,
                                        toolResult.tool_result.output_content,
                                        toolResult.tool_result.is_error
                                    ));
                                }
                            }

                            // Send all tool results together for efficient processing
                            if (allToolResults.Any())
                            {
                                await SendAllToolResults(allToolResults);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "AnthropicResponseCompletedAsync", this);
                        toolResponseLog.AppendLine($"EXCEPTION during response processing: {ex.Message}");
                    }
                    finally
                    {
                        // Display token usage information
                        //await ChatMessage(ChatUser.Usage, $"Input Tokens:", $"{aiResponse.usage?.input_tokens ?? 0}");
                        //await ChatMessage(ChatUser.Usage, $"Output Tokens:", $"{aiResponse.usage?.output_tokens ?? 0}");

                        //// Display cache-related token usage (only if non-zero)
                        //if (aiResponse.usage?.cache_creation_input_tokens != null && aiResponse.usage?.cache_creation_input_tokens > 0)
                        //    await ChatMessage(ChatUser.Usage, $"Cache Creation Input Tokens:", $"{aiResponse.usage?.cache_creation_input_tokens ?? 0}");

                        //if (aiResponse.usage?.cache_read_input_tokens != null && aiResponse.usage?.cache_read_input_tokens > 0)
                        //    await ChatMessage(ChatUser.Usage, $"Cache Read Input Tokens:", $"{aiResponse.usage?.cache_read_input_tokens ?? 0}");

                        //if (aiResponse.usage?.cache_creation?.Ephemeral5mInputTokens != null && aiResponse.usage?.cache_creation?.Ephemeral5mInputTokens > 0)
                        //    await ChatMessage(ChatUser.Usage, $"5m InputTokens Tokens:", $"{aiResponse.usage?.cache_creation?.Ephemeral5mInputTokens ?? 0}");

                        //if (aiResponse.usage?.cache_creation?.Ephemeral1hInputTokens != null && aiResponse.usage?.cache_creation?.Ephemeral1hInputTokens > 0)
                        //    await ChatMessage(ChatUser.Usage, $"1h InputTokens Tokens", $"{aiResponse.usage?.cache_creation?.Ephemeral1hInputTokens ?? 0}");

                        // Debug log of all tool processing steps
                        Debug.WriteLine($"--- Tool Processing Log for Request {requestId} ---\n{toolResponseLog}");
                    }
                }); // End of InvokeOnUIThreadAsync
            }); // End Task.Run
        }

        #endregion Anthropic Event Handlers

        /// <summary>
        /// Demo tool handler that simulates a long-running operation.
        /// Used to demonstrate the tool buffering system's ability to handle async tool execution.
        /// </summary>
        /// <param name="item">The tool_use content containing input parameters</param>
        /// <returns>Tool result with success or error status</returns>
        private async Task<ToolResultObject> HandleToolBufferDemo(Content? item)
        {
            var result = new ToolResultObject();
            var toolResponse = new StringBuilder();

            try
            {
                // Validate required input parameter
                if (string.IsNullOrEmpty(item?.input?.tool_buffer_demo_params?.sample_data))
                {
                    result.tool_result.output_content.Add("Error: sample_data is missing.");
                    result.tool_result.is_error = true;
                    result.tool_result.success = false;
                }
                else
                {
                    // Simulate long-running tool work (demonstrates non-blocking execution)
                    await Task.Delay(TimeSpan.FromSeconds(30));

                    result.tool_result.output_content.Add(
                        "Success, test completed and sample_data is present."
                    );
                    result.tool_result.is_error = false;
                    result.tool_result.success = true;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.tool_result.output_content.Add($"Error in tool execution: {ex.Message}");
                result.tool_result.is_error = true;
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "HandleToolBufferDemo", this);
            }

            return result;
        }

        #region Helper Methods

        /// <summary>
        /// Returns the appropriate tool handler function based on tool name.
        /// </summary>
        private Func<Content, Task<ToolResultObject>> GetToolHandler(string toolName)
        {
            switch (toolName)
            {
                case "tool_buffer_demo": return HandleToolBufferDemo;

                default:
                    // Return a handler that generates an error for unknown tools
                    return async (contentItem) =>
                    {
                        var errorResult = new ToolResultObject();
                        errorResult.tool_result = new ToolResult
                        {
                            is_error = true,
                            success = false,
                            output_content = new List<string> { $"Error: Unknown tool '{contentItem.name}' was requested." }
                        };
                        return errorResult;
                    };
            }
        }

        /// <summary>
        /// Creates a standardized permission denied error message in JSON format.
        /// </summary>
        private string CreatePermissionDeniedErrorJson(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) toolName = "UnknownTool";

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                error = $"Tool '{toolName}' is not allowed in the current context. Review the chain of thought, rules, and guidelines.",
                status = "error",
                message = "Stop, inform the user of the error. Do NOT proceed!"
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Processes a single tool use item: checks permissions, selects handler, and executes.
        /// </summary>
        private async Task<(bool isAllowed, ToolResultObject result)> ProcessToolUse(Content item, StringBuilder toolResponseLog)
        {
            toolResponseLog.AppendLine($"Processing tool use: ID={item.id}, Name={item.name}");

            // Check tool permissions
            bool isAllowed = _anthropicToolPermissions.IsToolUseAllowed(item.name);

            if (!isAllowed)
            {
                toolResponseLog.AppendLine($"Tool '{item.name}' not allowed in current context.");

                var errorResult = new ToolResultObject();
                errorResult.tool_result = new ToolResult
                {
                    is_error = true,
                    success = false,
                    output_content = new List<string> { CreatePermissionDeniedErrorJson(item.name) }
                };

                return (false, errorResult);
            }

            _anthropicToolPermissions.StartToolChain(item.name);

            toolResponseLog.AppendLine($"Tool chain started for '{item.name}'.");

            // Get the appropriate handler
            var handlerFunc = GetToolHandler(item.name);

            toolResponseLog.AppendLine($"Executing handler for '{item.name}'...");

            // Execute the handler
            ToolResultObject result;
            try
            {
                result = await handlerFunc(item);

                // Ensure there's at least one output message
                if (result.tool_result.output_content == null || !result.tool_result.output_content.Any())
                {
                    result.tool_result.output_content = new List<string> { $"Operation completed: {item.name}" };
                }
            }
            catch (Exception ex)
            {
                result = new ToolResultObject();
                result.tool_result = new ToolResult
                {
                    is_error = true,
                    success = false,
                    output_content = new List<string> { $"Error executing {item.name}: {ex.Message}" }
                };
                toolResponseLog.AppendLine($"Exception during handler execution: {ex.Message}");
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "ProcessToolUse", this);
            }

            toolResponseLog.AppendLine($"Handler execution completed for '{item.name}'.");
            return (true, result);
        }

        #endregion Helper Methods

        #region Alive Timer

        /// <summary>
        /// Toggles the cache keepalive timer on/off. Used for testing and debugging.
        /// </summary>
        private async Task ToggleTimerStateAsync()
        {
            try
            {
                await ThreadHelper.RunOnThreadAsync(async () =>
                {
                    await _timerCacheAlive.SetIntervalAsync(4, NonBlockingTimer.IntervalUnit.Seconds, true);
                    if (_timerCacheAlive.IsRunning)
                        await _timerCacheAlive.StopAsync();
                    else
                        await _timerCacheAlive.StartAsync();
                    await ChatMessage(ChatUser.System, $"Timer state: {_timerCacheAlive.IsRunning}");
                }, ex => ThreadHelper.InvokeOnUIThread(this, () => MessageBox.Show($"Toggle failed: {ex.Message}")));
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Low, "ToggleTimerStateAsync", this);
            }
        }

        /// <summary>
        /// Starts the cache keepalive timer on a background thread.
        /// </summary>
        private async Task StartTimerAsync()
        {
            try
            {
                await ThreadHelper.RunOnThreadAsync(
                    async () => await _timerCacheAlive.StartAsync(),
                    ex => ThreadHelper.InvokeOnUIThread(this, () =>
                        MessageBox.Show($"Start failed: {ex.Message}")));
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Medium, "StartTimerAsync", this);
            }
        }

        /// <summary>
        /// Resets the cache keepalive timer back to its full interval.
        /// Called after each API interaction to delay the next ping.
        /// </summary>
        private async Task ResetTimerAsync()
        {
            try
            {
                await ThreadHelper.RunOnThreadAsync(
                    async () => await _timerCacheAlive.ResetAsync(),
                    ex => ThreadHelper.InvokeOnUIThread(this, () =>
                    MessageBox.Show($"Reset failed: {ex.Message}")));
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Low, "ResetTimerAsync", this);
            }
        }

        /// <summary>
        /// Timer completion event handler. Sends a keepalive ping to maintain the prompt cache.
        /// </summary>
        private async Task Timer_TimerCompleted(object sender, EventArgs args)
        {
            try
            {
                // Run SendKeepAlive on a background thread as it involves network calls
                await ThreadHelper.RunOnThreadAsync(async () =>
                {
                    await SendKeepAlive();
                }, ex =>
                {
                    // Log error if SendKeepAlive fails
                    Debug.WriteLine($"Error during SendKeepAlive: {ex.Message}");
                    _ = ChatMessage(ChatUser.Error, "Failed to send keep-alive ping.");
                });
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Medium, "Timer_TimerCompleted", this);
            }
        }

        /// <summary>
        /// Timer tick event handler. Updates the form title with remaining cache time.
        /// </summary>
        private async Task Timer_TimerTick(object sender, EventArgs args)
        {
            try
            {
                // Update window title with countdown timer (on UI thread)
                ThreadHelper.InvokeOnUIThread(this, () =>
                {
                    this.Text = _timerCacheAlive.GetTimeString();
                });
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Low, "Timer_TimerTick", this);
            }
        }

        /// <summary>
        /// Timer error event handler. Logs timer-related errors.
        /// </summary>
        private async Task Timer_TimerError(object sender, NonBlockingTimer.ErrorEventArgs args)
        {
            try
            {
                await ThreadHelper.RunOnThreadAsync(async () =>
                    await ChatMessage(ChatUser.System, $"Error: {args.ErrorMessage}\n{(args.Exception?.Message ?? "")}"));
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Low, "Timer_TimerError", this);
            }
        }

        #endregion Alive Timer

        #region Chat Message and Logging

        /// <summary>
        /// Configuration record for chat message styling
        /// </summary>
        private record ChatStyle(
            string Label,
            Color Part1Color,
            Color Part2Color,
            int Part1FontSize = 10,
            int Part2FontSize = 10,
            bool Part1Bold = true,
            bool Part1Italic = false,
            bool Part1Underlined = false,
            bool Part2Bold = false,
            bool Part2Italic = false,
            bool Part2Underlined = false,
            bool ClearPrefix = false);

        /// <summary>
        /// Gets the style configuration for a specific chat user type
        /// </summary>
        private ChatStyle GetChatStyle(ChatUser chatUser) => chatUser switch
        {
            ChatUser.User => new("User: ", Color.OrangeRed, Color.Black, Part2FontSize: 11),
            ChatUser.Assistant => new("Assistant: ", Color.Green, Color.Blue, Part2FontSize: 11),
            ChatUser.AssistantStream => new("", Color.Blue, Color.Blue, Part1Bold: false, ClearPrefix: true, Part2FontSize: 10),
            ChatUser.RawData => new("Raw: ", Color.Black, Color.Purple),
            ChatUser.Warning => new("Warning: ", Color.DarkRed, Color.Purple),
            ChatUser.Error => new("Error: ", Color.Red, Color.Blue),
            ChatUser.System => new("System: ", Color.Black, Color.Brown, Part1FontSize: 9),
            ChatUser.Debug => new("Debug: ", Color.DarkCyan, Color.Blue),
            ChatUser.Usage => new("Usage: ", Color.Black, Color.Blue, Part1FontSize: 9, Part2FontSize: 9,
                                  Part1Italic: true, Part2Bold: true, Part2Underlined: true),
            ChatUser.Info => new("Info: ", Color.Orange, Color.Blue),
            _ => new("", Color.Orange, Color.Purple)
        };

        /// <summary>
        /// Displays a styled message in the chat log. Supports different message types with custom colors and formatting.
        /// Thread-safe - marshals UI updates to the UI thread.
        /// </summary>
        public async Task ChatMessage(ChatUser? chatUser, string? part1 = null, string? part2 = null,
            bool includeChatMessageType = true, bool includeNewLine = true, Color? part1Color = null, Color? part2Color = null)
        {
            await InvokeOnUIAsync(async () =>
            {
                if (chatUser == null) return;

                var style = GetChatStyle(chatUser.Value);
                var prefix = includeNewLine && !string.IsNullOrEmpty(rtbLog.Text) ? "\n" : "";
                if (style.ClearPrefix) prefix = "";

                var label = includeChatMessageType ? style.Label : "";

                var styleA = new TextStyleRtb
                {
                    Color = part1Color ?? style.Part1Color,
                    FontSize = style.Part1FontSize,
                    Bold = style.Part1Bold,
                    Italic = style.Part1Italic,
                    Underlined = style.Part1Underlined
                };

                var styleB = new TextStyleRtb
                {
                    Color = part2Color ?? style.Part2Color,
                    FontSize = style.Part2FontSize,
                    Bold = style.Part2Bold,
                    Italic = style.Part2Italic,
                    Underlined = style.Part2Underlined
                };

                // Special handling for Usage type
                if (chatUser == ChatUser.Usage)
                {
                    await _logRtf.WriteRtb(rtbLog, $"{prefix}{part1}", $" {part2}", styleA, styleB);
                    return;
                }

                // Skip debug messages with no content
                if (chatUser == ChatUser.Debug)
                {
                    if (string.IsNullOrEmpty(part1) && string.IsNullOrEmpty(part2)) return;

                    Debug.WriteLine($"Debug: {part1} {part2}");

                    return;
                }

                // Combine content parts
                var content = string.IsNullOrEmpty(part1) ? part2
                            : string.IsNullOrEmpty(part2) ? part1
                            : $"{part1} {part2}";

                if (!string.IsNullOrEmpty(content))
                    await _logRtf.WriteRtb(rtbLog, $"{prefix}{label}", content, styleA, styleB);
            });
        }

        /// <summary>
        /// Saves a message to the conversation history and optionally to the database.
        /// Thread-safe operation that runs on a background thread.
        /// </summary>
        /// <param name="message">The message to save</param>
        /// <param name="saveToDb">Whether to persist the message to the database</param>
        public async void SaveMessage(MessageAnthropic message, bool saveToDb = true)
        {
            //  await Task.Run(async () =>
            // {
            try
            {
                // Add to in-memory conversation history
                _userMessageList.Add(message);

                // Optionally persist to database for long-term storage
                if (saveToDb)
                {
                    await _messageDb.SaveMessage(message);
                }
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Medium, "SaveMessage", this);
            }
            // });
        }

        /// <summary>
        /// Loads previous conversation history from the database and validates message structure.
        /// Applies processing to ensure proper user/assistant message alternation.
        /// </summary>
        /// <param name="truncateAfter">Maximum character length for truncating individual messages</param>
        /// <param name="msgCount">Maximum number of messages to load</param>
        /// <param name="includeToolMessages">Whether to include tool_use and tool_result messages</param>
        /// <param name="databaseName">Name of the database to load from</param>
        private async Task LoadContextHistory(int truncateAfter, int msgCount, bool includeToolMessages, string databaseName)
        {
            try
            {
                _userMessageList = new List<MessageAnthropic>();

                // Helper function to retrieve messages from database
                async Task<List<MessageAnthropic>> GetMessagesFromDb(int truncateAfter, int msgCount, bool includeToolMessages, string databaseName = "")
                {
                    var msgList = await _messageDb.LoadMessages(trunkMax: truncateAfter, maxMsgCount: msgCount, includeTools: includeToolMessages);
                    return msgList ?? new List<MessageAnthropic>();
                }

                var dbMessageList = await GetMessagesFromDb(truncateAfter, msgCount, includeToolMessages, databaseName);

                if (dbMessageList == null || dbMessageList.Count == 0) return;

                var jsonString = await GetMessagesAsJson(dbMessageList);
                Debug.WriteLine($"\n\nPre Cleanup of messages JSON\n\n{jsonString}\n\n");

                // Process conversation history to clean up and validate structure
                var postProcessedMessageList = _anthropicApi.ProcessConversationHistory(dbMessageList);
                if (postProcessedMessageList == null) return;

                // Verify proper user/assistant alternation (required by Anthropic API)
                var verificationResult = _anthropicApi.VerifyMessageAlternation(postProcessedMessageList);
                if (!verificationResult)
                {
                    await ChatMessage(ChatUser.System, $"Warning: Bad Alternation or corruption in the context history. No context will be loaded!");
                    return;
                }

                // Add verified messages to conversation history
                _userMessageList.AddRange(postProcessedMessageList);

                jsonString = await GetMessagesAsJson(_userMessageList);
                Debug.WriteLine($"\n\nPost Cleanup of messages JSON\n\n{jsonString}\n\n");

                // Display loaded conversation in the UI
                await DisplayMessageHistory(messageList: postProcessedMessageList, showToolMessages: (verificationResult && includeToolMessages));
            }
            catch (Exception ex)
            {
                _userMessageList = new List<MessageAnthropic>();
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Medium, "LoadContextHistory", this);
            }
        }

        /// <summary>
        /// Displays conversation history in the chat UI, optionally showing tool messages.
        /// Filters out ping/keepalive messages for cleaner display.
        /// </summary>
        /// <param name="messageList">List of messages to display</param>
        /// <param name="showToolMessages">Whether to display tool_use and tool_result messages</param>
        /// <returns>True if messages were displayed, false otherwise</returns>
        private async Task<bool> DisplayMessageHistory(List<MessageAnthropic> messageList, bool showToolMessages = false)
        {
            try
            {
                if (!messageList.Any()) return false;

                if (messageList == null || messageList.Count == 0)
                {
                    await ChatMessage(ChatUser.System, "No messages to display.");
                    return false;
                }

                // Iterate through each message in the history
                foreach (var message in messageList)
                {
                    // Skip null or empty messages
                    if (message == null || message.content == null || !message.content.Any()) continue;

                    var hasTools = message.content.Any(c => c is ToolUseContent || c is ToolResultContentList || c is ToolResultMessage);

                    // Display each content block in the message
                    foreach (var content in message.content)
                    {
                        switch (content)
                        {
                            case MessageContent textContent:
                                // Display text content (filter out ping messages)
                                if (textContent.text != null && !string.IsNullOrEmpty(textContent.text) && !string.IsNullOrWhiteSpace(textContent.text) &&
                                    !textContent.text.StartsWith("This is a 'ping'") && !textContent.text.StartsWith("ping ack"))
                                {
                                    await ChatMessage(message.role == "user" ? ChatUser.User : ChatUser.Assistant,
                                        $"{textContent.text}"
                                    );
                                }
                                break;

                            case ToolUseContent toolUseContent when (showToolMessages):
                                // Display tool_use with formatted JSON input
                                if (toolUseContent.input == null) continue;

                                var toolInputJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                                    toolUseContent.input,
                                    Newtonsoft.Json.Formatting.Indented
                                );

                                await ChatMessage(ChatUser.Assistant, $"Tool Use:{toolUseContent.id}\n{toolInputJson}");
                                break;

                            case ToolResultContentList toolResultContent when (showToolMessages):
                                // Display tool_result with numbered steps
                                if (toolResultContent.content == null) continue;

                                var steps = toolResultContent.content
                                    .OfType<MessageContent>()
                                    .Select((m, i) => $"{i + 1}. {m.text?.Trim()}")
                                    .ToList();

                                if (steps.Count > 0)
                                {
                                    var resultPrefix = toolResultContent.is_error == true ? "Error: " : "result: ";
                                    var combinedMessage = $"{resultPrefix}\n{toolResultContent.tool_use_id}\n{string.Join("\n", steps)}";
                                    await ChatMessage(ChatUser.User, $"Tool Result:{combinedMessage}");
                                }
                                break;

                            case ToolResultMessage toolResultMsg when (showToolMessages):
                                // Display simple tool result message
                                var toolResultPrefix = toolResultMsg.is_error ? "Error: " : "result: ";
                                await ChatMessage(ChatUser.User, $"Tool Result Message:{toolResultPrefix}\n{toolResultMsg.tool_use_id}\n{toolResultMsg.content}");
                                break;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.Medium, "DisplayMessageHistory", this);
                return false;
            }
        }

        /// <summary>
        /// Resets the streaming state flags. Called when a content block or thinking block completes.
        /// </summary>
        private void ResetMessageStates()
        {
            _hasStartedContent = false;
            _hasStartedThinking = false;
        }

        #endregion Chat Message and Logging

        private async Task<Task> InvokeOnUIAsync(Func<Task> asyncAction)
        {
            return await ThreadHelper.InvokeOnUIThreadAsync(this, asyncAction);
        }

        /// <summary>
        /// Handles keyboard shortcuts for the textRequest textbox.
        /// Ctrl+Enter triggers the send button.
        /// </summary>
        private void textRequest_KeyDown(object sender, KeyEventArgs e)
        {
            // Check if Ctrl+Enter is pressed
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                // Prevent the default behavior (adding a new line)
                e.SuppressKeyPress = true;

                // Trigger the send button click
                btnSend_Click(sender, e);
            }
        }

        /// <summary>
        /// Send button click handler. Sends user message to Claude or stops an ongoing request.
        /// Button toggles between "Send" and "Stop" states.
        /// </summary>
        private async void btnSend_Click(object sender, EventArgs e)
        {
            if (btnSend.Text == "Send")
            {
                // Ignore empty messages
                if (string.IsNullOrWhiteSpace(textRequest.Text)) return;

                btnSend.Text = "Stop";

                string requestText = textRequest.Text.Trim();
                textRequest.Clear();

                if (!string.IsNullOrWhiteSpace(requestText))
                {
                    await Task.Run(async () =>
                    {
                        try
                        {
                            // Start cache keepalive timer if not already running
                            if (!_timerCacheAlive.IsRunning)
                            {
                                await StartTimerAsync();
                            }

                            // Send the message to Anthropic
                            SendAnthropic(requestText);
                        }
                        catch (Exception ex)
                        {
                            await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "btnSend_Click", this);
                        }
                    }).ContinueWith(t =>
                    {
                        // Reset UI state when done (runs on UI thread)
                        btnSend.Text = "Send";

                        if (t.IsFaulted)
                        {
                            MessageBox.Show("An error occurred while processing your request.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                else
                {
                    btnSend.Text = "Send";
                }
            }
            else
            {
                // Stop button clicked - cancel ongoing request
                try
                {
                    if (_anthropicApi != null) _anthropicApi.RequestStop();
                    Debug.WriteLine("Anthropic stop signal sent.");
                }
                catch (Exception ex)
                {
                    await ErrorHandlerToolBufferDemo.HandleError(ex, ErrorHandlerToolBufferDemo.ErrorSeverity.High, "RequestStop", this);
                }

                btnSend.Text = "Send";
            }
        }

        /// <summary>
        /// Menu item handler for exiting the application.
        /// </summary>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        /// <summary>
        /// Form closing event handler. Saves settings before exit.
        /// </summary>
        private void FormAnthropicDemo_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                SettingsManager.SaveSettings(_settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings on close: {ex.Message}");
            }
        }
    }
}