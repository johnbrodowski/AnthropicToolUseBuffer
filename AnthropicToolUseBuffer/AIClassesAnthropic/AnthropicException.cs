using System;
using System.Net;
using System.Runtime.Serialization;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace AnthropicToolUseBuffer
{
    /// <summary>
    /// Base exception for all Anthropic API related errors
    /// </summary>
    [Serializable]
    public class AnthropicException : Exception
    {
        public string RequestId { get; }
        public DateTime Timestamp { get; }

        public AnthropicException(string message, string requestId = null)
            : base(message)
        {
            RequestId = requestId ?? Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
        }

        public AnthropicException(string message, Exception innerException, string requestId = null)
            : base(message, innerException)
        {
            RequestId = requestId ?? Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
        }

        protected AnthropicException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            RequestId = info.GetString(nameof(RequestId));
            Timestamp = info.GetDateTime(nameof(Timestamp));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(RequestId), RequestId);
            info.AddValue(nameof(Timestamp), Timestamp);
        }
    }

    /// <summary>
    /// Exception thrown when API requests fail with HTTP errors
    /// </summary>
    [Serializable]
    public class AnthropicApiException : AnthropicException
    {
        public HttpStatusCode? StatusCode { get; }
        public string ResponseContent { get; }
        public string HttpMethod { get; }
        public string RequestUrl { get; }

        public AnthropicApiException(
            string message,
            HttpStatusCode? statusCode = null,
            string responseContent = null,
            string httpMethod = null,
            string requestUrl = null,
            string requestId = null)
            : base(message, requestId)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
            HttpMethod = httpMethod;
            RequestUrl = requestUrl;
        }

        public AnthropicApiException(
            string message,
            Exception innerException,
            HttpStatusCode? statusCode = null,
            string responseContent = null,
            string httpMethod = null,
            string requestUrl = null,
            string requestId = null)
            : base(message, innerException, requestId)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
            HttpMethod = httpMethod;
            RequestUrl = requestUrl;
        }

        protected AnthropicApiException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            StatusCode = (HttpStatusCode?)info.GetValue(nameof(StatusCode), typeof(HttpStatusCode?));
            ResponseContent = info.GetString(nameof(ResponseContent));
            HttpMethod = info.GetString(nameof(HttpMethod));
            RequestUrl = info.GetString(nameof(RequestUrl));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(StatusCode), StatusCode);
            info.AddValue(nameof(ResponseContent), ResponseContent);
            info.AddValue(nameof(HttpMethod), HttpMethod);
            info.AddValue(nameof(RequestUrl), RequestUrl);
        }
    }

    /// <summary>
    /// Exception thrown for validation errors in requests or responses
    /// </summary>
    [Serializable]
    public class AnthropicValidationException : AnthropicException
    {
        public string ValidationField { get; }
        public object ValidationValue { get; }

        public AnthropicValidationException(
            string message,
            string validationField = null,
            object validationValue = null,
            string requestId = null)
            : base(message, requestId)
        {
            ValidationField = validationField;
            ValidationValue = validationValue;
        }

        protected AnthropicValidationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ValidationField = info.GetString(nameof(ValidationField));
            ValidationValue = info.GetValue(nameof(ValidationValue), typeof(object));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ValidationField), ValidationField);
            info.AddValue(nameof(ValidationValue), ValidationValue);
        }
    }

    /// <summary>
    /// Exception thrown when stream processing fails
    /// </summary>
    [Serializable]
    public class AnthropicStreamException : AnthropicException
    {
        public string StreamData { get; }
        public long StreamPosition { get; }

        public AnthropicStreamException(
            string message,
            string streamData = null,
            long streamPosition = -1,
            string requestId = null)
            : base(message, requestId)
        {
            StreamData = streamData;
            StreamPosition = streamPosition;
        }

        public AnthropicStreamException(
            string message,
            Exception innerException,
            string streamData = null,
            long streamPosition = -1,
            string requestId = null)
            : base(message, innerException, requestId)
        {
            StreamData = streamData;
            StreamPosition = streamPosition;
        }

        protected AnthropicStreamException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            StreamData = info.GetString(nameof(StreamData));
            StreamPosition = info.GetInt64(nameof(StreamPosition));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(StreamData), StreamData);
            info.AddValue(nameof(StreamPosition), StreamPosition);
        }
    }

    /// <summary>
    /// Exception thrown when configuration is invalid
    /// </summary>
    [Serializable]
    public class AnthropicConfigurationException : AnthropicException
    {
        public string ConfigurationKey { get; }

        public AnthropicConfigurationException(string message, string configurationKey = null)
            : base(message)
        {
            ConfigurationKey = configurationKey;
        }

        protected AnthropicConfigurationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ConfigurationKey = info.GetString(nameof(ConfigurationKey));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ConfigurationKey), ConfigurationKey);
        }
    }

    /// <summary>
    /// Centralized error handler for Anthropic API operations
    /// </summary>
    public class AnthropicErrorHandler
    {
        private readonly ILogger<AnthropicErrorHandler> _logger;

        public AnthropicErrorHandler(ILogger<AnthropicErrorHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles HTTP response errors and creates appropriate exceptions
        /// </summary>
        public AnthropicApiException HandleHttpError(
            HttpResponseMessage response,
            string requestContent = null,
            string requestId = null)
        {
            var statusCode = response.StatusCode;
            var method = response.RequestMessage?.Method?.Method;
            var url = response.RequestMessage?.RequestUri?.ToString();

            string responseContent = null;
            try
            {
                responseContent = response.Content?.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read error response content for request {RequestId}", requestId);
            }

            var errorMessage = CreateHttpErrorMessage(statusCode, responseContent);

            _logger.LogError(
                "HTTP error {StatusCode} for {Method} {Url}. Request ID: {RequestId}. Response: {ResponseContent}",
                statusCode, method, url, requestId, responseContent);

            return new AnthropicApiException(
                errorMessage,
                statusCode,
                responseContent,
                method,
                url,
                requestId);
        }

        /// <summary>
        /// Handles validation errors
        /// </summary>
        public AnthropicValidationException HandleValidationError(
            string message,
            string field = null,
            object value = null,
            string requestId = null)
        {
            _logger.LogWarning(
                "Validation error for request {RequestId}: {Message}. Field: {Field}, Value: {Value}",
                requestId, message, field, value);

            return new AnthropicValidationException(message, field, value, requestId);
        }

        /// <summary>
        /// Handles stream processing errors
        /// </summary>
        public AnthropicStreamException HandleStreamError(
            string message,
            Exception innerException = null,
            string streamData = null,
            long position = -1,
            string requestId = null)
        {
            _logger.LogError(innerException,
                "Stream processing error for request {RequestId} at position {Position}: {Message}. Data: {StreamData}",
                requestId, position, message, streamData);

            return new AnthropicStreamException(message, innerException, streamData, position, requestId);
        }

        /// <summary>
        /// Handles configuration errors
        /// </summary>
        public AnthropicConfigurationException HandleConfigurationError(
            string message,
            string configurationKey = null)
        {
            _logger.LogError(
                "Configuration error for key {ConfigurationKey}: {Message}",
                configurationKey, message);

            return new AnthropicConfigurationException(message, configurationKey);
        }

        /// <summary>
        /// Handles unexpected errors
        /// </summary>
        public AnthropicException HandleUnexpectedError(
            Exception exception,
            string context = null,
            string requestId = null)
        {
            var message = $"Unexpected error occurred";
            if (!string.IsNullOrEmpty(context))
                message += $" in {context}";
            message += $": {exception.Message}";

            _logger.LogError(exception,
                "Unexpected error for request {RequestId} in context {Context}",
                requestId, context);

            return new AnthropicException(message, exception, requestId);
        }

        private string CreateHttpErrorMessage(HttpStatusCode statusCode, string responseContent)
        {
            var message = $"HTTP {(int)statusCode} {statusCode}";

            // Try to extract meaningful error message from response
            if (!string.IsNullOrEmpty(responseContent))
            {
                try
                {
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    if (errorResponse?.error?.message != null)
                    {
                        message += $": {errorResponse.error.message}";
                    }
                    else if (errorResponse?.message != null)
                    {
                        message += $": {errorResponse.message}";
                    }
                }
                catch
                {
                    // If JSON parsing fails, include raw response if it's not too long
                    if (responseContent.Length <= 200)
                    {
                        message += $": {responseContent}";
                    }
                }
            }

            return message;
        }
    }

    /// <summary>
    /// Extensions for logging Anthropic-specific events
    /// </summary>
    public static class AnthropicLoggingExtensions
    {
        private static readonly Action<ILogger, string, string, Exception> _logApiRequest =
            LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(1001, "AnthropicApiRequest"),
                "Sending Anthropic API request {RequestId} to {Endpoint}");

        private static readonly Action<ILogger, string, TimeSpan, Exception> _logApiResponse =
            LoggerMessage.Define<string, TimeSpan>(
                LogLevel.Debug,
                new EventId(1002, "AnthropicApiResponse"),
                "Received Anthropic API response for request {RequestId} in {Duration}");

        private static readonly Action<ILogger, string, string, Exception> _logStreamingEvent =
            LoggerMessage.Define<string, string>(
                LogLevel.Trace,
                new EventId(1003, "AnthropicStreamingEvent"),
                "Streaming event {EventType} for request {RequestId}");

        private static readonly Action<ILogger, string, string, Exception> _logValidationWarning =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(1004, "AnthropicValidationWarning"),
                "Validation warning for request {RequestId}: {Message}");

        public static void LogApiRequest(this ILogger logger, string requestId, string endpoint)
        {
            _logApiRequest(logger, requestId, endpoint, null);
        }

        public static void LogApiResponse(this ILogger logger, string requestId, TimeSpan duration)
        {
            _logApiResponse(logger, requestId, duration, null);
        }

        public static void LogStreamingEvent(this ILogger logger, string eventType, string requestId)
        {
            _logStreamingEvent(logger, eventType, requestId, null);
        }

        public static void LogValidationWarning(this ILogger logger, string requestId, string message)
        {
            _logValidationWarning(logger, requestId, message, null);
        }
    }

    /// <summary>
    /// Structured logging context for Anthropic operations
    /// </summary>
    public class AnthropicLoggingContext : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IDisposable _scope;

        public string RequestId { get; }
        public string Operation { get; }
        public DateTime StartTime { get; }

        public AnthropicLoggingContext(ILogger logger, string operation, string requestId = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            RequestId = requestId ?? Guid.NewGuid().ToString();
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            StartTime = DateTime.UtcNow;

            _scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = RequestId,
                ["Operation"] = Operation,
                ["StartTime"] = StartTime
            });

            _logger.LogDebug("Started operation {Operation} with request ID {RequestId}", Operation, RequestId);
        }

        public void LogProgress(string message, params object[] args)
        {
            _logger.LogDebug($"[{Operation}] {message}", args);
        }

        public void LogWarning(string message, params object[] args)
        {
            _logger.LogWarning($"[{Operation}] {message}", args);
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            _logger.LogError(exception, $"[{Operation}] {message}", args);
        }

        public void Dispose()
        {
            var duration = DateTime.UtcNow - StartTime;
            _logger.LogDebug("Completed operation {Operation} with request ID {RequestId} in {Duration}ms",
                Operation, RequestId, duration.TotalMilliseconds);
            _scope?.Dispose();
        }
    }
}

