using System.Data;
using System.Text;

using AnthropicToolUseBuffer.ToolClasses;

using Microsoft.VisualBasic.Devices;

using Newtonsoft.Json;

namespace AnthropicToolUseBuffer
{
    public class ToolTransformerBuilderAnthropic
    {
        private string _name;


        private string _description; 


        private string _instructionHeader; 
        private string _type = "object";
        private Dictionary<string, object> _properties = new();
        private List<string> _requiredFields = new();
        private List<string> _keywords = new();
        private List<string> _constraints = new();
        private List<string> _instructions = new();
        private CacheControl? _cacheControl = null;

        public ToolTransformerBuilderAnthropic AddToolName(string name)
        {
            _name = name ?? throw new ArgumentException("Tool name cannot be null.");
            return this;
        }
        /// <summary>
        /// Adds constraints that limit when or how the tool should be used.
        /// </summary>
        /// <param name="constraints">One or more constraints to add</param>
        /// <returns>The builder instance for method chaining</returns>
        public ToolTransformerBuilderAnthropic AddConstraint(params string[] constraints)
        {
            if (constraints != null)
            {
                foreach (var constraint in constraints)
                {
                    if (!string.IsNullOrEmpty(constraint))
                    {
                        _constraints.Add(constraint);
                    }
                }
            }
            return this;
        }


        //public ToolTransformerBuilder AddConstraint(params string[] constraints)
        //{
        //    if (constraints != null)
        //    {
        //       foreach (var constraint in constraints)
        //        {
        //            if (!string.IsNullOrEmpty(constraint))
        //            {
        //                _constraints.Add(constraint);
        //            }
        //        }           
        //    }
        //    return this;
        //}
        /// <summary>
        /// Adds keywords that help categorize or identify the tool's purpose.
        /// </summary>
        /// <param name="keywords">One or more keywords to add</param>
        /// <returns>The builder instance for method chaining</returns>
        public ToolTransformerBuilderAnthropic AddKeyWords(params string[] keywords)
        {
            if (keywords != null)
            {
                foreach (var keyword in keywords)
                {
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        _keywords.Add(keyword);
                    }
                }
            }
            return this;
        }
        //public ToolTransformerBuilder AddKeyWords(params string[] keywords)
        //{
        //    if (keywords != null)
        //    {
        //        foreach (var keyword in keywords)
        //        {
        //            if (!string.IsNullOrEmpty(keyword))
        //            {
        //                _keywords.Add(keyword);
        //            }
        //        }
        //    }
        //    return this;
        //}


        /// <summary>
        /// Sets the header for the instructions section.
        /// </summary>
        /// <param name="instructionHeader">The header text for instructions</param>
        /// <returns>The builder instance for method chaining</returns>
        public ToolTransformerBuilderAnthropic AddInstructionHeader(string instructionHeader)
        {
            if (!string.IsNullOrEmpty(instructionHeader))
            {
                _instructionHeader = instructionHeader;
            }
            return this;
        }
        //public ToolTransformerBuilder AddInstructionHeader(string instructionHeader)
        //{
        //    // if (instruction != null)
        //    // {
        //    // foreach (var keyword in instructions)
        //    //{
        //    if (!string.IsNullOrEmpty(instructionHeader))
        //    {
        //        _instructionHeader = instructionHeader;
        //    }
        //    //}
        //    // }
        //    return this;
        //}



        /// <summary>
        /// Adds a specific instruction for using the tool.
        /// </summary>
        /// <param name="instruction">The instruction to add</param>
        /// <returns>The builder instance for method chaining</returns>
        public ToolTransformerBuilderAnthropic AddInstructions(string instruction)
        {
            if (!string.IsNullOrEmpty(instruction))
            {
                _instructions.Add(instruction);
            }
            return this;
        }
        //public ToolTransformerBuilder AddInstructions(string instruction)
        //{
        //   // if (instruction != null)
        //   // {
        //        // foreach (var keyword in instructions)
        //        //{
        //            if (!string.IsNullOrEmpty(instruction))
        //            {
        //                _instructions.Add(instruction);
        //            }
        //       //}
        //   // }
        //    return this;
        //}

        public ToolTransformerBuilderAnthropic AddDescription(string description)
        {
            _description = description;
            return this;
        }

        public NestedObjectBuilder AddNestedObject(string objectName, string objectDescription, bool isRequired = true, bool isArray = false)
        {
            return new NestedObjectBuilder(this, null, objectName, objectDescription, isRequired, isArray);
        }

        public ToolTransformerBuilderAnthropic AddProperty(
            string fieldName,
            string fieldType,
            string fieldDescription,
            bool isRequired = false,
            Dictionary<string, string> items = null)
        {
            var propertyDef = new Dictionary<string, object>
            {
                { "type", fieldType },
                { "description", fieldDescription }
            };

            if (items != null)
            {
                propertyDef["items"] = items;
            }

            _properties[fieldName] = propertyDef;

            if (isRequired)
            {
                _requiredFields.Add(fieldName);
            }

            return this;
        }

