
using AnthropicToolUseBuffer.ToolClasses;

using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

 

namespace AnthropicToolUseBuffer
{
    internal class LoadTools
    {
 
        public static async Task<List<Tool>> AnthropicUITools(ToolPermissionManager _toolPermissions, bool outputPreview = false, bool FullAccess = true)
        {
            List<Tool> toolList = new();


            var toolListPreview = new StringBuilder();

            var allToolsAllowed = new[] {
                 "tool_buffer_demo" //1
            };

 
            var toolBufferDemo = new ToolTransformerBuilderAnthropic()
                .AddToolName("tool_buffer_demo")
                .AddDescription("Demonstrates asynchronous tool execution buffering. This tool simulates a long-running operation to test the tool use buffering mechanism.")

                .AddNestedObject(
                    objectName: "tool_buffer_demo_params",
                    objectDescription: "Parameters for the tool buffer demonstration.",
                    isRequired: true
                    )
                    .AddProperty(
                        fieldName: "sample_data",
                        fieldType: "string",
                        fieldDescription: "Sample data for the test.",
                        isRequired: false
                        )
                .EndNestedObject()
                .EndObject()
                .Build();

            toolList.Add(toolBufferDemo);
            toolListPreview.AppendLine(ToolStringOutput.GenerateToolJson(toolBufferDemo));
            _toolPermissions.RegisterTool(toolName: "tool_buffer_demo", canInitiateToolChain: true, allowedTools: allToolsAllowed);
 
            if (outputPreview)
            {
                Debug.WriteLine(toolListPreview.ToString());
                foreach (var toolPermission in _toolPermissions._toolPermissions)
                {
                    Debug.WriteLine($"Registered tool: {toolPermission.Key}");
                    Debug.WriteLine($"  Can initiate: {toolPermission.Value.CanInitiateToolChain}");
                    Debug.WriteLine($"  Allowed tools: {string.Join(", ", toolPermission.Value.AllowedTools)}");
                }
            }
            return toolList;

        }

 
    }
}