using AnthropicToolUseBuffer.ToolClasses;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace AnthropicToolUseBuffer
{
    public class RequestBody
    {
        [JsonIgnore]
        public ModelOption ModelOption { get; set; }

        [JsonIgnore]
        public ToolChoice? ToolChoice { get; set;}

        [JsonProperty("model", NullValueHandling = NullValueHandling.Ignore)]
        public string Model => ModelOption.Value;

        [JsonProperty("max_tokens", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxTokens { get; set;}

        [JsonProperty("thinking", NullValueHandling = NullValueHandling.Ignore)]
        public Thinking? thinking { get; set;}

        [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
        public double? Temperature { get; set;}

        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<Tool>? Tools { get; set;}

        [JsonProperty("stop_sequences", NullValueHandling = NullValueHandling.Ignore)]
        public List<string>? stopSequences { get; set; } = null;

        [JsonProperty("system", NullValueHandling = NullValueHandling.Ignore)]
        public List<SystemMessage>? System { get; set;}

        [JsonProperty("tool_choice", NullValueHandling = NullValueHandling.Ignore)]
        public object? ToolChoiceObject
        {
            get
            {
                // If it's "tool", must include name:
                if (ToolChoice != null && ToolChoice?.Value == "tool")
                {
                    if (string.IsNullOrWhiteSpace(ToolChoice.Name))
                    {
                        throw new InvalidOperationException("When toolChoice is 'tool', you must provide a tool name.");
                    }
                    return new
                    {
                        type = ToolChoice.Value,
                        name = ToolChoice.Name
                    };
                }
                else
                {
                    // For "any" or "auto"
                   // return new { type = ToolChoice.Value };
                    return null;
                }
            }
        } 

        [JsonProperty("messages", NullValueHandling = NullValueHandling.Ignore)]
        public List<MessageAnthropic>? Messages { get; set;}

        [JsonProperty("stream", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Stream { get; set;}

         

        public RequestBody(
            ModelOption model,
            int? maxTokens = null,
            Thinking? thinking = null,
            double? temperature = null,
            List<string>? stopSequences = null,
            List<Tool>? tools = null,
            List<SystemMessage>? system = null,
            List<MessageAnthropic>? messages = null,
            ToolChoice? toolChoice = null,
            bool stream = true)
        {
            // Validate ModelOption
            if (!ModelOption.AllModels.Contains(model))
            {
                throw new ArgumentException(
                    $"Invalid model: {model.Value}. Must be one of: {string.Join(", ", ModelOption.AllModels.Select(m => m.Value))}");
            }

            // Validate toolChoice
            if (toolChoice != null && toolChoice.Value != "auto" && toolChoice.Value != "any" && toolChoice.Value != "tool")
            {

                toolChoice = null;
                throw new ArgumentException("Invalid tool choice. Must be 'auto', 'any', or 'tool'.");
            }

            this.ModelOption = model;
            this.MaxTokens = maxTokens;
            this.thinking = thinking;
            this.Temperature = temperature;
            this.stopSequences = stopSequences;
            this.Tools = tools;
            this.System = system;
            this.Messages = messages;
            this.ToolChoice = toolChoice;
            this.Stream = stream;
        }

        public HttpRequestMessage ToHttpRequestMessage(string url)
        {
            var jsonBody = JsonConvert.SerializeObject(this, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            return new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
