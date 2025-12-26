using AnthropicToolUseBuffer.ToolClasses;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.Data;
using System.Diagnostics;
using System.Text;

namespace AnthropicToolUseBuffer.AIClassesAnthropic
{
    public class AnthropicApiClass : IDisposable
    {
        // Configuration constants
        private const double TEMPERATURE_WITH_THINKING = 1.0;
        private const double TEMPERATURE_WITHOUT_THINKING = 0.2;
        private const int THINKING_BUDGET_TOKENS = 15000;
        private const int MAX_TOKENS_WITH_THINKING = 25000;
        private const int MAX_TOKENS_SONNET = 10000;
        private const int MAX_TOKENS_DEFAULT = 8000;

        // Placeholder text constants
        private const string PLACEHOLDER_USER_TEXT = "placeholder for missing user text message";
        private const string PLACEHOLDER_USER_TOOL_RESULT = "placeholder for missing user tool result message";
        private const string PLACEHOLDER_ASSISTANT = "placeholder for missing assistant message";
        private const string PLACEHOLDER_PREFIX = "placeholder for missing";

        // Content type constants
        private const string CONTENT_TYPE_TEXT = "text";
        private const string CONTENT_TYPE_TOOL_USE = "tool_use";
        private const string CONTENT_TYPE_TOOL_RESULT = "tool_result";
        private const string CONTENT_TYPE_THINKING = "thinking";
        private const string CONTENT_TYPE_REDACTED_THINKING = "redacted_thinking";
        private const string CONTENT_TYPE_SERVER_TOOL_USE = "server_tool_use";

        private CancellationTokenSource _cancellationTokenSource;

        private string _apiKeyAnthropic;
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        private readonly string url = "https://api.anthropic.com/v1/messages";

        /// <summary>
        /// Event triggered when an error occurs during non-streaming API requests.
        /// </summary>
        public event EventHandler<(string ErrorMessage, string requestId)>? ErrorResponse;

        /// <summary>
        /// Event triggered when an error occurs during streaming API requests.
        /// </summary>
        public event EventHandler<(string ErrorMessage, string requestId)>? ErrorResponseStream;

        private readonly List<Func<object, AnthropicResponse, List<MessageAnthropic>, string, Task>> completedStreamHandlers = new();

        /// <summary>
        /// Event triggered for various streaming events during API communication.
        /// </summary>
        public event EventHandler<(StreamingEventType EventType, string? Content, string? Message, string? Json)>? StreamingEvent;

        private readonly List<Func<object, AnthropicResponse, List<MessageAnthropic>, string, Task>> completedHandlers = new();

        /// <summary>
        /// Event triggered when an API response is completed.
        /// </summary>
        public event Func<object, AnthropicResponse, List<MessageAnthropic>, string, Task> ResponseCompleted
        {
            add { lock (completedHandlers) { completedHandlers.Add(value); } }
            remove { lock (completedHandlers) { completedHandlers.Remove(value); } }
        }
 
        protected async Task OnResponseCompletedAsync(AnthropicResponse anthropicResponse, List<MessageAnthropic> updatedMessageList, string requestId = "00000")
        {
            var handlersCopy = completedHandlers.ToList();
            foreach (var handler in handlersCopy)
            {
                try
                {
                    await handler(this, anthropicResponse, updatedMessageList, requestId);
                }
                catch (Exception ex)
                {
                    TriggerStreamingEvent(StreamingEventType.Error, $"An error occurred while executing the completed handler: {ex.Message} {requestId}");
                }
            }
        }

        private void TriggerStreamingEvent(StreamingEventType eventType, string? content = null, string? message = null, string? json = null)
        {
            StreamingEvent?.Invoke(this, (eventType, content, message, json));
        }

        public AnthropicApiClass(string apiKey)
        {
            this._apiKeyAnthropic = apiKey ?? throw new ArgumentNullException(nameof(apiKey), "API key cannot be null.");
        }