// Usage examples:

/*
// In your main AnthropicApiClass, inject the error handler:
public class AnthropicApiClass
{
    private readonly AnthropicErrorHandler _errorHandler;
    private readonly ILogger<AnthropicApiClass> _logger;

    public AnthropicApiClass(string apiKey, AnthropicErrorHandler errorHandler, ILogger<AnthropicApiClass> Logger)
    {
        _apiKeyAnthropic = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _logger = Logger ?? throw new ArgumentNullException(nameof(Logger));
    }

    public async Task SendAnthropicApiRequestStreamAsync(...)
    {
        using var loggingContext = new AnthropicLoggingContext(_logger, "SendStreamRequest");
        
        try
        {
            // Validate parameters
            if (messageList == null)
                throw _errorHandler.HandleValidationError("messageList cannot be null", 
                    nameof(messageList), null, loggingContext.RequestId);

            loggingContext.LogProgress("Sending request to Anthropic API");
            
            // ... your existing code ...
            
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw _errorHandler.HandleHttpError(httpResponse, requestBody, loggingContext.RequestId);
            }
            
            // ... rest of processing ...
        }
        catch (AnthropicException)
        {
            throw; // Re-throw our custom exceptions
        }
        catch (JsonException ex)
        {
            throw _errorHandler.HandleStreamError("JSON parsing failed", ex, 
                requestId: loggingContext.RequestId);
        }
        catch (Exception ex)
        {
            throw _errorHandler.HandleUnexpectedError(ex, "SendStreamRequest", loggingContext.RequestId);
        }
    }
}

// Register in DI container:
services.AddScoped<AnthropicErrorHandler>();
services.AddLogging(builder => builder.AddConsole().AddDebug());
*/
