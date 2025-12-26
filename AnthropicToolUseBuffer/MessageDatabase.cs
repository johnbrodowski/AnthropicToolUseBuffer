using Newtonsoft.Json; 
using System.Data;
using Microsoft.Data.Sqlite;
using System.Diagnostics; 

namespace AnthropicToolUseBuffer
{
    public class MessageDatabase
    {
        public event EventHandler<MemoryDatabaseEventArgs>? ErrorOccurred;

        public event EventHandler<MemoryDatabaseEventArgs>? RequestCompleted;

        public event EventHandler<MemoryDatabaseEventArgs>? MessageRequestCompleted;
         
        private string _connectionString;

        public MessageDatabase(string dbName)
        {

            _connectionString = $"Data Source={dbName}.db;Pooling=True;";
             
            InitializeDatabase();
        }
         
        private async Task<SqliteConnection> GetConnectionAsync()
        {
            var connection = new SqliteConnection(_connectionString);
            //await connection.OpenAsync();
            return connection;
        }
         
        private async void InitializeDatabase()
        {
            //using var connection = new SqliteConnection(_connectionString);
            using var connection = await GetConnectionAsync();

            connection.Open();

            // Create Messages table
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Messages (
                        MessageId TEXT PRIMARY KEY,
                        Role TEXT NOT NULL,
                        Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                        CacheControl TEXT
                    )";
                command.ExecuteNonQuery();
            }