        /// <summary>
        /// Sends a streaming API request to the Anthropic API with the specified parameters.
        /// </summary>
        /// <param name="requestParameters">The parameters for the API request including messages, model, and configuration options.</param>
        public async Task SendAnthropicApiRequestStreamAsync( AiRequestParametersAnthropic requestParameters)
        { 
            _cancellationTokenSource?.Dispose();
             
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            var anthropicResponse = new AnthropicResponse();

            try
            {
                ValidateConfiguration();

                ValidateInputParameters(requestParameters);

                EnsureValidMessageStructure(requestParameters);

                HandleCachingStrategies(requestParameters);

                var requestBody = CreateAnthropicRequestBody( requestParameters);

                cancellationToken.ThrowIfCancellationRequested();

                LogRequestDetails(requestBody);

                var requestMessage = CreateHttpRequestMessage(requestBody);

                using (var httpResponse = await client.SendAsync( requestMessage, HttpCompletionOption.ResponseHeadersRead,  cancellationToken))
                {
                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var content = await httpResponse.Content.ReadAsStringAsync();
                        var statusCode = (int)httpResponse.StatusCode;

                        TriggerStreamingEvent(StreamingEventType.Error,  $"HTTP Error {statusCode}\nResponse: {content}");

                        return; 
                    }
 
                    using (var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken))
                    using (var reader = new StreamReader(stream))
                    {
 
                        await ProcessResponseStreamWithCancellation(reader, requestParameters.UserMessageList, anthropicResponse, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            { 
                TriggerStreamingEvent(StreamingEventType.Cancelled, "Request was cancelled");
            }
            catch (ArgumentException ex)
            {
                HandleValidationError(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                HandleApiError($"HTTP error: {ex.Message}");
            }
            catch (JsonException ex)
            {
                HandleApiError($"JSON parsing error: {ex.Message}");
            }
            catch (Exception ex)
            {
                HandleUnexpectedError(ex);
            }
            finally
            {
                await FinalizeResponseProcessing(anthropicResponse, requestParameters.UserMessageList);
            }
        }
 
        /// <summary>
        /// Requests the current streaming operation to stop gracefully.
        /// </summary>
        public void RequestStop()
        {
            TriggerStreamingEvent(StreamingEventType.StopRequested, "User requested to stop generation");
            CancelStreamingRequest();
        }

 
        private async Task ProcessResponseStreamWithCancellation(
    StreamReader reader,
    List<MessageAnthropic> messages,  
    AnthropicResponse response,  
    CancellationToken cancellationToken)
        {
            StringBuilder accumulatedText = new StringBuilder();
            StringBuilder accumulatedJSON = new StringBuilder(); 
            StringBuilder accumulatedThinking = new StringBuilder();

            string currentThinkingSignature = null;
            bool initialChunk = true;
 


            try
            {
                while (!cancellationToken.IsCancellationRequested && await reader.ReadLineAsync() is { } line)
                {
                    if (!line.StartsWith("data:")) continue;

                    var jsonData = line["data:".Length..].Trim();
                    if (string.IsNullOrWhiteSpace(jsonData)) continue;
                    
                    var streamData = JsonConvert.DeserializeObject<StreamResponse>(jsonData);
                    if (streamData == null) continue;

                    cancellationToken.ThrowIfCancellationRequested();

                    switch (streamData.ResponseType)
                    {
                        case StreamingEventType.MessageStart when streamData.Message != null:
                            response.id = streamData.Message.id;
                            response.type = streamData.Message.type;
                            response.role = streamData.Message.role;
                            response.model = streamData.Message.model;
                            response.content = new List<Content>(); 
                            if (streamData.Message.usage != null) response.usage = streamData.Message.usage;
                            TriggerStreamingEvent(StreamingEventType.MessageStart);
                            break;

                        case StreamingEventType.ContentBlockStart:
                     
                            if (streamData.ContentBlock?.Type == "tool_use")  
                            {
                                response.content?.Add(new Content
                                {
                                    type = streamData.ContentBlock.Type,
                                    id = streamData.ContentBlock.Id,
                                    name = streamData.ContentBlock.Name,
                                    input = new ToolInput(), 
                                    Index = streamData.index

                                });
                                response.ToolUsed = true;
                                response.stop_reason = "tool_use";  
                            }
                            else if (streamData.ContentBlock?.Type == "server_tool_use")  
                            {
                                response.content?.Add(new Content  
                                {
                                    type = streamData.ContentBlock.Type,  
                                    id = streamData.ContentBlock.Id,      
                                    name = streamData.ContentBlock.Name,   
                                    input = new ToolInput(),  
                                    Index = streamData.index
                                });
                                response.ToolUsed = true;
                                response.stop_reason = "tool_use";  
                            }
                            else if (streamData.ContentBlock?.Type == "text")
                            {
                                initialChunk = true;
                            }
                            else if (streamData.ContentBlock?.Type == "thinking")
                            {
                                initialChunk = true;
                                accumulatedThinking.Clear();
                                currentThinkingSignature = null;
                            }
                            else if (streamData.ContentBlock?.Type == "redacted_thinking")
                            {
                                if (!string.IsNullOrEmpty(streamData.ContentBlock.Data))
                                {
                                    response.content?.Add(new Content
                                    {
                                        type = "redacted_thinking",
                                        text = streamData.ContentBlock.Data, // Store data in text field for simplicity
                                        Index = streamData.index
                                    });
                                }
                            }

                            TriggerStreamingEvent(StreamingEventType.ContentBlockStart, null, streamData.ContentBlock?.Type);
                            break;

                        case StreamingEventType.ContentBlockDelta:
                           
                            if (streamData.Delta?.type == "input_json_delta" && 
                                response.content?.LastOrDefault(c => c.type == "server_tool_use") is Content serverToolUseContent)
                            {
                                accumulatedJSON.Append(streamData.Delta?.partial_json);
                                TriggerStreamingEvent(StreamingEventType.ContentBlockDelta, streamData.Delta?.partial_json, "server_tool_input_json");
                            }
                            else if (!string.IsNullOrEmpty(streamData.Delta?.text))
                            {
                                var deltaText = initialChunk ? streamData.Delta.text.TrimStart('\n') : streamData.Delta.text;
                                accumulatedText.Append(deltaText);
                               
                                Debug.Write($"{deltaText }");
                                TriggerStreamingEvent(StreamingEventType.ContentBlockDelta, deltaText, "text");
                                initialChunk = false;
                            }
                            else if (!string.IsNullOrEmpty(streamData.Delta?.partial_json)) // For client-side tool_use
                            {
                                accumulatedJSON.Append(streamData.Delta.partial_json);
                                Debug.Write($"{streamData.Delta.partial_json}");
                                TriggerStreamingEvent(StreamingEventType.ContentBlockDelta, streamData.Delta.partial_json, "json");
                            }
                            else if (!string.IsNullOrEmpty(streamData.Delta?.thinking))
                            {
                                accumulatedThinking.Append(streamData.Delta.thinking);
                                Debug.Write($"{streamData.Delta.thinking}");
                                TriggerStreamingEvent(StreamingEventType.ContentBlockDelta, streamData.Delta.thinking, "thinking");
                            }
                            else if (!string.IsNullOrEmpty(streamData.Delta?.signature))
                            {
                                currentThinkingSignature = streamData.Delta.signature;
                                Debug.Write($"{currentThinkingSignature}");
                                TriggerStreamingEvent(StreamingEventType.ContentBlockDelta, null, "signature");
                            }
                            break;

                        case StreamingEventType.ContentBlockStop:
                            if (response.content?.LastOrDefault()?.type == "server_tool_use" && accumulatedJSON.Length > 0)
                            {
                                // Finalize input for server_tool_use
                                var lastServerToolContent = response.content.Last(c => c.type == "server_tool_use");
                                try
                                {
                                    // Assuming the accumulated JSON is the direct input object for ToolInput
                                    // For web_search, this would be {"query": "..."}
                                    lastServerToolContent.input = JsonConvert.DeserializeObject<ToolInput>(accumulatedJSON.ToString());
                                    TriggerStreamingEvent(StreamingEventType.ContentBlockStop, accumulatedJSON.ToString(), "server_tool_input_json" );
                                }
                                catch (JsonException ex)
                                {
                                    Debug.WriteLine($"JSON parse error for server_tool_use input: {ex.Message}");
                                    TriggerStreamingEvent(StreamingEventType.Error, $"JSON error for server tool input: {ex.Message}" );
                                }
                                accumulatedJSON.Clear();
                            }
                            else if (accumulatedText.Length > 0) // Regular text block
                            {
                                response.content?.Add(new Content
                                {
                                    type = "text",
                                    text = accumulatedText.ToString()
                                });
                                TriggerStreamingEvent(StreamingEventType.ContentBlockStop, accumulatedText.ToString(), "text");
                                accumulatedText.Clear();
                            }
                            else if (accumulatedJSON.Length > 0) // Client-side tool_use JSON
                            {
                                var lastToolContent = response.content?.LastOrDefault(c => c.type == "tool_use");
                                if (lastToolContent != null)
                                {
                                    try
                                    {
                                        lastToolContent.input = JObject.Parse(accumulatedJSON.ToString()).ToObject<ToolInput>();
                                        TriggerStreamingEvent(StreamingEventType.ContentBlockStop, accumulatedJSON.ToString(), "json");
                                    }
                                    catch (JsonException ex)
                                    {
                                        Debug.WriteLine($"JSON parse error for client tool_use input: {ex.Message}");
                                        TriggerStreamingEvent(StreamingEventType.Error, $"JSON error for client tool input: {ex.Message}");
                                    }
                                }
                                accumulatedJSON.Clear();
                            }
                            else if (accumulatedThinking.Length > 0)
                            {
                                response.content?.Add(new Content
                                {
                                    type = "thinking",
                                    text = accumulatedThinking.ToString(),
                                    signature = currentThinkingSignature,
                                    Index = streamData.index
                                });
                                TriggerStreamingEvent(StreamingEventType.ContentBlockStop, accumulatedThinking.ToString(), "thinking");
                                accumulatedThinking.Clear();
                                currentThinkingSignature = null;
                            }
                            break;

                        case StreamingEventType.MessageDelta:
                            if (streamData.Delta != null)
                            {
                                response.stop_reason = streamData.Delta.stop_reason ?? response.stop_reason;
                                response.stop_sequence = streamData.Delta.stop_sequence ?? response.stop_sequence;
                                response.usage = streamData.Delta.usage ?? response.usage; // Update usage if present
                            }
                            TriggerStreamingEvent(StreamingEventType.MessageDelta);
                            break;

                        case StreamingEventType.MessageStop:
                            TriggerStreamingEvent(StreamingEventType.MessageStop);
                            break;

                        case StreamingEventType.Error: // API level error event
                            var errorDetails = streamData.Error != null ? $"{streamData.Error.type}: {streamData.Error.message}" : "Unknown API error";
                            TriggerStreamingEvent(StreamingEventType.Error, errorDetails);
                            return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                TriggerStreamingEvent(StreamingEventType.Cancelled, "Generation was cancelled by the user", "cancelled");
                response.stop_reason = "cancelled_by_user";
                if (accumulatedText.Length > 0)
                {
                    response.content?.Add(new Content { type = "text", text = accumulatedText.ToString() + " [Generation stopped by user]"});
                    accumulatedText.Clear();
                }
                return;
            }

            if (response.stop_reason != "tool_use" && !cancellationToken.IsCancellationRequested)
            {
                var assistantMessage = new MessageAnthropic(Roles.Assistant);

                var thinkingContents = response.content?.Where(c => c.type == "thinking" || c.type == "redacted_thinking").ToList();
               
                if (thinkingContents != null && thinkingContents.Any())
                {
                    foreach (var thinkingContent in thinkingContents)
                    {
                        if (thinkingContent.type == "thinking")
                        {
                            assistantMessage.content.Add(new ThinkingContent
                            {
                                ThinkingText = thinkingContent.text,
                                Signature = thinkingContent.signature
                            });
                        }
                        else if (thinkingContent.type == "redacted_thinking")
                        {
                            assistantMessage.content.Add(new RedactedThinkingContent
                            {
                                Data = thinkingContent.text // Assuming text holds the data for redacted
                            });
                        }
                    }
                }

                foreach (var contentItem in response.content ?? new List<Content>())
                {
                    if (contentItem.type == "text")
                    {
                        assistantMessage.content.Add(new MessageContent  
                        {
                            text = contentItem.text
                        });
                    }
                    else if (contentItem.type == "tool_use" && contentItem.id != null) // Client-side tool
                    {
                        assistantMessage.content.Add(new ToolUseContent
                        {
                            id = contentItem.id,
                            name = contentItem.name,
                            input = contentItem.input
                        });
                        response.ToolUsed = true; // Mark client-side tool use
                    }
                }

                TriggerStreamingEvent(StreamingEventType.InteractionComplete);
            } 
        }
 
        /// <summary>
        /// Cancels the current streaming request immediately.
        /// </summary>
        public void CancelStreamingRequest()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                TriggerStreamingEvent(StreamingEventType.Cancelled, "Request was cancelled by user");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during cancellation: {ex.Message}");
            }
        }


        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_apiKeyAnthropic))
            {
                throw new InvalidOperationException("API key not configured");
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("API endpoint not configured");
            }
        }


