using AnthropicToolUseBuffer.ToolClasses;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public class MessageManagementClass
    {


        public MessageManagementClass()
        {



        }



        public List<MessageAnthropic> MessageManager(List<Tool>? toolList, List<MessageAnthropic> messageList, List<SystemMessage> systemMessages, AiRequestParametersAnthropic requestParameters)
        {


            // Ensure the last message is from the user
            if (messageList.Count > 0)
            {
                // If last message isn't from user, remove it
                if (messageList[messageList.Count - 1].role != Roles.User)
                {
                    messageList.RemoveAt(messageList.Count - 1);
                }
            }

            // Only add the new message if it has actual content
            if (requestParameters.UserMessage.content.Any(c => !string.IsNullOrWhiteSpace(c.text)))
            {
                messageList.Add(requestParameters.UserMessage);
            }

            // Ensure message alternation
            messageList = EnsureMessageAlternation(messageList);

            // Output message list after ensuring alternation
            //  await OutputMessageList(messageList, "After ensuring alternation");

            // Handle caching if enabled
            if (requestParameters.UseCache)
            {
                if (requestParameters.UseTools && toolList != null)
                {
                    if (requestParameters.CacheTools)
                    {
                        // Apply cache_control to the last tool
                        if (toolList.Count > 0)
                        {
                            toolList[toolList.Count - 1].cache_control = new CacheControl { type = "ephemeral" };
                        }
                    }
                }

                if (requestParameters.CacheSystem)
                {
                    // Apply cache_control to the last system message
                    if (systemMessages.Count > 0)
                    {
                        systemMessages[systemMessages.Count - 1].CacheControl = new CacheControl { type = "ephemeral" };
                    }
                }

                if (requestParameters.CacheMessages)
                {
                    // Identify the last and second-to-last user messages
                    MessageAnthropic? lastUserMessage = null;
                    MessageAnthropic? secondToLastUserMessage = null;
                    int userMessageCount = 0;

                    // Traverse the message list from the end to find the last two user messages
                    for (int i = messageList.Count - 1; i >= 0; i--)
                    {
                        if (messageList[i].role == Roles.User)
                        {
                            if (userMessageCount == 0)
                            {
                                // This is the last user message
                                lastUserMessage = messageList[i];
                            }
                            else if (userMessageCount == 1)
                            {
                                // This is the second-to-last user message
                                secondToLastUserMessage = messageList[i];
                                break; // Once we've found both, we can stop
                            }
                            userMessageCount++;
                        }
                    }

                    // Apply cache_control to the second-to-last user message
                    if (secondToLastUserMessage != null)
                    {
                        secondToLastUserMessage.content[0].CacheControl = new CacheControl { type = "ephemeral" };
                    }

                    // Apply cache_control to the last user message
                    if (lastUserMessage != null)
                    {
                        lastUserMessage.content[0].CacheControl = new CacheControl { type = "ephemeral" };
                    }

                    // Remove cache_control from any other user messages
                    for (int i = 0; i < messageList.Count; i++)
                    {
                        if (messageList[i].role == Roles.User && messageList[i] != lastUserMessage && messageList[i] != secondToLastUserMessage)
                        {
                            messageList[i].content[0].CacheControl = null; // Ensure older user messages have no cache_control
                        }
                    }
                }
            }

            return messageList;
        }


        private List<MessageAnthropic> EnsureMessageAlternation(List<MessageAnthropic> messages)
        {
            List<MessageAnthropic> alternatingMessages = new List<MessageAnthropic>();
            string expectedRole = Roles.User;

            foreach (var message in messages)
            {
                // Add the message if it matches the expected role
                if (message.role == expectedRole)
                {
                    alternatingMessages.Add(message);
                    // Toggle role between 'user' and 'assistant'
                    expectedRole = (expectedRole == Roles.User) ? Roles.Assistant : Roles.User;
                }
            }

            // Ensure the list ends with a user message
            if (alternatingMessages.Count > 0 && alternatingMessages[^1].role != Roles.User)
            {
                alternatingMessages.RemoveAt(alternatingMessages.Count - 1);
            }

            return alternatingMessages;
        }




    }
}
