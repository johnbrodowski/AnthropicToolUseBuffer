using AnthropicToolUseBuffer.ToolClasses;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AnthropicToolUseBuffer
{

    public class MessageAnthropic
    {
        [JsonProperty("role")]
        public string role { get; set; }

        [JsonProperty("content")]
        public List<IMessageContentAnthropic> content { get; set; }

        public MessageAnthropic(string role)
        {
            this.role = role;
            content = new List<IMessageContentAnthropic>();
        }

        public static MessageAnthropic CreateUserMessage(string text)
        {
            var message = new MessageAnthropic(Roles.User);
            message.content.Add(new MessageContent
            {
                text = text
            });

            return message;
        }



        public static MessageAnthropic CreateSearchMessage(string text)
        {
            var message = new MessageAnthropic(Roles.User);

            message.content.Add(new MessageContent
            {
                text = text
            });

 
            var _tools = new ServerSideTools
            {
                tools = new List<IMessageContentAnthropic>
                {
                    new ServerTool
                    {
                        type = MessageType.WebSearch,
                        name = "web_search",
                        max_uses = 5
                    }
                }
            };
 
            return message;
        }







        public static MessageAnthropic CreateSystemTextMessage(string text, CacheControl? cacheControl = null)
        {
            var message = new MessageAnthropic(Roles.System);
            message.content.Add(new MessageContent
            {
                text = text,
                CacheControl = cacheControl
            });

            return message;
        }


        public static MessageAnthropic CreateUserImageMessage(string text, string imageData)
        {
            var message = new MessageAnthropic(Roles.User);

            message.content.Add(new ImageContent
            {
                source = new Source
                {
                    media_type = ImageData.MediaType.Png,
                    data = imageData
                }
            });


            //message.content.Add( new ImageContent
            //{ 
            //    source = new Source
            //    {
            //        media_type = ImageData.MediaType.Jpeg,
            //        data = imageData
            //    }
            //});

 
            message.content.Add(new MessageContent
            {
                text = text
            });

            return message;
        }

        public static MessageAnthropic CreateToolResultImageMessage(string toolUseId, string text, string imageData, bool isError)
        {
            var message = new MessageAnthropic(Roles.User);

            var toolContent = new List<IMessageContentAnthropic>();

            toolContent.Add(new MessageContent
            {
                text = text
            });


            toolContent.Add(new ImageContent
            {
                source = new Source
                {
                    media_type = ImageData.MediaType.Jpeg,
                    data = imageData
                }
            });

            message.content.Add(new ToolResultContentList
            {
                tool_use_id = toolUseId,
                content = toolContent,
                is_error = isError
            });


            return message;
        }


  
  
         
        public static MessageAnthropic CreateToolResultMessage(string toolUseId, string text, bool isError)
        {
            var message = new MessageAnthropic(Roles.User);

            message.content.Add(new ToolResultMessage
            {
                tool_use_id = toolUseId,
                content = text,
                is_error = isError
            });

            return message;
        }

        public static MessageAnthropic CreateToolResultMessage(string toolUseId, List<IMessageContentAnthropic> toolContent, bool isError = false)
        {
            var message = new MessageAnthropic(Roles.User);

            message.content.Add(new ToolResultContentList
            {
                tool_use_id = toolUseId,
                content = toolContent,
                is_error = isError
            });

            if (isError)
            {
                message.content.Add(new MessageContent
                {
                    text = "Task completed with errors, prompt the user on how to proceed"
                });
            }
            else
            {
                //message.content.Add(new MessageContent
                //{
                //    text = "Task complete, prompt the user on how to proceed"
                //});
            }

            return message;
        }

         

        public static MessageAnthropic CreateAssistantTextMessage(string text, CacheControl? cacheControl = null)
        {
            var message = new MessageAnthropic(Roles.Assistant);
            message.content.Add(new MessageContent
            {
                text = text,
                CacheControl = cacheControl
            });

            return message;
        }
 
        public static MessageAnthropic CreateToolUseMessage(string toolId, string text, ToolInput? input, CacheControl? cacheControl = null)
        {
            var message = new MessageAnthropic(Roles.Assistant);
            message.content.Add(new ToolUseContent
            {
                text = text,
                id = toolId,
                input = input,
                CacheControl = cacheControl
            });

            return message;
        }

        public static MessageAnthropic CreateUserFileMessage(string text, string fileId, string fileType = "document")
        {
            var message = new MessageAnthropic(Roles.User);

            message.content.Add(new FileContent(fileId, fileType));

            message.content.Add(new MessageContent
            {
                text = text
            });

            return message;
        }



    }

    public class MessageContent : IMessageContentAnthropic
    {
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string type { get; } = MessageType.Text;

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string? text { get; set; }

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? CacheControl { get; set; }
    }


    public class ServerSideTools : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.WebSearch;

        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<IMessageContentAnthropic>? tools { get; set; }
 
        [JsonIgnore]
        public string? text { get; set; }

        [JsonIgnore]
        public CacheControl? CacheControl { get; set; }
    }


    public class ServerTool : IMessageContentAnthropic
    {
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string? type { get; set; } = MessageType.WebSearch;

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get;  set;} = "web_search";

        [JsonProperty("max_uses", NullValueHandling = NullValueHandling.Ignore)]
        public int? max_uses { get; set; } = 5;


        [JsonIgnore]
        public string? text { get; set; }

        [JsonIgnore]
        public CacheControl? CacheControl { get; set; }

    }










    public class WebSearchToolResultBlock : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; set; } = "web_search_tool_result";
  
        [JsonIgnore]
        public string? text { get; set; }
        [JsonIgnore]
        public CacheControl CacheControl { get; set; }

    }















    public class ImageContent : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.Image;

        [JsonIgnore]
        public string? text { get; set; }

        [JsonProperty("source")]
        public Source source { get; set; } = new Source();

        [JsonIgnore]
        public CacheControl? CacheControl { get; set; }
    }

    public class Source
    {
        [JsonProperty("type")]
        public string type { get;} = ImageData.Type.Base64;

        [JsonProperty("media_type")]
        public string media_type { get; set; } = ImageData.MediaType.Jpeg;

        [JsonProperty("data")]
        public string data { get; set; } = string.Empty;
    }








    public class ToolResultEmpty : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.ToolResult;

        [JsonIgnore]
        public string? text { get; set; }

        [JsonProperty("tool_use_id")]
        public string? tool_use_id { get; set; } = string.Empty;

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? CacheControl { get; set; }
    }

    public class ToolResultEmptyWithError : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.ToolResult;

        [JsonIgnore]
        public string? text { get; set; }

        [JsonProperty("tool_use_id")]
        public string? tool_use_id { get; set; } = string.Empty;

        [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)]
        public bool is_error { get; set; } = false;

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? CacheControl { get; set; }
    }

    public class ToolResultTextMessage : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.ToolResult;

        [JsonIgnore]
        public string? text { get; set; }

        [JsonProperty("tool_use_id")]
        public string? tool_use_id { get; set; } = string.Empty;

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public string content { get; set; } = string.Empty;

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? CacheControl { get; set; }
    }

    public class ToolResultContentList : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; set; } = MessageType.ToolResult;

        [JsonIgnore]
        public string? text { get; set; }

        [JsonProperty("tool_use_id")]
        public string? tool_use_id { get; set; } = string.Empty;

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public List<IMessageContentAnthropic>? content { get; set; }

        [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)]
        public bool? is_error { get; set; }

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? CacheControl { get; set; }
    }


    public class ToolResultContent : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.ToolResult;

        [JsonIgnore]
        public string? text { get; set; }

        [JsonProperty("tool_use_id")]
        public string? tool_use_id { get; set; } = string.Empty;

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public IMessageContentAnthropic? content { get; set; }

        [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)]
        public bool? is_error { get; set; }

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? CacheControl { get; set; }
    }




    public class ToolResultMessage : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.ToolResult;

        [JsonIgnore]
        public string? text { get; set; }

        [JsonProperty("tool_use_id")]
        public string? tool_use_id { get; set; } = string.Empty;

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public string content { get; set; } = string.Empty;

        [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)]
        public bool is_error { get; set; } = false;

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? CacheControl { get; set; }
    }
 





    public class ToolUseContent : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; set;} = MessageType.ToolUse;

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string? text { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? id { get; set; }
 
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }

        [JsonProperty("input", NullValueHandling = NullValueHandling.Ignore)]
        public ToolInput? input { get; set; }

        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl? CacheControl { get; set; }

        public ToolUseContent()
        {
            this.type = MessageType.ToolUse; 
        } // Default
    }


     
    //public class MessageLogEntry : IMessageContentAnthropic
    //{
    //    [JsonProperty("type")]
    //    public string type { get; } = MessageType.ToolUse;

    //    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    //    public string? text { get; set; }

    //    [JsonProperty("editor_id", NullValueHandling = NullValueHandling.Ignore)]
    //    public string? editor_id { get; set; }

    //    [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
    //    public string role { get; set; }

    //    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    //    public List<IMessageContentAnthropic> content { get; set; }

    //    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    //    public CacheControl? CacheControl { get; set; }

    //    public MessageLogEntry()
    //    {
    //        content = new List<IMessageContentAnthropic>();
    //    }
    //}

    public class SystemMessage : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.Text;

        [JsonProperty("text")]
        public string? text { get; set; }

        [JsonProperty("cache_control")]
        public CacheControl? CacheControl { get; set; } = null;


        public SystemMessage()
        {
 
        }

        public SystemMessage(string text, CacheControl? cacheControl = null)
        {
            type = "text";
            this.text = text;
            CacheControl = cacheControl;
        }


        public static MessageAnthropic CreateSystemTextMessage(string text)
        {
            CacheControl? cacheControl = null;

            var message = new MessageAnthropic(Roles.System);
            message.content.Add(new MessageContent
            {
                text = text,
                CacheControl = cacheControl
            });

            return message;
        }


    }

    public class Thinking
    {
        [JsonProperty("type")]
        public string type { get; set; } = "enabled";

        [JsonProperty("budget_tokens")]
        public int? BudgetTokens { get; set; }
    }
 

 

    // Models for thinking content in responses
    public class ThinkingContent : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.Thinking;

        [JsonProperty("thinking")]
        public string? ThinkingText { get; set; }

        [JsonProperty("signature")]
        public string? Signature { get; set; }

        [JsonIgnore]
        public string? text { get; set; }

        [JsonIgnore]
        public CacheControl? CacheControl { get; set; }
    }

    public class RedactedThinkingContent : IMessageContentAnthropic
    {
        [JsonProperty("type")]
        public string type { get; } = MessageType.RedactedThinking;

        [JsonProperty("data")]
        public string? Data { get; set; }

        [JsonIgnore]
        public string? text { get; set; }

        [JsonIgnore]
        public CacheControl? CacheControl { get; set; }
    }

    public class FileContent : IMessageContentAnthropic
    {
        // Make the "type" property read-only after instantiation.
        [JsonProperty("type")]
        public string type { get; }

        [JsonProperty("source")]
        public FileSource source { get; set; }

        [JsonIgnore]
        public string? text { get; set; }

        [JsonIgnore]
        public CacheControl? CacheControl { get; set; }

        public FileContent(string fileId, string fileType = "document")
        {
            this.type = fileType; // The type is set here and cannot be changed later
            this.source = new FileSource { file_id = fileId };
        }
    }

    public class FileSource
    {
        [JsonProperty("type")]
        public string type { get; } = "file";

        [JsonProperty("file_id")]
        public string file_id { get; set; }
    }

}