using AnthropicToolUseBuffer.ToolClasses;

using Microsoft.VisualBasic.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public class MessageHistoryLogger
    {
        private readonly string _logFilePath;
        private static readonly object _lock = new object();
        private readonly JsonSerializerSettings _serializerSettings;

        private Dictionary<string, MessageState> _messageStates = new Dictionary<string, MessageState>();

        private class MessageState
        {
            public bool WaitingForToolResult { get; set; }
            public string LastToolUseId { get; set; }
            public MessageAnthropic CurrentMessage { get; set; }
        }



        public MessageHistoryLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
            _serializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
                Converters = new List<JsonConverter> { new MessageContentConverter() }
            };
            EnsureLogFileExists();
        }

        private void EnsureLogFileExists()
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            if (!File.Exists(_logFilePath))
            {
                File.WriteAllText(_logFilePath, "[]");
            }
        }

        public class MessageContentConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(IMessageContentAnthropic).IsAssignableFrom(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject jo = JObject.Load(reader);
                string type = jo["type"]?.ToString();  // Changed from "type" to "type"

                IMessageContentAnthropic content;
                switch (type)
                {
                    case "text":
                        content = new MessageContent
                        {
                            text = jo["text"]?.ToString()  // Changed from "text" to "text"
                        };
                        break;

                    case "tool_use":
                        content = new ToolUseContent();
                        break;

                    case "tool_result":
                        content = new ToolResultContentList();
                        break;

                    case "image":
                        content = new ImageContent();
                        break;

                    default:
                        throw new JsonSerializationException($"Unknown message content type: {type}");
                }

                serializer.Populate(jo.CreateReader(), content);
                return content;
            }


            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                JObject jo = new JObject();
                Type type = value.GetType();

                foreach (var prop in type.GetProperties())
                {
                    var val = prop.GetValue(value);
                    if (val != null)
                    {
                        jo.Add(prop.Name, JToken.FromObject(val, serializer));
                    }
                }

                jo.WriteTo(writer);
            }

            public override bool CanWrite => true;
        }






        // Add a new method to append content to existing message or create new one

        public async Task LogOutgoingMessage(string text)
        {
            var entry = new MessageAnthropic("user")
            {
                content = new List<IMessageContentAnthropic>
            {
                new MessageContent
                {
                    text = text
                }
            }
            };
            await SaveLogEntry(entry);
        }




        public async Task LogOutgoingToolResult(string toolId, List<IMessageContentAnthropic> contents, bool? isError = false)
        {
            var entry = new MessageAnthropic("user")
            {
                content = new List<IMessageContentAnthropic>
            {
                new ToolResultContentList
                {
                    tool_use_id = toolId,
                    content = contents,
                    is_error = isError
                }
            }
            };
            await SaveLogEntry(entry);
        }


        public async Task LogToolResultImageMessage(string toolUseId, string text, string imageData, bool isError)
        {
            var toolContent = new List<IMessageContentAnthropic>();

            var entry = new MessageAnthropic("user")
            {
                content = new List<IMessageContentAnthropic>()
            };

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

            entry.content.Add(new ToolResultContentList
            {
                tool_use_id = toolUseId,
                content = toolContent,
                is_error = isError
            });

            await SaveLogEntry(entry);
        }

        public async Task LogOutgoingToolResult(string toolId, string text, bool isError = false)
        {
            var entry = new MessageAnthropic("user")
            {
                content = new List<IMessageContentAnthropic>()
            };

            entry.content.Add(new ToolResultMessage
            {
                tool_use_id = toolId,
                content = text,
                is_error = isError
            });

            await SaveLogEntry(entry);
        }

        public async Task LogIncomingAssistantMessage(AnthropicResponse aiResponse)
        {
            var entry = new MessageAnthropic(aiResponse.role)
            {
                content = new List<IMessageContentAnthropic>()
            };

            foreach (var item in aiResponse.content)
            {
                if (item.type == "text")
                {
                    entry.content.Add(new MessageContent
                    {
                        text = item.text
                    });
                }
                else if (item.type == "tool_use")
                {
                    entry.content.Add(new ToolUseContent
                    {
                        id = item.id,
                        name = item.name,
                        input = item.input
                    });
                }
            }

 
            await SaveLogEntry(entry);
        }

        public async Task LogIncomingMessage(string requestId, string? textContent, string? jsonContent)
        {
            var entry = new MessageAnthropic("assistant")
            {
                content = new List<IMessageContentAnthropic>()
            };

            // Add text content
            if (!string.IsNullOrEmpty(textContent))
            {
                entry.content.Add(new MessageContent
                {
                    text = textContent
                });
            }

            // Add tool use content if present
            if (!string.IsNullOrEmpty(jsonContent))
            {
                var tmp = JsonConvert.DeserializeObject<ToolUseContent>(jsonContent);
                entry.content.Add(new ToolUseContent
                {
                    id = tmp.id,
                    name = tmp.name,
                    input = JsonConvert.DeserializeObject<ToolInput>(jsonContent)
                });
            }

            // Save the message
            List<MessageAnthropic> existingLogs;
            lock (_lock)
            {
                string existingContent = File.ReadAllText(_logFilePath);
                existingLogs = JsonConvert.DeserializeObject<List<MessageAnthropic>>(existingContent, _serializerSettings)
                    ?? new List<MessageAnthropic>();

                existingLogs.Add(entry);

                string updatedJson = JsonConvert.SerializeObject(existingLogs, _serializerSettings);
                File.WriteAllText(_logFilePath, updatedJson);
            }
        }


        public async Task LogIncomingToolUse(string requestId, string content)
        {
            var entry = new MessageAnthropic("assistant")
            {
                content = new List<IMessageContentAnthropic>
        {
            new ToolUseContent
            {
                input = JsonConvert.DeserializeObject<ToolInput>(content)
            }
        }
            };
            await SaveLogEntry(entry);
        }


        private async Task SaveLogEntry(MessageAnthropic entry)
        {
            try
            {
                List<MessageAnthropic> existingLogs;
                lock (_lock)
                {
                    string jsonContent = File.ReadAllText(_logFilePath);
                    existingLogs = JsonConvert.DeserializeObject<List<MessageAnthropic>>(jsonContent, _serializerSettings)
                        ?? new List<MessageAnthropic>();

                    existingLogs.Add(entry);

                    string updatedJson = JsonConvert.SerializeObject(existingLogs, _serializerSettings);
                    File.WriteAllText(_logFilePath, updatedJson);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving message log: {ex.Message}");
                throw;
            }
        }


        public async Task SaveLogEntry(List<MessageAnthropic> entries)
        {
            try
            {
                List<MessageAnthropic> existingLogs;

                lock (_lock)
                {
                    //string jsonContent = File.ReadAllText(_logFilePath);
                    //existingLogs = JsonConvert.DeserializeObject<List<IMessageContentAnthropic>>(jsonContent, _serializerSettings)
                    //    ?? new List<IMessageContentAnthropic>();

                    //existingLogs.Add(entry);

                    string updatedJson = JsonConvert.SerializeObject(entries, _serializerSettings);
                    File.WriteAllText(_logFilePath, updatedJson);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving message log: {ex.Message}");
                throw;
            }
        }


        public List<MessageAnthropic> LoadMessageHistory(int MaxEntries = 10)
        {
            try
            {
                lock (_lock)
                {
                    string jsonContent = File.ReadAllText(_logFilePath);
                    var logs = JsonConvert.DeserializeObject<List<MessageAnthropic>>(jsonContent, _serializerSettings)
                        ?? new List<MessageAnthropic>();

                    return logs
                        .TakeLast(MaxEntries)
                        .Where(entry => !entry.content.Any(c =>
                            c.type == "tool_use" || c.type == "tool_result"))
                        .Select(entry => new MessageAnthropic(entry.role)
                        {
                            content = entry.content
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading message history: {ex.Message}");
                return new List<MessageAnthropic>();
            }
        }

        public List<MessageAnthropic> LoadMessageHistory2(int MaxEntries = 10) 
        {
            try
            {
                lock (_lock)
                {
                    string jsonContent = File.ReadAllText(_logFilePath);
                    var logs = JsonConvert.DeserializeObject<List<MessageAnthropic>>(jsonContent, _serializerSettings)
                        ?? new List<MessageAnthropic>();

                    return logs
                        .TakeLast(MaxEntries)
                        .Where(entry => !entry.content.Any(c =>
                            c.type == "tool_use" || c.type == "tool_result"))
                        .Select(entry =>
                        {
                            var newMessage = new MessageAnthropic(entry.role);

                            foreach (var c in entry.content)
                            {
                                if (entry.role == "user" && c.type == "text" && c.text != null)
                                {
                                    // Find the position of "## The_User_Request ##"
                                    int requestMarkerPos = c.text.IndexOf("## The_User_Request ##");

                                    if (requestMarkerPos >= 0)
                                    {
                                        // Find the position after the marker and newlines
                                        int startPos = c.text.IndexOf("\r\n\r\n", requestMarkerPos);
                                        if (startPos >= 0)
                                        {
                                            // Create a new content with only the text after the marker
                                            var cleanedText = c.text.Substring(startPos + 4); // +4 to skip the "\r\n\r\n"
                                            var newContent = new MessageContent
                                            {
                                                text = cleanedText,
                                                CacheControl = c.CacheControl
                                            };
                                            newMessage.content.Add(newContent);
                                            continue;
                                        }
                                    }
                                }

                                // For other content types or when no marker is found, add the original content
                                // Create a deep clone based on the actual type
                                if (c is MessageContent textContent)
                                {
                                    newMessage.content.Add(new MessageContent
                                    {
                                        text = textContent.text,
                                        CacheControl = textContent.CacheControl
                                    });
                                }
                                else if (c is ImageContent imgContent)
                                {
                                    var newImgContent = new ImageContent
                                    {
                                        text = imgContent.text,
                                        CacheControl = imgContent.CacheControl
                                    };

                                    // Clone the source object
                                    newImgContent.source = new Source
                                    {
                                        data = imgContent.source.data,
                                        media_type = imgContent.source.media_type
                                    };

                                    newMessage.content.Add(newImgContent);
                                }
                                else
                                {
                                    // For other content types, just add the original reference
                                    // since we're not modifying them
                                    newMessage.content.Add(c);
                                }
                            }

                            return newMessage;
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading message history: {ex.Message}");
                return new List<MessageAnthropic>();
            }
        }

        public List<MessageAnthropic> LoadMessageHistory3(int MaxEntries = 10)
        {
            try
            {
                lock (_lock)
                {
                    var result = new List<MessageAnthropic>();

                    using (var fileStream = File.OpenText(_logFilePath))
                    using (var jsonReader = new JsonTextReader(fileStream))
                    {
                        var serializer = JsonSerializer.Create(_serializerSettings);

                        // Read the opening array token
                        jsonReader.Read();

                        // Read messages from the end
                        var allMessages = new List<MessageAnthropic>();

                        while (jsonReader.Read())
                        {
                            if (jsonReader.TokenType == JsonToken.StartObject)
                            {
                                var message = serializer.Deserialize<MessageAnthropic>(jsonReader);
                                if (message != null)
                                {
                                    allMessages.Add(message);
                                }
                            }
                        }

                        // Take only the last N messages
                        return allMessages
                            .Skip(Math.Max(0, allMessages.Count - MaxEntries))
                            .Where(entry => !entry.content.Any(c =>
                                c.type == "tool_use" || c.type == "tool_result"))
                            .Select(entry => new MessageAnthropic(entry.role)
                            {
                                content = entry.content
                            })
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading message history: {ex.Message}");
                return new List<MessageAnthropic>();
            }
        }


        public List<MessageAnthropic> LoadMessageHistory4(int MaxEntries = 100, int maxUserTextLength = 200, int maxAssistantTextLength = 200, string TruncatedMessage = "")
        {
            try
            {
                lock (_lock)
                {
                    string jsonContent = File.ReadAllText(_logFilePath);
                    var logs = JsonConvert.DeserializeObject<List<MessageAnthropic>>(jsonContent, _serializerSettings)
                        ?? new List<MessageAnthropic>();

                    return logs
                        .TakeLast(MaxEntries)
                        .Where(entry => !entry.content.Any(c =>
                            c.type == "tool_use" || c.type == "tool_result"))
                        .Select(entry =>
                        {
                            var newMessage = new MessageAnthropic(entry.role);

                            foreach (var c in entry.content)
                            {
                                if (c.type == "text" && c.text != null)
                                {
                                    string processedText = c.text;
                                    bool isTruncated = false;

                                    // Handle user message special formatting
                                    if (entry.role == "user")
                                    {
                                        // Find the position of "## The_User_Request ##"
                                        int requestMarkerPos = processedText.IndexOf("## The_User_Request ##");

                                        if (requestMarkerPos >= 0)
                                        {
                                            // Find the position after the marker and newlines
                                            int startPos = processedText.IndexOf("\r\n\r\n", requestMarkerPos);
                                            if (startPos >= 0)
                                            {
                                                // Keep only the text after the marker
                                                processedText = processedText.Substring(startPos + 4); // +4 to skip the "\r\n\r\n"
                                            }
                                        }

                                        // Apply user text length limit if specified
                                        if (maxUserTextLength > 0 && processedText.Length > maxUserTextLength)
                                        {
                                            processedText = processedText.Substring(0, maxUserTextLength);
                                            isTruncated = true;
                                        }
                                    }
                                    // Handle assistant message truncation
                                    else if (entry.role == "assistant" && maxAssistantTextLength > 0 && processedText.Length > maxAssistantTextLength)
                                    {
                                        processedText = processedText.Substring(0, maxAssistantTextLength);
                                        isTruncated = true;
                                    }

                                    // Add truncation message if needed
                                    if (isTruncated)
                                    {
                                        processedText += "\n\nTruncated for brevity";
                                    }

                                    // Create a new content with the processed text
                                    var newContent = new MessageContent
                                    {
                                        text = processedText,
                                        CacheControl = c.CacheControl
                                    };
                                    newMessage.content.Add(newContent);
                                }
                                else if (c is ImageContent imgContent)
                                {
                                    var newImgContent = new ImageContent
                                    {
                                        text = imgContent.text,
                                        CacheControl = imgContent.CacheControl
                                    };

                                    // Clone the source object
                                    newImgContent.source = new Source
                                    {
                                        data = imgContent.source.data,
                                        media_type = imgContent.source.media_type
                                    };

                                    newMessage.content.Add(newImgContent);
                                }
                                else
                                {
                                    // For other content types, just add the original reference
                                    newMessage.content.Add(c);
                                }
                            }

                            return newMessage;
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading message history: {ex.Message}");
                return new List<MessageAnthropic>();
            }
        }

        public List<MessageAnthropic> LoadMessageHistory5(int MaxEntries = 10, int maxUserTextLength = 0, int maxAssistantTextLength = 0, string TruncatedMessage = "",string skipWord = "placeholder")
        {
            try
            {
                lock (_lock)
                {
                    string jsonContent = File.ReadAllText(_logFilePath);

                    var logs = JsonConvert.DeserializeObject<List<MessageAnthropic>>(jsonContent, _serializerSettings)
                        ?? new List<MessageAnthropic>();

                    return logs
                        .TakeLast(MaxEntries)
                        .Where(entry =>
                            // Filter out tool-related messages
                            !entry.content.Any(c => c.type == "tool_use" || c.type == "tool_result") &&
                            // Filter out messages containing "placeholder"
                            !entry.content.Any(c => c.type == "text" && c.text != null &&
                                                  c.text.IndexOf(skipWord, StringComparison.OrdinalIgnoreCase) >= 0))
                        .Select(entry =>
                        {
                            var newMessage = new MessageAnthropic(entry.role);

                            foreach (var c in entry.content)
                            {
                                if (c.type == "text" && c.text != null)
                                {
                                    string processedText = c.text;
                                    bool isTruncated = false;

                                    // Handle user message special formatting
                                    if (entry.role == "user")
                                    {
                                        // Find the position of "## The_User_Request ##"
                                        int requestMarkerPos = processedText.IndexOf("## The_User_Request ##");

                                        if (requestMarkerPos >= 0)
                                        {
                                            // Find the position after the marker and newlines
                                            int startPos = processedText.IndexOf("\r\n\r\n", requestMarkerPos);
                                            if (startPos >= 0)
                                            {
                                                // Keep only the text after the marker
                                                processedText = processedText.Substring(startPos + 4); // +4 to skip the "\r\n\r\n"
                                            }
                                        }

                                        // Apply user text length limit if specified
                                        if (maxUserTextLength > 0 && processedText.Length > maxUserTextLength)
                                        {
                                            processedText = processedText.Substring(0, maxUserTextLength);
                                            isTruncated = true;
                                        }
                                    }
                                    // Handle assistant message truncation
                                    else if (entry.role == "assistant" && maxAssistantTextLength > 0 && processedText.Length > maxAssistantTextLength)
                                    {
                                        processedText = processedText.Substring(0, maxAssistantTextLength);
                                        isTruncated = true;
                                    }

                                    // Add truncation message if needed
                                    if (isTruncated)
                                    { 
                                        processedText += TruncatedMessage;  //  "\n\nTruncated for brevity"
                                    }

                                    // Create a new content with the processed text
                                    var newContent = new MessageContent
                                    {
                                        text = processedText,
                                        CacheControl = c.CacheControl
                                    };
                                    newMessage.content.Add(newContent);
                                }
                                else if (c is ImageContent imgContent)
                                {
                                    var newImgContent = new ImageContent
                                    {
                                        text = imgContent.text,
                                        CacheControl = imgContent.CacheControl
                                    };

                                    // Clone the source object
                                    newImgContent.source = new Source
                                    {
                                        data = imgContent.source.data,
                                        media_type = imgContent.source.media_type
                                    };

                                    newMessage.content.Add(newImgContent);
                                }
                                else
                                {
                                    // For other content types, just add the original reference
                                    newMessage.content.Add(c);
                                }
                            }

                            return newMessage;
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading message history: {ex.Message}");
                return new List<MessageAnthropic>();
            }
        }

        public List<MessageAnthropic> LoadMessageAndToolHistory(int MaxEntries = 10)
        {
            try
            {
                lock (_lock)
                {
                    string jsonContent = File.ReadAllText(_logFilePath);
                    var logs = JsonConvert.DeserializeObject<List<MessageAnthropic>>(jsonContent, _serializerSettings)
                        ?? new List<MessageAnthropic>();

                    //return logs
                    //    .Take(MaxEntries * 2) // Automatically double the entries
                    //    .Select(entry => new MessageAnthropic(entry.role)
                    //    {
                    //        content = entry.content
                    //    })
                    //    .ToList();



                    // skip to get the message from the end for older version of .NET

                    //int entriesToTake = MaxEntries; // or MaxEntries * 2 if needed
                    //int skipCount = Math.Max(0, logs.Count - entriesToTake);

                    //return logs
                    //    .Skip(skipCount)
                    //    .Select(entry => new MessageAnthropic(entry.role)
                    //    {
                    //        content = entry.content
                    //    })
                    //    .ToList();




                    return logs
                        .TakeLast(MaxEntries) // or MaxEntries * 2 if that's what you need
                        .Select(entry => new MessageAnthropic(entry.role)
                        {
                            content = entry.content

                        })
                        .ToList();





                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading message history: {ex.Message}");
                return new List<MessageAnthropic>();
            }
        }


        /*

                public List<message> LoadMessageHistory()
                {
                    try
                    {
                        lock (_lock)
                        {
                            string jsonContent = File.ReadAllText(_logFilePath);
                            var logs = JsonConvert.DeserializeObject<List<MessageLogEntry>>(jsonContent, _serializerSettings)
                                ?? new List<MessageLogEntry>();

                            return logs.Select(entry => new message(entry.role)
                            {
                                content = entry.content
                            }).ToList();
                        }
                    }
                    catch (exception ex)
                    {
                        Debug.WriteLine($"Error loading message history: {ex.message}");
                        return new List<message>();
                    }
                }

        */

        public async Task ClearMessageHistory()
        {
            try
            {
                lock (_lock)
                {
                    File.WriteAllText(_logFilePath, "[]");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing message history: {ex.Message}");
                throw;
            }
        }
    }
}