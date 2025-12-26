
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{

    public class StreamResponse
    {

        private string _type;

        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                ResponseType = StreamResponseTypeExtensions.FromJsonValue(value);
            }
        }

        [JsonIgnore]
        public StreamingEventType ResponseType { get; private set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public AnthropicResponse? Message { get; set; }

        [JsonProperty("index", NullValueHandling = NullValueHandling.Ignore)]
        public int index { get; set; }

        [JsonProperty("Error", NullValueHandling = NullValueHandling.Ignore)]
        public ErrorClass? Error { get; set; }

        [JsonProperty("content_block", NullValueHandling = NullValueHandling.Ignore)]
        public ContentBlock? ContentBlock { get; set; }

        [JsonProperty("delta", NullValueHandling = NullValueHandling.Ignore)]
        public Delta? Delta { get; set; }
    }

    public class ErrorClass
    {
        [JsonProperty("type")]
        public string type { get; set; } = "error";


        [JsonProperty("error")]
        public string error { get; set; } = string.Empty;


        [JsonProperty("message")]
        public string message { get; set; } = string.Empty;
    }



        public enum StreamingEventType
    {
        MessageStart,
        ContentBlockStart,
        Ping,
        ContentBlockDelta,
        ContentBlockStop,
        MessageDelta,
        MessageStop,
        Error,
        Usage,
        Status,
        RawData,
        InteractionComplete,
        Warning,
        Debug,
        CodeBlock,
        ThinkingBlockStart,
        ThinkingBlockDelta,
        ThinkingBlockStop,
        RedactedThinkingBlockStart,
        // New event types for cancellation
        Cancelled,
        StopRequested
    }

    public static class StreamResponseTypeExtensions
    {
        public static string ToJsonValue(this StreamingEventType type)
        {
            return type switch
            {
                StreamingEventType.MessageStart => "message_start",
                StreamingEventType.ContentBlockStart => "content_block_start",
                StreamingEventType.Ping => "ping",
                StreamingEventType.ContentBlockDelta => "content_block_delta",
                StreamingEventType.ContentBlockStop => "content_block_stop",
                StreamingEventType.MessageDelta => "message_delta",
                StreamingEventType.MessageStop => "message_stop",
                StreamingEventType.Error => "error",
                _ => throw new ArgumentException($"Unknown stream response type: {type}")
            };
        }

        public static StreamingEventType FromJsonValue(string value)
        {
            return value switch
            {
                "message_start" => StreamingEventType.MessageStart,
                "content_block_start" => StreamingEventType.ContentBlockStart,
                "ping" => StreamingEventType.Ping,
                "content_block_delta" => StreamingEventType.ContentBlockDelta,
                "content_block_stop" => StreamingEventType.ContentBlockStop,
                "message_delta" => StreamingEventType.MessageDelta,
                "message_stop" => StreamingEventType.MessageStop,
                "error" => StreamingEventType.Error,
                _ => throw new ArgumentException($"Unknown stream response type string: {value}")
            };
        }



    }


 






}
