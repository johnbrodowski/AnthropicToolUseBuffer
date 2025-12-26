using AnthropicToolUseBuffer.ToolClasses;

using System;
using System.Collections.Generic;
using System.Linq;

namespace AnthropicToolUseBuffer
{
    /// <summary>
    /// Converts UniversalToolDefinition to provider-specific tool formats.
    /// Reuses existing provider-specific builders internally.
    /// </summary>
    public static class ToolConverter
    {
        #region Anthropic

        public static Tool ToAnthropic(this UniversalToolDefinition definition)
        {
            var builder = new ToolTransformerBuilderAnthropic()
                .AddToolName(definition.Name)
                .AddDescription(definition.Description);

            ApplyMetadata(builder, definition);
            ApplyProperties(builder, definition);

            return builder.Build();
        }

        private static void ApplyMetadata(ToolTransformerBuilderAnthropic builder, UniversalToolDefinition definition)
        {
            if (definition.Keywords?.Count > 0)
                builder.AddKeyWords(definition.Keywords.ToArray());

            if (definition.Constraints?.Count > 0)
                builder.AddConstraint(definition.Constraints.ToArray());

            if (!string.IsNullOrEmpty(definition.InstructionHeader))
                builder.AddInstructionHeader(definition.InstructionHeader);

            if (definition.Instructions?.Count > 0)
            {
                foreach (var instruction in definition.Instructions)
                    builder.AddInstructions(instruction);
            }
        }

        private static void ApplyProperties(ToolTransformerBuilderAnthropic builder, UniversalToolDefinition definition)
        {
            foreach (var prop in definition.Properties)
            {
                AddPropertyToAnthropic(builder, prop.Key, prop.Value, definition.RequiredFields.Contains(prop.Key));
            }
        }

        private static void AddPropertyToAnthropic(ToolTransformerBuilderAnthropic builder, string name, UniversalProperty prop, bool isRequired)
        {
            if (prop.NestedProperties != null)
            {
                var nestedBuilder = builder.AddNestedObject(name, prop.Description, isRequired, prop.IsArray);
                AddNestedPropertiesToAnthropic(nestedBuilder, prop);
                nestedBuilder.EndObject();
            }
            else
            {
                builder.AddProperty(name, prop.Type, prop.Description, isRequired, prop.Items);
            }
        }

        private static void AddNestedPropertiesToAnthropic(NestedObjectBuilder builder, UniversalProperty prop)
        {
            foreach (var nested in prop.NestedProperties)
            {
                var isRequired = prop.RequiredFields?.Contains(nested.Key) ?? false;

                if (nested.Value.NestedProperties != null)
                {
                    var nestedBuilder = builder.AddNestedObject(nested.Key, nested.Value.Description, isRequired, nested.Value.IsArray);
                    AddNestedPropertiesToAnthropic(nestedBuilder, nested.Value);
                    nestedBuilder.EndNestedObject();
                }
                else
                {
                    builder.AddProperty(nested.Key, nested.Value.Type, nested.Value.Description, isRequired, nested.Value.Items);
                }
            }
        }

        #endregion

 
    }
}
