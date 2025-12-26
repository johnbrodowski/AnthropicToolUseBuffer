using AnthropicToolUseBuffer.AIClassesAnthropic;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace AnthropicToolUseBuffer
{
    public class ErrorHandlerForm1
    {
        // Logging levels
        public enum ErrorSeverity
        {
            Low,     // Non-critical errors that don't impact core functionality
            Medium,  // Errors that impact current operation but allow continue
            High     // Critical errors that require user attention
        }

        // Log error and notify user based on severity
        public static async Task HandleError(Exception ex, ErrorSeverity severity, string context, FormAnthropicDemo form)
        {
            string errorMessage = $"Error in {context}: {ex.Message}";

            // Log all errors
            await form.ChatMessage(ChatUser.Error, errorMessage);

            // For high severity, ensure user is notified
            if (severity == ErrorSeverity.High)
            {
                await form.ChatMessage(ChatUser.System, "A critical error occurred. Please check the logs.");
                form.isError = true;
            }

            // For medium severity, log but don't interrupt
            if (severity == ErrorSeverity.Medium)
            {
                await form.ChatMessage(ChatUser.Warning, errorMessage);
            }

            // For debugging
            Debug.WriteLine($"{severity} error in {context}: {ex}");
        }
    }



    public class ErrorHandlerToolBufferDemo
    {
        // Logging levels
        public enum ErrorSeverity
        {
            Low,     // Non-critical errors that don't impact core functionality
            Medium,  // Errors that impact current operation but allow continue
            High,     // Critical errors that require user attention
        }

        // Log error and notify user based on severity
        public static async Task HandleError(Exception ex, ErrorSeverity severity, string context, FormAnthropicDemo form)
        {
            string errorMessage = $"Error in {context}: {ex.Message}";

            // Log all errors
            await form.ChatMessage(ChatUser.Error, errorMessage);

            // For high severity, ensure user is notified
            if (severity == ErrorSeverity.High)
            {
                await form.ChatMessage(ChatUser.System, "A critical error occurred. Please check the logs.");
                form.isError = true;
            }

            // For medium severity, log but don't interrupt
            if (severity == ErrorSeverity.Medium)
            {
                await form.ChatMessage(ChatUser.Warning, errorMessage);
            }

            // For debugging
            Debug.WriteLine($"{severity} error in {context}: {ex}");
        }
    }



}
