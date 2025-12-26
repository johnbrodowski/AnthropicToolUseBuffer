using Newtonsoft.Json;

namespace AnthropicToolUseBuffer.ToolClasses
{
    public class ToolPermissionManager
    {
        public readonly Dictionary<string, ToolPermissions> _toolPermissions = new();
        public string? _currentToolChainInitiator = null;

        public class ToolPermissions
        {
            public bool CanInitiateToolChain { get; set; }
            public HashSet<string> AllowedTools { get; set; } = new();
        }

        public void RegisterTool(string toolName, bool canInitiateToolChain, params string[] allowedTools)
        {
            var permissions = new ToolPermissions
            {
                CanInitiateToolChain = canInitiateToolChain,
                AllowedTools = new HashSet<string>(allowedTools)
            };

            _toolPermissions[toolName] = permissions;
        }

        public void StartToolChain(string? toolName = null)
        {
            _currentToolChainInitiator = toolName;
        }

        public bool IsToolUseAllowed(string? toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return false;

            // Add debug logging
            System.Diagnostics.Debug.WriteLine($"Checking permission for {toolName}");
            System.Diagnostics.Debug.WriteLine($"Current initiator: {_currentToolChainInitiator ?? "null"}");

            // First check if we know about this tool at all
            if (!_toolPermissions.ContainsKey(toolName))
            {
                System.Diagnostics.Debug.WriteLine($"Tool {toolName} not found in permissions");
                return false;
            }

            // If no tool chain is active (direct user request)
            if (_currentToolChainInitiator == null)
            {
                var allowed = _toolPermissions[toolName].CanInitiateToolChain;
                System.Diagnostics.Debug.WriteLine($"Direct user request - canInitiate: {allowed}");
                return allowed;  // This should now properly block tools that can't be initiated
            }

            // Allow a tool to call itself
            if (_currentToolChainInitiator == toolName)
            {
                System.Diagnostics.Debug.WriteLine($"Tool calling itself - allowed");
                return true;
            }

            // If we're in a tool chain, check if the current tool can use the requested tool
            if (!_toolPermissions.ContainsKey(_currentToolChainInitiator))
            {
                System.Diagnostics.Debug.WriteLine($"Current initiator not found in permissions");
                return false;
            }

            var chainAllowed = _toolPermissions[_currentToolChainInitiator].AllowedTools.Contains(toolName);
            System.Diagnostics.Debug.WriteLine($"Tool chain check - allowed: {chainAllowed}");
            return chainAllowed;
        }
    }
}