            // Create MessageContent table
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS MessageContent (
                        ContentId TEXT PRIMARY KEY,
                        MessageId TEXT NOT NULL,
                        ContentOrder INT NOT NULL,
                        ContentType TEXT NOT NULL,
                        Text TEXT,
                        ToolUseId TEXT,
                        IsError INTEGER,
                        ImageSource TEXT,
                        ImageMediaType TEXT,
                        ImageData TEXT,
                        ToolId TEXT,
                        ToolName TEXT,
                        ToolInput TEXT,
                        CacheControl TEXT,
                        FOREIGN KEY(MessageId) REFERENCES Messages(MessageId)
                    )";
                command.ExecuteNonQuery();
            }
        }
         
 
        public async Task<bool> SaveMessage(MessageAnthropic message)
        {
            using var connection = await GetConnectionAsync();
            connection.Open();
            using var transaction = connection.BeginTransaction();



            try
            {
                var messageId = Guid.NewGuid().ToString();

                // Save message
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
                        INSERT INTO Messages (MessageId, Role)
                        VALUES (@MessageId, @Role)";
                    command.Parameters.AddWithValue("@MessageId", messageId);
                    command.Parameters.AddWithValue("@Role", message.role);
                    await command.ExecuteNonQueryAsync();
                }

                // Save each content item
                for (int i = 0; i < message.content.Count; i++)
                {
                    SaveMessageContent(connection, transaction, messageId, message.content[i], i);
                }
                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                //throw;
                return false;
            }
        }
 
        private void SaveMessageContent(SqliteConnection connection, SqliteTransaction transaction, string messageId, IMessageContentAnthropic content, int order)
        {
            if (content is ToolResultContentList toolResult)
            {
                // Save the main tool result entry
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
                INSERT INTO MessageContent (
                    ContentId, MessageId, ContentOrder, ContentType, Text,
                    ToolUseId, IsError, ImageSource, ImageMediaType, ImageData,
                    ToolId, ToolName, ToolInput, CacheControl
                ) VALUES (
                    @ContentId, @MessageId, @ContentOrder, @ContentType, @Text,
                    @ToolUseId, @IsError, @ImageSource, @ImageMediaType, @ImageData,
                    @ToolId, @ToolName, @ToolInput, @CacheControl
                )";

                    var contentId = Guid.NewGuid().ToString();
                    command.Parameters.AddWithValue("@ContentId", contentId);
                    command.Parameters.AddWithValue("@MessageId", messageId);
                    command.Parameters.AddWithValue("@ContentOrder", order);
                    command.Parameters.AddWithValue("@ContentType", content.type);
                    command.Parameters.AddWithValue("@Text", DBNull.Value);
                    command.Parameters.AddWithValue("@ToolUseId", toolResult.tool_use_id);
                    command.Parameters.AddWithValue("@IsError", toolResult.is_error ?? false);
                    command.Parameters.AddWithValue("@ImageSource", DBNull.Value);
                    command.Parameters.AddWithValue("@ImageMediaType", DBNull.Value);
                    command.Parameters.AddWithValue("@ImageData", DBNull.Value);
                    command.Parameters.AddWithValue("@ToolId", DBNull.Value);
                    command.Parameters.AddWithValue("@ToolName", DBNull.Value);
                    command.Parameters.AddWithValue("@ToolInput", DBNull.Value);
                    command.Parameters.AddWithValue("@CacheControl",
                        content.CacheControl != null ? JsonConvert.SerializeObject(content.CacheControl) : DBNull.Value);

                    command.ExecuteNonQuery();
                }

                // Save each nested content item
                if (toolResult.content != null)
                {
                    for (int i = 0; i < toolResult.content.Count; i++)
                    {
                        SaveMessageContent(connection, transaction, messageId, toolResult.content[i], order + i + 1);
                    }
                }
            }
            else
            {
                // Original code for other content types
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
            INSERT INTO MessageContent (
                ContentId, MessageId, ContentOrder, ContentType, Text,
                ToolUseId, IsError, ImageSource, ImageMediaType, ImageData,
                ToolId, ToolName, ToolInput, CacheControl
            ) VALUES (
                @ContentId, @MessageId, @ContentOrder, @ContentType, @Text,
                @ToolUseId, @IsError, @ImageSource, @ImageMediaType, @ImageData,
                @ToolId, @ToolName, @ToolInput, @CacheControl
            )";

                var contentId = Guid.NewGuid().ToString();
                command.Parameters.AddWithValue("@ContentId", contentId);
                command.Parameters.AddWithValue("@MessageId", messageId);
                command.Parameters.AddWithValue("@ContentOrder", order);
                command.Parameters.AddWithValue("@ContentType", content.type);
                command.Parameters.AddWithValue("@Text", (object?)content.text ?? DBNull.Value);

                // Handle different content types
                switch (content)
                {
                    case ImageContent imageContent:
                        command.Parameters.AddWithValue("@ImageSource", imageContent.source.type);
                        command.Parameters.AddWithValue("@ImageMediaType", imageContent.source.media_type);
                        command.Parameters.AddWithValue("@ImageData", imageContent.source.data);
                        break;

                    case ToolUseContent toolUse:
                        command.Parameters.AddWithValue("@ToolId", (object?)toolUse.id ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ToolName", (object?)toolUse.name ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ToolInput", toolUse.input != null ?
                            JsonConvert.SerializeObject(toolUse.input) : DBNull.Value);
                        break;

                    default:
                        command.Parameters.AddWithValue("@ToolUseId", DBNull.Value);
                        command.Parameters.AddWithValue("@IsError", DBNull.Value);
                        break;
                }

                // Set remaining parameters to DBNull.Value if not already set
                SetParameterIfNotExists(command, "@ImageSource", DBNull.Value);
                SetParameterIfNotExists(command, "@ImageMediaType", DBNull.Value);
                SetParameterIfNotExists(command, "@ImageData", DBNull.Value);
                SetParameterIfNotExists(command, "@ToolId", DBNull.Value);
                SetParameterIfNotExists(command, "@ToolName", DBNull.Value);
                SetParameterIfNotExists(command, "@ToolInput", DBNull.Value);
                SetParameterIfNotExists(command, "@ToolUseId", DBNull.Value);
                SetParameterIfNotExists(command, "@IsError", DBNull.Value);
                SetParameterIfNotExists(command, "@CacheControl",
                    content.CacheControl != null ? JsonConvert.SerializeObject(content.CacheControl) : DBNull.Value);

                command.ExecuteNonQuery();
            }
        }
         
        private void SetParameterIfNotExists(SqliteCommand command, string parameterName, object value)
        {
            if (!command.Parameters.Contains(parameterName))
            {
                command.Parameters.AddWithValue(parameterName, value);
            }
        }
         
        public async Task<List<MessageAnthropic>> LoadMessages(int trunkMax, int maxMsgCount, bool includeTools)
        {
            var messageDict = new Dictionary<string, MessageAnthropic>();
            var messageOrder = new List<string>();
 
            string previousMessageType = string.Empty;
            MessageAnthropic? previousMessage = null;
            MessageAnthropic? currentMessage = null;

            using var connection = await GetConnectionAsync();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
WITH RankedMessages AS (
    SELECT m.MessageId, m.Role, m.Timestamp, mc.ContentOrder, mc.ContentType, mc.Text, mc.ToolUseId,
           mc.IsError, mc.ImageSource, mc.ImageMediaType, mc.ImageData,
           mc.ToolId, mc.ToolName, mc.ToolInput, mc.CacheControl,
           ROW_NUMBER() OVER (ORDER BY m.Timestamp DESC) as RowNum
    FROM Messages m
    LEFT JOIN MessageContent mc ON m.MessageId = mc.MessageId
)
SELECT * FROM RankedMessages
WHERE RowNum <= @MaxMessages
ORDER BY Timestamp ASC, ContentOrder ASC";

            command.Parameters.AddWithValue("@MaxMessages", maxMsgCount);

            using var reader = (SqliteDataReader)await command.ExecuteReaderAsync();
            Debug.WriteLine($"[DEBUG Message History:");
            while (await reader.ReadAsync())
            {
                // Check for essential columns.
                // We require MessageId (column 0), Role (column 1) and ContentType (column 4).
                if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(4))
                {
                    continue;
                }

                string messageId = reader.GetString(0);
                string role = reader.GetString(1);
                string contentType = reader.GetString(4);

                // If any of the essential strings is blank, skip this record.
                if (string.IsNullOrWhiteSpace(messageId) ||
                    string.IsNullOrWhiteSpace(role) ||
                    string.IsNullOrWhiteSpace(contentType))
                {
                    continue;
                }
                 

                // If this is the first time seeing this messageId, create a new MessageAnthropic.
                if (!messageDict.ContainsKey(messageId))
                {
                    messageDict[messageId] = new MessageAnthropic(role);
                    messageOrder.Add(messageId);
                }
                  
                currentMessage = messageDict[messageId];
                 
                bool hasTools = currentMessage.content.Any(c => c is ToolUseContent || c is ToolResultContentList || c is ToolResultMessage );
 

                if (contentType == MessageType.ToolResult && includeTools && messageDict.Count > 1)
                {
                    // Skip tool_result if it would be the first content in the message
                    if (currentMessage.content.Count == 0)
                    {
                        Debug.WriteLine($"[DEBUG] Skipping ToolResult as first content - MessageId: {messageId}");
                        continue;
                    }

                    // For a tool result, ensure we have a valid ToolUseId.
                    if (reader.IsDBNull(6))
                        continue;

                    string toolUseId = reader.GetString(6);
                    bool isError = !reader.IsDBNull(7) && reader.GetBoolean(7);

                    // See if we already have a ToolResult for this tool use.
                    var existingToolResult = currentMessage.content
                        .FirstOrDefault(x => x is ToolResultContentList trc && trc.tool_use_id == toolUseId)
                        as ToolResultContentList;

                    if (existingToolResult == null)
                    {
                        var newToolResult = new ToolResultContentList
                        {
                            tool_use_id = toolUseId,
                            is_error = isError,
                            content = new List<IMessageContentAnthropic>()
                        };
                        currentMessage.content.Add(newToolResult);

                        // Attempt to create nested content.
                        var nestedContent = CreateMessageContent(reader);

                        if (nestedContent != null && nestedContent.type != MessageType.ToolResult)
                        {
                            newToolResult.content.Add(nestedContent);
                        }
                    }
                    else
                    {
                        var nestedContent = CreateMessageContent(reader);
                        if (nestedContent != null && nestedContent.type != MessageType.ToolResult)
                        {
                            existingToolResult.content?.Add(nestedContent);
                        }
                    }
                }
                else
                {
                    // Skip all tool-related content when includeTools is false
                    if (!includeTools && (contentType == MessageType.ToolResult || contentType == MessageType.ToolUse))
                    {
                        Debug.WriteLine($"[DEBUG] Skipping tool content (includeTools=false) - Type: {contentType}, MessageId: {messageId}");
                        continue;
                    }

                    // Handle non-tool result content
                    var content = CreateMessageContent(reader);

                    if (!includeTools && hasTools) 
                        continue;

 
                    if (content != null)
                    {
                        // Add content to the current message
                        var lastContent = currentMessage.content.LastOrDefault(); // Last one added is the last one because we haven't added this one yet.

                        if (lastContent is ToolResultContentList toolResult && includeTools && hasTools)
                        {
                            if (content.type != MessageType.ToolResult)
                            {
                                content.text = GetTruncatedText(content.text, trunkMax, $"(Truncated Text) - MessageId: {messageId}");
                                toolResult.content?.Add(content);
                            }
                        }
                        else
                        {
                            // Skip tool_use if it would be the first content in the message
                            if (contentType == MessageType.ToolUse && currentMessage.content.Count == 0)
                            {
                                Debug.WriteLine($"[DEBUG] Skipping ToolUse as first content - MessageId: {messageId}");
                                continue;
                            }
                             
                            if (string.IsNullOrEmpty(content.text)) continue;
                             
                            if(contentType == MessageType.Text)
                            {
 
                                var shouldSkip = false;
 
                                var excludes = new List<string> { "ping_ttl_ack", "No content returned for index" };

                                foreach (var ex in excludes)
                                {
                                    if (!hasTools && content.text.Contains(ex))
                                    {
                                        shouldSkip = true;
                                        break;
                                    }
                                }

                                if (shouldSkip) continue;

                            }

                            content.text = GetTruncatedText(content.text, trunkMax, $"(Truncated Text) - MessageId: {messageId}");              
                             
                            if (content != null) currentMessage.content.Add(content);
                        }
                    }
                }
            }

            // Limit the returned messages to maxMsgCount while preserving the order.
            return messageOrder.Take(maxMsgCount).Select(id => messageDict[id]).ToList();
        }
  
        private string GetTruncatedText(string? text, int maxLength, string truncationMessage = "")
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return $"{text.Substring(0, maxLength)}{truncationMessage}"; 
        }
 
        private IMessageContentAnthropic? CreateMessageContent(SqliteDataReader reader)
        {
            var contentType = reader.GetString(4); // ContentType
            var text = reader.IsDBNull(5) ? null : reader.GetString(5);

            switch (contentType)
            {
                case MessageType.Text:
                    return new MessageContent { text = text };

                case MessageType.Image:
                    if (!reader.IsDBNull(8)) // ImageSource exists
                    {
                        return new ImageContent
                        {
                            source = new Source
                            {
                                media_type = reader.GetString(9),
                                data = reader.GetString(10)
                            }
                        };
                    }
                    break;

                case MessageType.ToolUse:
                    var toolUseContent = new ToolUseContent { text = text };
                    if (!reader.IsDBNull(11)) // id exists
                    {
                        toolUseContent.id = reader.GetString(11);
                        toolUseContent.name = reader.IsDBNull(12) ? null : reader.GetString(12);
                        if (!reader.IsDBNull(13))
                        {
                            toolUseContent.input = JsonConvert.DeserializeObject<ToolInput>(reader.GetString(13));
                        }
                    }
                    return toolUseContent;

                case MessageType.ToolResult:
                    if (!reader.IsDBNull(6)) // ToolUseId exists
                    {
                        var isError = !reader.IsDBNull(7) && reader.GetBoolean(7);
                        return new ToolResultMessage
                        {
                            tool_use_id = reader.GetString(6),
                            content = text ?? string.Empty,
                            is_error = isError
                        };
                    }
                    break;
            }

            return null;
        }
 
        public class MemoryDatabaseEventArgs : EventArgs
        {
            public string? Message { get; set; }
            public MessageAnthropic? MessageObject { get; set; }
            public string? EventType { get; set; }
            public object? Result { get; set; }
        }
    }
}