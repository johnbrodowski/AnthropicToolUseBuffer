
using AnthropicToolUseBuffer.ToolClasses;

using Newtonsoft.Json;

namespace AnthropicToolUseBuffer
{
  
        public class AiRequestParametersAnthropic
        {

        /// <summary>
        /// Whether to use caching for optimizing API requests
        /// </summary>
        public bool UseCache { get; set; } = true;

        /// <summary>
        /// Whether to cache tool definitions
        /// </summary>
        public bool CacheTools { get; set; } = true;

        /// <summary>
        /// Whether to cache system messages
        /// </summary>
        public bool CacheSystem { get; set; } = true;

        /// <summary>
        /// Whether to cache message content
        /// </summary>
        public bool CacheMessages { get; set; } = true;

        /// <summary>
        /// Whether to enable tools
        /// </summary>
        public bool UseTools { get; set; } = true;

        /// <summary>
        /// Model to use for API requests
        /// </summary>
        public ModelOption Model { get; set; } = ModelOption.Claude45Haiku;

        /// <summary>
        /// Maximum tokens in the response
        /// </summary>
        public int MaxTokens { get; set; } = 15000;

        /// <summary>
        /// Temperature for response generation (0.0 to 1.0)
        /// </summary>
        public double Temperature { get; set; } = 1.0;

        /// <summary>
        /// Tool choice mode
        /// </summary>
        public ToolChoice ToolChoice { get; set; } = ToolChoice.Auto;

        /// <summary>
        /// Whether to use stop sequences
        /// </summary>
        public bool UseStopSequences { get; set; } = false;
        /// <summary>
        /// Whether to enable thinking mode
        /// </summary>
        public bool UseThinking { get; set; } = false;

        /// <summary>
        /// Budget for thinking tokens when thinking is enabled
        /// </summary>
        public int ThinkingBudgetTokens { get; set; } = 5000;

        /// <summary>
        /// Whether to stream the response
        /// </summary>
        public bool Stream { get; set; } = true;


 
        public string? ToolUseId { get; set; } = null;

        public IMessageContentAnthropic MessageContent { get; set; }

        public MessageAnthropic UserMessage { get; set; } = new MessageAnthropic("user");

        public SystemMessage SystemMessage { get; set; } = new SystemMessage("user");

        public List<MessageAnthropic> UserMessageList { get; set; } = new List<MessageAnthropic>();

        public List<SystemMessage>? SystemMessageList { get; set; } = null;

        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<Tool>? Tools { get; set; }

    }
 
}