        private void LogRequestDetails(RequestBody requestBody)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                var json = JsonConvert.SerializeObject(requestBody, settings);
                Debug.Print($"Request payload:\n{json}"); 
            }
            catch (Exception ex)
            {
                TriggerStreamingEvent(StreamingEventType.Warning,
                    $"Failed to log request: {ex.Message}");
            }
        }
 
        private void ValidateInputParameters(AiRequestParametersAnthropic? requestParameters = null)
        {
            if (requestParameters?.UserMessageList == null) throw new ArgumentNullException(nameof(requestParameters.UserMessageList));
        }
 
        private void EnsureValidMessageStructure(AiRequestParametersAnthropic requestParameters)
        {
            while (requestParameters.UserMessageList.Count > 0 && requestParameters.UserMessageList[^1].role != Roles.User)
            {
                requestParameters.UserMessageList.RemoveAt(requestParameters.UserMessageList.Count - 1);
            }
        }

        private void HandleCachingStrategies(AiRequestParametersAnthropic parameters)
        {
            if (!parameters.UseCache) return;

            HandleToolCaching( parameters);
            HandleSystemMessageCaching( parameters);
            HandleMessageCaching(parameters);
        }

        private void HandleToolCaching(  AiRequestParametersAnthropic parameters)
        {
            if (parameters.UseTools && parameters.CacheTools && parameters.Tools?.Count > 0)
            {
                parameters.Tools[^1].cache_control = new CacheControl { type = "ephemeral" };
            }
        }

        private void HandleSystemMessageCaching( AiRequestParametersAnthropic parameters)
        {
            if (parameters.CacheSystem && parameters.SystemMessageList?.Count > 0)
            {
                parameters.SystemMessageList[^1].CacheControl = new CacheControl { type = "ephemeral" };
            }
        }

        private void HandleMessageCaching( AiRequestParametersAnthropic parameters)
        {
            if (!parameters.CacheMessages) return;

            var (lastUserMessage, secondToLastUserMessage) = FindLastUserMessages(parameters.UserMessageList);
            ApplyMessageCacheControls(parameters.UserMessageList, lastUserMessage, secondToLastUserMessage);
        }

        private (MessageAnthropic? last, MessageAnthropic? secondLast) FindLastUserMessages(List<MessageAnthropic> messages)
        {
            MessageAnthropic? last = null;
            MessageAnthropic? secondLast = null;
            int count = 0;

            for (int i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].role != Roles.User) continue;

                if (count == 0) last = messages[i];
                else if (count == 1) secondLast = messages[i];
                if (++count >= 2) break;
            }
            return (last, secondLast);
        }

        private void ApplyMessageCacheControls(List<MessageAnthropic> messages, MessageAnthropic? last, MessageAnthropic? secondLast)
        {
            foreach (var message in messages.Where(m => m.role == Roles.User))
            {
                var content = message.content.FirstOrDefault(c =>
                    c.type == CONTENT_TYPE_TEXT || c.type == CONTENT_TYPE_TOOL_RESULT);

                if (content == null) continue;

                if (message == last || message == secondLast)
                {
                    content.CacheControl = new CacheControl { type = "ephemeral" };
                }
                else
                {
                    content.CacheControl = null;
                }
            }
        }

        private RequestBody CreateAnthropicRequestBody( AiRequestParametersAnthropic requestParameters)
        {

            requestParameters.Temperature = requestParameters.UseThinking ? TEMPERATURE_WITH_THINKING : TEMPERATURE_WITHOUT_THINKING;
            requestParameters.ThinkingBudgetTokens = THINKING_BUDGET_TOKENS;
            requestParameters.MaxTokens = requestParameters.UseThinking ? MAX_TOKENS_WITH_THINKING : requestParameters.Model == ModelOption.Claude4Sonnet ? MAX_TOKENS_SONNET : MAX_TOKENS_DEFAULT;
            requestParameters.UseStopSequences = false;

            Thinking? thinking = null;


            if (requestParameters.Model == ModelOption.Claude4Sonnet)
            {
                thinking = requestParameters.UseThinking ? new Thinking
                {
                    type = "enabled",
                    BudgetTokens = requestParameters.ThinkingBudgetTokens
                }
                : new Thinking
                {
                    type = "disabled"
                };
            }

            var stopSequences = requestParameters.UseStopSequences
                 ? new List<string>()
                 : null;

            return new RequestBody(
                model: requestParameters.Model,
                maxTokens: requestParameters.MaxTokens,
                thinking: thinking,
                temperature: (requestParameters.UseThinking && thinking != null) ? TEMPERATURE_WITH_THINKING : TEMPERATURE_WITHOUT_THINKING,
                stopSequences: stopSequences,
                tools: requestParameters.UseTools ? requestParameters.Tools : null,
                system: requestParameters.SystemMessageList,
                messages: requestParameters.UserMessageList,
                toolChoice: requestParameters.ToolChoice,
                stream: requestParameters.Stream
            );
        }

        private HttpRequestMessage CreateHttpRequestMessage(RequestBody requestBody)
        {
            if (string.IsNullOrEmpty(_apiKeyAnthropic))
                throw new InvalidOperationException("API key is not configured");

            var requestMessage = requestBody.ToHttpRequestMessage(url);
            requestMessage.Headers.Add("x-api-key", _apiKeyAnthropic);
            requestMessage.Headers.Add("anthropic-version", "2023-06-01");
            requestMessage.Headers.Add("anthropic-beta", "files-api-2025-04-14");
            //  requestMessage.Headers.Add("anthropic-beta", "fine-grained-tool-streaming-2025-05-14");
            requestMessage.Headers.Add("anthropic-beta", "extended-cache-ttl-2025-04-11");
            return requestMessage;
        }

        private void HandleValidationError(string message)
        {
            TriggerStreamingEvent(StreamingEventType.Error, $"Validation error: {message}");
        }

        private void HandleApiError(string errorMessage)
        {
            TriggerStreamingEvent(StreamingEventType.Error, errorMessage);
        }

        private void HandleUnexpectedError(Exception ex)
        {
            TriggerStreamingEvent(StreamingEventType.Error,
                $"Unexpected error: {ex.Message}. Stack trace: {ex.StackTrace}");
        }
 
        private async Task FinalizeResponseProcessing(AnthropicResponse apiResponseObject, List<MessageAnthropic> conversationHistory)
        {
            try
            {
                if (apiResponseObject.role == Roles.Assistant && apiResponseObject.content.Any())
                {
                    var assistantTurnMessage = new MessageAnthropic(Roles.Assistant);

                    foreach (var contentBlockFromApi in apiResponseObject.content)
                    {
                        IMessageContentAnthropic iMessageContentBlock = null;
                        switch (contentBlockFromApi.type)
                        {
                            case MessageType.Text:
                                var textContent = new MessageContent
                                {
                                    text = contentBlockFromApi.text
                                };

                                iMessageContentBlock = textContent;
                                break;
                            case MessageType.ToolUse:
                            case "server_tool_use": // Handle both client and server-side tool use
                                iMessageContentBlock = new ToolUseContent
                                {
                                    type = contentBlockFromApi.type, // Ensure this type is correctly set
                                    id = contentBlockFromApi.id,
                                    name = contentBlockFromApi.name,
                                    input = contentBlockFromApi.input,
                                    text = contentBlockFromApi.text // If tool_use can have associated text
                                                                    // CacheControl might need to be sourced
                                };
                                break;
                            case MessageType.Thinking:
                                iMessageContentBlock = new ThinkingContent
                                {
                                    ThinkingText = contentBlockFromApi.text, // Assuming 'text' holds thinking content
                                    Signature = contentBlockFromApi.signature
                                };
                                break;
                            case MessageType.RedactedThinking:
                                iMessageContentBlock = new RedactedThinkingContent
                                {
                                    // Assuming 'Data' or 'text' holds redacted content.
                                    // 'Content' class has 'Data' and 'text'. 'RedactedThinkingContent' has 'Data'.
                                    Data = contentBlockFromApi.Data ?? contentBlockFromApi.text
                                };
                                break;
                            // Add other types if necessary (e.g., image)
                            default:
                                Debug.WriteLine($"[AnthropicApiClass.FinalizeResponseProcessing] Warning: Unhandled content block type '{contentBlockFromApi.type}' in assistant response.");
                                // Create a generic text block for unknown types if necessary, or skip
                                // For now, skipping unknown types.
                                break;
                        }

                        if (iMessageContentBlock != null)
                        {
                            assistantTurnMessage.content.Add(iMessageContentBlock);
                        }
                    }

                    // Only add the assistantTurnMessage if it actually has content after mapping
                    if (assistantTurnMessage.content.Any())
                    {
                        //conversationHistory.Add(assistantTurnMessage); // Add this complete turn to the history

                        var settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                        string assistantTurnJson = JsonConvert.SerializeObject(assistantTurnMessage, settings);
                        Debug.WriteLine($"[AnthropicApiClass.FinalizeResponseProcessing] Added assistant turn to history:\n{assistantTurnJson}");
                    }
                    else if (apiResponseObject.content.Any()) // Original response had content, but mapping resulted in none
                    {
                        Debug.WriteLine($"[AnthropicApiClass.FinalizeResponseProcessing] Warning: Assistant response had content blocks, but none were successfully mapped to IMessageContent for history.");
                    }
                }
                else if (apiResponseObject.role == Roles.Assistant && !apiResponseObject.content.Any())
                {
                    TriggerStreamingEvent(StreamingEventType.Warning, "Assistant response object received but has no content blocks to add to history.");
                }

                string effectiveRequestId = apiResponseObject.RequestID ?? apiResponseObject.id ?? "unknown_request_id";
                await OnResponseCompletedAsync(apiResponseObject, conversationHistory, effectiveRequestId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AnthropicApiClass.FinalizeResponseProcessing: {ex.Message}\n{ex.StackTrace}");
                TriggerStreamingEvent(StreamingEventType.Error, $"Error in finalization: {ex.Message}");
            }
        }
 
 

 
        /// <summary>
        /// Processes and validates conversation history to ensure proper message alternation between user and assistant.
        /// </summary>
        /// <param name="messages">The list of messages to process and validate.</param>
        /// <returns>A cleaned and validated list of messages with proper alternation.</returns>
        public List<MessageAnthropic>? ProcessConversationHistory(List<MessageAnthropic> messages)
        {

            var tmpMessages = new List<MessageAnthropic>();

            tmpMessages.AddRange(messages);

            if (tmpMessages == null || tmpMessages.Count == 0)
                return tmpMessages;

            // Step 1: Clean tmpMessages of duplicates, empty content, etc.
            tmpMessages = CleanMessages(tmpMessages);

            // Step 2: Collapse consecutive placeholders and placeholder sequences
            tmpMessages = CollapseRepeatingPatterns(tmpMessages);


            //// Step 3: Ensure proper alternation
            tmpMessages = EnsureMessageAlternation(tmpMessages);     // Newest and Working.
 
            // Step 4: Remove any remaining placeholder sequences
            tmpMessages = RemovePlaceholderDoubleSequences(tmpMessages);
            tmpMessages = RemovePlaceholderTripleSequences(tmpMessages);
  
            // Step 5: Remove any Placeholder Sandwiches
            tmpMessages = RemovePlaceholderSandwiches(tmpMessages);
            tmpMessages = ConsolidateConsecutiveUserMessages(tmpMessages);
             
             // Step 6: Ensure proper start and end
            tmpMessages = EnsureProperStartAndEnd(tmpMessages);
 
            // Step 7: Final verification
            var verificationResult = VerifyMessageAlternationDetailed(tmpMessages);


            if (!verificationResult.IsValid)
            {
                // Handle invalid tmpMessages by removing problematic ones
                tmpMessages = ForceValidAlternation(tmpMessages);
            }
 
            messages.Clear();
            messages.AddRange(tmpMessages);

            return messages;
        }
       
        public List<MessageAnthropic> CleanMessages(List<MessageAnthropic> messages)
        {
            if (messages == null || !messages.Any())
            {
                return new List<MessageAnthropic>();
            }

            var result = new List<MessageAnthropic>();

            foreach (var message in messages)
            {
                // Skip null messages
                if (message == null || message.content == null)
                {
                    continue;
                }

                var newMessage = new MessageAnthropic(message.role);
                var uniqueTextSet = new HashSet<string>(); // For tracking duplicate text content
                bool hasNonEmptyContent = false;
                bool hasToolContent = message.content.Any(c => c != null &&
                                                         (GetContentType(c) == CONTENT_TYPE_TOOL_USE ||
                                                          GetContentType(c) == CONTENT_TYPE_TOOL_RESULT));

                // Process all content items in the message
                foreach (var contentItem in message.content)
                {
                    // Skip null content items
                    if (contentItem == null)
                    {
                        continue;
                    }

                    string contentType = GetContentType(contentItem);

                    // Handle text content
                    if (contentType == CONTENT_TYPE_TEXT)
                    {
                        // Get the text from the appropriate type
                        string text = "";
                        if (contentItem is MessageContent mc)
                        {
                            text = mc.text;
                        }

                        // Skip empty text content
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            continue;
                        }

                        // Skip duplicate text content
                        if (!uniqueTextSet.Add(text))
                        {
                            continue;
                        }

                        hasNonEmptyContent = true;
                    }
                    // Handle non-text content (including tool_use and tool_result)
                    else if (!string.IsNullOrEmpty(contentType))
                    {
                        hasNonEmptyContent = true;
                    }
                    // Skip unknown or empty content types
                    else
                    {
                        continue;
                    }

                    // Add content to the new message
                    newMessage.content.Add(contentItem);
                }

                // Only add messages that have non-empty content
                if (hasNonEmptyContent && newMessage.content.Any())
                {
                    result.Add(newMessage);
                }
            }

            return result;
        }
  
        private List<MessageAnthropic> CollapseRepeatingPatterns(List<MessageAnthropic> messages)
        {
            if (messages == null || messages.Count < 3)
                return messages;

            var result = new List<MessageAnthropic>();

            // First, identify any repeating patterns
            for (int i = 0; i < messages.Count; i++)
            {
                var current = messages[i];

                // If we have at least 2 messages already in the result and current message would create
                // a repeating A-B-A pattern where B is a placeholder, collapse it
                if (result.Count >= 2 &&
                    result[result.Count - 2].role == current.role &&
                    IsPlaceholder(result[result.Count - 1]))
                {
                    // Remove the placeholder and keep only the latest real message
                    result.RemoveAt(result.Count - 1);
                    result[result.Count - 1] = current;
                }
                else
                {
                    // Add the current message normally
                    result.Add(current);
                }
            }

            return result;
        }
         
        public List<MessageAnthropic> EnsureMessageAlternation(List<MessageAnthropic> messages)
        {
            if (messages == null || !messages.Any())
            {
                return new List<MessageAnthropic>();
            }

            // Step 1: Filter out any messages that are null or have no content.
            var validMessages = messages.Where(msg => msg != null && msg.content != null && msg.content.Any(c => c != null)).ToList();
            if (!validMessages.Any())
            {
                return new List<MessageAnthropic>();
            }

            var fixedMessages = new List<MessageAnthropic>();

            // Step 2: Ensure the conversation starts with a User role.
            // If the first message is from the assistant, insert a user placeholder.
            if (validMessages.First().role == Roles.Assistant)
            {
                fixedMessages.Add(CreatePlaceholder(Roles.User, CONTENT_TYPE_TEXT));
            }

            foreach (var currentMessage in validMessages)
            {
                if (!fixedMessages.Any())
                {
                    fixedMessages.Add(currentMessage);
                    continue;
                }

                var lastMessage = fixedMessages.Last();

                // Step 3: Check if the current message's role is the same as the last one.
                // This indicates a break in the user-assistant alternation that needs fixing.
                if (lastMessage.role == currentMessage.role)
                {
                    // Case 1: We have two consecutive Assistant messages.
                    // This means a User response is missing.
                    if (currentMessage.role == Roles.Assistant)
                    {
                        // Check if the previous assistant message was a tool_use call.
                        var lastToolUseContent = lastMessage.content.FirstOrDefault(c => GetContentType(c) == CONTENT_TYPE_TOOL_USE) as ToolUseContent;
                        if (lastToolUseContent != null)
                        {
                            // If yes, the missing user message should be a 'tool_result'.
                            // We create a placeholder for that result, using the ID from the tool call.
                            fixedMessages.Add(CreatePlaceholder(Roles.User, CONTENT_TYPE_TOOL_RESULT, lastToolUseContent.id));
                        }
                        else
                        {
                            // If no, it was a standard text message, so we're missing a standard user text response.
                            fixedMessages.Add(CreatePlaceholder(Roles.User, CONTENT_TYPE_TEXT));
                        }
                    }
                    // Case 2: We have two consecutive User messages.
                    // This means an Assistant response is missing.
                    else // currentMessage.role == Roles.User
                    {
                        fixedMessages.Add(CreatePlaceholder(Roles.Assistant, CONTENT_TYPE_TEXT));
                    }
                }

                // After potentially inserting a placeholder to fix the sequence, add the current message.
                fixedMessages.Add(currentMessage);
            }

            // Step 4: Ensure the conversation ends with a valid sequence.
            var finalMessage = fixedMessages.LastOrDefault();
            if (finalMessage != null)
            {
                // If the last message is a user message, it needs an assistant reply.
                if (finalMessage.role == Roles.User)
                {
                    fixedMessages.Add(CreatePlaceholder(Roles.Assistant, CONTENT_TYPE_TEXT));
                }
                else // The last message is from the assistant
                {
                    // If the last assistant message is a 'tool_use' call, it requires a 'tool_result' from the user to be valid.
                    var lastToolUseContent = finalMessage.content.FirstOrDefault(c => GetContentType(c) == CONTENT_TYPE_TOOL_USE) as ToolUseContent;
                    if (lastToolUseContent != null)
                    {
                        fixedMessages.Add(CreatePlaceholder(Roles.User, CONTENT_TYPE_TOOL_RESULT, lastToolUseContent.id));
                    }
                }
            }

            return fixedMessages;
        }
 
        private List<MessageAnthropic> RemovePlaceholderSandwiches(List<MessageAnthropic> messages)
        {
            if (messages == null || messages.Count < 3)
            {
                return messages;
            }

            var result = new List<MessageAnthropic>();
            int i = 0;

            while (i < messages.Count)
            {
                // Check if there are at least 3 messages left to form a potential sandwich
                if (i + 2 < messages.Count)
                {
                    var first = messages[i];
                    var second = messages[i + 1];
                    var third = messages[i + 2];

                    // Check for the Placeholder -> Real MessageAnthropic -> Placeholder pattern
                    if (IsPlaceholder(first) && !IsPlaceholder(second) && IsPlaceholder(third))
                    {
                        // Pattern found. Skip all three messages by advancing the index.
                        i += 3;
                        continue; // Continue to the next iteration of the loop
                    }
                }

                // If the pattern is not found, add the current message to the result
                // and advance the index by one.
                result.Add(messages[i]);
                i++;
            }

            return result;
        }
 
        private List<MessageAnthropic> ConsolidateConsecutiveUserMessages(List<MessageAnthropic> messages)
        {
            if (messages == null || !messages.Any())
            {
                return new List<MessageAnthropic>();
            }

            var result = new List<MessageAnthropic>();
            int i = 0;

            while (i < messages.Count)
            {
                var currentMessage = messages[i];

                // If the message is from the assistant, just add it and continue.
                if (currentMessage.role == Roles.Assistant)
                {
                    result.Add(currentMessage);
                    i++;
                    continue;
                }

                // If we've found a User message, look ahead to find where the
                // block of consecutive user messages ends.
                int userBlockEndIndex = i;
                while (userBlockEndIndex + 1 < messages.Count && messages[userBlockEndIndex + 1].role == Roles.User)
                {
                    userBlockEndIndex++;
                }

                // Add ONLY the last user message from that block to the result.
                result.Add(messages[userBlockEndIndex]);

                // Move the main index past the entire block of user messages we just processed.
                i = userBlockEndIndex + 1;
            }

            return result;
        }
 
        private List<MessageAnthropic> RemovePlaceholderDoubleSequences(List<MessageAnthropic> messages)
        {
            if (messages == null || messages.Count < 2)
                return messages;

            var result = new List<MessageAnthropic>();
            bool lastWasPlaceholder = false;

            foreach (var message in messages)
            {
                bool isPlaceholder = IsPlaceholder(message);

                // Only add this placeholder if the previous message wasn't a placeholder
                if (isPlaceholder)
                {
                    if (!lastWasPlaceholder)
                    {
                        result.Add(message);
                    }
                    // If last message was already a placeholder, skip this one
                }
                else
                {
                    // Always add non-placeholder messages
                    result.Add(message);
                }

                lastWasPlaceholder = isPlaceholder;
            }

            return result;
        }
 
        private List<MessageAnthropic> RemovePlaceholderTripleSequences(List<MessageAnthropic> messages)
        {
            if (messages == null || messages.Count < 2)
                return messages;

            var result = new List<MessageAnthropic>();
            int i = 0;

            while (i < messages.Count)
            {
                // Check if we have at least 3 messages to evaluate
                if (i + 2 < messages.Count)
                {
                    var first = messages[i];
                    var second = messages[i + 1];
                    var third = messages[i + 2];

                    if (IsPlaceholder(first) && !IsPlaceholder(second) && IsPlaceholder(third))
                    {
                        // Only keep the first placeholder
                        result.Add(first);
                        i += 3; // Skip the next two
                        continue;
                    }
                }

                // Default behavior: add the message
                result.Add(messages[i]);
                i++;
            }

            return result;
        }
 
        public List<MessageAnthropic> EnsureProperStartAndEnd(List<MessageAnthropic> messages)
        {
            if (messages == null || !messages.Any())
                return new List<MessageAnthropic>();

            // Create a new list to store the fixed messages
            var fixedMessages = new List<MessageAnthropic>(messages);

            // Check if the first message is from Assistant, insert a User placeholder at the beginning
            if (fixedMessages.Any() && fixedMessages.First().role == Roles.Assistant)
            {
                // Insert a placeholder User message at the beginning
                fixedMessages.Insert(0, CreatePlaceholder(Roles.User, CONTENT_TYPE_TEXT));
            }

            // Check if the last message is from User, append an Assistant placeholder at the end
            if (fixedMessages.Any() && fixedMessages.Last().role == Roles.User)
            {
                // Add a placeholder Assistant message at the end
                fixedMessages.Add(CreatePlaceholder(Roles.Assistant, CONTENT_TYPE_TEXT));
            }

            // Verify the alternation pattern (for debugging purposes)
            for (int i = 0; i < fixedMessages.Count - 1; i++)
            {
                string currentRole = fixedMessages[i].role;
                string nextRole = fixedMessages[i + 1].role;

                if ((currentRole == Roles.User && nextRole != Roles.Assistant) ||
                    (currentRole == Roles.Assistant && nextRole != Roles.User))
                {
                    // Found an alternation error
                    // Note: You could throw an exception here or log this issue,
                    // but for now let's just continue and assume the previous steps fixed most issues
                }
            }

            return fixedMessages;
        }
 
        public bool VerifyMessageAlternation(List<MessageAnthropic>? messages)
        {
            // Check if the list is empty
            if (messages == null || !messages.Any())
                return false;

            // Verify that the first message is from the user
            if (messages.First().role != Roles.User)
                return false;

            // Verify that the last message is from the assistant
            if (messages.Last().role != Roles.Assistant)
                return false;

            // Check that roles alternate properly throughout the list
            for (int i = 0; i < messages.Count - 1; i++)
            {
                // Current message's role
                string currentRole = messages[i].role;

                // Next message's role
                string nextRole = messages[i + 1].role;

                // If current is user, next must be assistant
                if (currentRole == Roles.User && nextRole != Roles.Assistant)
                    return false;

                // If current is assistant, next must be user
                if (currentRole == Roles.Assistant && nextRole != Roles.User)
                    return false;
            }

            // All checks passed
            return true;
        }
 
        public (bool IsValid, string ErrorMessage) VerifyMessageAlternationDetailed(List<MessageAnthropic> messages)
        {
            // Check if the list is empty
            if (messages == null || !messages.Any())
                return (false, "Message list is empty or null");

            // Verify that the first message is from the user
            if (messages.First().role != Roles.User)
                return (false, $"First message is not from User (role: {messages.First().role})");

            // Verify that the last message is from the assistant
            if (messages.Last().role != Roles.Assistant)
                return (false, $"Last message is not from Assistant (role: {messages.Last().role})");

            // Check that roles alternate properly throughout the list
            for (int i = 0; i < messages.Count - 1; i++)
            {
                // Current message's role
                string currentRole = messages[i].role;

                // Next message's role
                string nextRole = messages[i + 1].role;

                // If current is user, next must be assistant
                if (currentRole == Roles.User && nextRole != Roles.Assistant)
                    return (false, $"Message alternation error at index {i}: User message followed by {nextRole} message instead of Assistant");

                // If current is assistant, next must be user
                if (currentRole == Roles.Assistant && nextRole != Roles.User)
                    return (false, $"Message alternation error at index {i}: Assistant message followed by {nextRole} message instead of User");
            }

            // All checks passed
            return (true, "Message alternation is valid");
        }

        private List<MessageAnthropic> ForceValidAlternation(List<MessageAnthropic> messages)
        {
            if (messages == null || !messages.Any())
                return new List<MessageAnthropic>();

            var result = new List<MessageAnthropic>();

            // First, ensure we have a user message to start with
            int startIndex = 0;
            while (startIndex < messages.Count && messages[startIndex].role != Roles.User)
            {
                startIndex++;
            }

            // If we couldn't find a user message, return empty list
            if (startIndex >= messages.Count)
                return new List<MessageAnthropic>();

            // Add the first user message
            result.Add(messages[startIndex]);
            string expectedRole = Roles.Assistant;

            // Process the rest of the messages
            for (int i = startIndex + 1; i < messages.Count; i++)
            {
                var message = messages[i];

                // Only add messages that match the expected alternating pattern
                if (message.role == expectedRole)
                {
                    result.Add(message);
                    expectedRole = expectedRole == Roles.User ? Roles.Assistant : Roles.User;
                }
            }

            // Ensure the conversation ends with an assistant message
            if (result.Last().role != Roles.Assistant)
            {
                result.Add(CreatePlaceholder(Roles.Assistant, CONTENT_TYPE_TEXT));
            }

            return result;
        }

        private MessageAnthropic CreatePlaceholder(string role, string expectedContentType, string toolUseId = null)
        {
            var placeholder = new MessageAnthropic(role);
            if (role == Roles.User)
            {
                if (expectedContentType == CONTENT_TYPE_TEXT)
                {
                    placeholder.content.Add(new MessageContent { text = PLACEHOLDER_USER_TEXT });
                }
                else if (expectedContentType == CONTENT_TYPE_TOOL_RESULT)
                {
                    // Ensure a valid tool use id is provided.
                    if (string.IsNullOrEmpty(toolUseId))
                    {
                        toolUseId = "tool_unknown"; // Fallback ID if none provided
                        //  throw new ArgumentException("A valid tool use id is required when creating a tool_result placeholder");
                    }
                    placeholder.content.Add(new ToolResultContentList
                    {
                        tool_use_id = toolUseId,
                        content = new List<IMessageContentAnthropic>
                {
                    new MessageContent { text = PLACEHOLDER_USER_TOOL_RESULT }
                },
                        is_error = false
                    });
                }
            }
            else if (role == Roles.Assistant)
            {
                // For an assistant placeholder, default to a text message.
                placeholder.content.Add(new MessageContent { text = PLACEHOLDER_ASSISTANT });
            }
            return placeholder;
        }
 
        private bool IsPlaceholder(MessageAnthropic message)
        {
            if (message.content.Count != 1)
                return false;

            var content = message.content[0];
            if (content is MessageContent mc)
            {
                return mc.text.StartsWith(PLACEHOLDER_PREFIX);
            }
            else if (content is ToolResultContentList trc)
            {
                if (trc.content.Count != 1 || !(trc.content[0] is MessageContent trcMc))
                    return false;

                return trcMc.text.StartsWith(PLACEHOLDER_PREFIX);
            }

            return false;
        }
 
        private string GetContentType(IMessageContentAnthropic content)
        {
            if (content is MessageContent mc) return mc.type;
            if (content is ToolUseContent tuc) return tuc.type;
            if (content is ToolResultContentList trc) return trc.type;
            if (content is ThinkingContent) return CONTENT_TYPE_THINKING;
            if (content is RedactedThinkingContent) return CONTENT_TYPE_REDACTED_THINKING;
            return string.Empty;
        }

        /// <summary>
        /// Disposes the resources used by the AnthropicApiClass.
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

    }
}