        internal void SetNestedObject(string objectName, Dictionary<string, object> properties, bool isRequired, bool isArray)
        {
            var props = properties["properties"] as Dictionary<string, object>;
            bool isSingleProperty = props?.Count == 1;

            if (isArray)
            {
                var arraySchema = new Dictionary<string, object>
        {
            { "type", "array" },
            { "items", properties }
        };
                _properties[objectName] = arraySchema;
            }
            else
            {
                if (isSingleProperty)
                {
                    // Don't remove the required field - it should be preserved from BuildDefinition()
                    _properties[objectName] = properties;
                    _requiredFields.Add(objectName);
                    return;
                }

                _properties[objectName] = properties;
            }

            if (isRequired)
            {
                _requiredFields.Add(objectName);
            }
        }

        public Tool Build()
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                throw new InvalidOperationException("Tool name must be set before building.");
            }

            var descriptionBuilder = new StringBuilder();
            descriptionBuilder.Append(_description?.Trim() ?? "This tool processes input data and generates output.");

            // Format and add keywords section
            if (_keywords.Count > 0)
            {
                descriptionBuilder.Append("\n\nKeywords:");
                foreach (var keyword in _keywords)
                {
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        descriptionBuilder.Append($"\n- {keyword}");
                    }
                }
            }

            // Format and add constraints section
            if (_constraints.Count > 0)
            {
                descriptionBuilder.Append("\n\nConstraints:");
                int count = 1;

                foreach (var constraint in _constraints)
                {
                    if (!string.IsNullOrWhiteSpace(constraint))
                    {
                        descriptionBuilder.Append($"\n{count}. {constraint}");
                        count++;
                    }
                }
            }

            // Format and add instructions section
            if (_instructions.Count > 0)
            {
                descriptionBuilder.Append("\n\nInstructions:");
                int count = 1;

                if (!string.IsNullOrWhiteSpace(_instructionHeader))
                {
                    descriptionBuilder.Append($"\n# {_instructionHeader} #");
                }

                foreach (var instruction in _instructions)
                {
                    if (!string.IsNullOrWhiteSpace(instruction))
                    {
                        descriptionBuilder.Append($"\n{count}. {instruction}");
                        count++;
                    }
                }
            }

            var inputSchema = new InputSchema(
                type: _type,
                properties: _properties,
                required: _requiredFields.Any() ? _requiredFields : null
            );

            return new Tool(
                name: _name,
                description: descriptionBuilder.ToString(),
                inputSchema: inputSchema,
                cacheControl: _cacheControl
            );
        }

        // Update Build method to only include required fields if explicitly set
        //public Tool Build()
        //{
        //    if (string.IsNullOrWhiteSpace(_name))
        //    {
        //        throw new InvalidOperationException("Tool name must be set before building.");
        //    }

        //    var descriptionBuilder = new StringBuilder();
        //    descriptionBuilder.Append(_description?.Trim() ?? "This tool processes input data and generates output.");

        //    if (_keywords.Count > 0)
        //    {
        //        descriptionBuilder.Append("\n\nKeywords:");
        //        foreach (var keyword in _keywords)
        //        {
        //            if (!string.IsNullOrWhiteSpace(keyword))
        //            {
        //                descriptionBuilder.Append($"\n- {keyword}");
        //            }
        //        }
        //    }



        //    if (_constraints.Count > 0)
        //    {
        //        descriptionBuilder.Append("\n\nConstraints:");
        //        int count = 1;

        //        foreach (var constraint in _constraints)
        //        {

        //            if (!string.IsNullOrWhiteSpace(constraint))
        //            {
        //                descriptionBuilder.Append($"\n{count}. {constraint}");
        //                count++;
        //            }
        //        }
        //    }


        //    if (_instructions.Count > 0)
        //    {
        //        descriptionBuilder.Append("\n\nInstructions:");
        //        int count = 1;

        //        if (!string.IsNullOrWhiteSpace(_instructionHeader))
        //        {
        //            descriptionBuilder.Append($"\n# {_instructionHeader} #");
        //            count++;
        //        }

        //        foreach (var instruction in _instructions)
        //        {

        //            if (!string.IsNullOrWhiteSpace(instruction))
        //            {
        //                descriptionBuilder.Append($"\n{count}. {instruction}");
        //                count++;
        //            }
        //        }
        //    }


        //    var inputSchema = new InputSchema(
        //        type: _type,
        //        properties: _properties,
        //        required: _requiredFields.Any() ? _requiredFields : null
        //    );

        //    return new Tool(
        //        name: _name,
        //        Description: descriptionBuilder.ToString(),
        //        inputSchema: inputSchema,
        //        cacheControl: _cacheControl
        //    );
        //}
 


    }


    public class NestedObjectBuilder
    {
        private readonly ToolTransformerBuilderAnthropic _parentBuilder;
        private readonly NestedObjectBuilder _parentNestedBuilder;
        private readonly string _objectName;
        private readonly string _objectDescription;
        private readonly Dictionary<string, object> _properties = new();
        private readonly List<string> _requiredFields = new();
        private readonly bool _isArray;
        private readonly bool _isRequired;

        public NestedObjectBuilder(
    ToolTransformerBuilderAnthropic parentBuilder,
    NestedObjectBuilder parentNestedBuilder,
    string objectName,
    string objectDescription,
    bool isRequired,
    bool isArray)
        {
            _parentBuilder = parentBuilder;
            _parentNestedBuilder = parentNestedBuilder;
            _objectName = objectName;
            _objectDescription = objectDescription;
            _isArray = isArray;
            _isRequired = isRequired;
        }
 

        public NestedObjectBuilder AddNestedObject(string objectName, string objectDescription, bool isRequired = true, bool isArray = false)
        {
            return new NestedObjectBuilder(null, this, objectName, objectDescription, isRequired, isArray);
        }

        public NestedObjectBuilder AddProperty(
            string fieldName,
            string fieldType,
            string fieldDescription,
            bool isRequired = false,
            Dictionary<string, string> items = null)
        {
            var propertyDef = new Dictionary<string, object>
        {
            { "type", fieldType },
            { "description", fieldDescription }
        };

            if (items != null)
            {
                propertyDef["items"] = items;
            }

            _properties[fieldName] = propertyDef;

            if (isRequired)
            {
                _requiredFields.Add(fieldName);  // Add to this object's required fields
            }

            return this;
        }

        // In NestedObjectBuilder
        private Dictionary<string, object> BuildDefinition()
        {
            var objectDefinition = new Dictionary<string, object>
    {
        { "type", "object" },
        { "description", _objectDescription },
        { "properties", _properties }
    };

            if (_requiredFields.Count > 0)
            {
                objectDefinition["required"] = _requiredFields;
            }

            return objectDefinition;
        }

        public NestedObjectBuilder EndNestedObject()
        {
            var definition = BuildDefinition();
            if (_parentNestedBuilder != null)
            {
                _parentNestedBuilder._properties[_objectName] = definition;
                return _parentNestedBuilder;
            }
            return this;
        }

        public ToolTransformerBuilderAnthropic EndObject()
        {
            if (_parentBuilder == null)
            {
                throw new InvalidOperationException("Cannot end object without a parent builder");
            }

            var objectDefinition = BuildDefinition();
            _parentBuilder.SetNestedObject(_objectName, objectDefinition, _isRequired, _isArray);
            return _parentBuilder;
        }
    }

     
    public static class ToolStringOutput
    {
        public static string GenerateToolJson(Tool tool)
        {
            return JsonConvert.SerializeObject(tool, Formatting.Indented);
        }

        public static string GenerateToolString(Tool tool)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"new Tool(");
            sb.AppendLine($"    name: \"{tool.name}\",");
            sb.AppendLine($"    description: \"{tool.description}\",");

            sb.AppendLine($"    inputSchema: new InputSchema(");
            sb.AppendLine($"        type: \"{tool.input_schema.type}\",");

            sb.AppendLine($"        properties: new Dictionary<string, object>");
            sb.AppendLine($"        {{");
            FormatProperties(sb, tool.input_schema.properties);
            sb.AppendLine($"        }},");

            if (tool.input_schema.required != null && tool.input_schema.required.Count > 0)
            {
                sb.AppendLine($"        required: new List<string> {{ {string.Join(", ", tool.input_schema.required.Select(r => $"\"{r}\""))} }}");
            }
            else
            {
                sb.AppendLine($"        required: null");
            }

            sb.AppendLine($"    )");

            if (tool.cache_control != null)
            {
                sb.AppendLine($"    cacheControl: new CacheControl()");
            }

            sb.AppendLine($");");

            return sb.ToString();
        }

        private static void FormatProperties(StringBuilder sb, Dictionary<string, object> properties)
        {
            foreach (var prop in properties)
            {
                sb.AppendLine($"            {{");
                sb.AppendLine($"                \"{prop.Key}\", new");
                sb.AppendLine($"                {{");

                var propDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(prop.Value));

                foreach (var detail in propDict)
                {
                    if (detail.Value is Newtonsoft.Json.Linq.JObject)
                    {
                        sb.AppendLine($"                    {detail.Key} = new");
                        sb.AppendLine($"                    {{");
                        var nestedProps = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                            detail.Value.ToString());
                        foreach (var nested in nestedProps)
                        {
                            sb.AppendLine($"                        {nested.Key} = \"{nested.Value}\",");
                        }
                        sb.AppendLine($"                    }},");
                    }
                    else
                    {
                        sb.AppendLine($"                    {detail.Key} = \"{detail.Value}\",");
                    }
                }

                sb.AppendLine($"                }}");
                sb.AppendLine($"            }},");
            }
        }
    }
}