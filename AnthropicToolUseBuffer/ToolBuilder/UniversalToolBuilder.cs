using System;
using System.Collections.Generic;

namespace AnthropicToolUseBuffer
{
    /// <summary>
    /// Builder for creating provider-agnostic tool definitions.
    /// Use this to define a tool once, then convert to any provider format.
    /// </summary>
    public class UniversalToolBuilder
    {
        private readonly UniversalToolDefinition _definition;

        public UniversalToolBuilder()
        {
            _definition = new UniversalToolDefinition(null);
        }

        public UniversalToolBuilder AddToolName(string name)
        {
            _definition.Name = name ?? throw new ArgumentException("Tool name cannot be null.");
            return this;
        }

        public UniversalToolBuilder AddDescription(string description)
        {
            _definition.Description = description;
            return this;
        }

        public UniversalToolBuilder SetStrict(bool strict = false)
        {
            _definition.Strict = strict;
            return this;
        }

        public UniversalToolBuilder SetAdditionalProperties(bool allowAdditional)
        {
            _definition.AdditionalProperties = allowAdditional;
            return this;
        }

        public UniversalToolBuilder AddConstraint(params string[] constraints)
        {
            if (constraints != null)
            {
                foreach (var constraint in constraints)
                {
                    if (!string.IsNullOrEmpty(constraint))
                    {
                        _definition.Constraints.Add(constraint);
                    }
                }
            }
            return this;
        }

        public UniversalToolBuilder AddKeyWords(params string[] keywords)
        {
            if (keywords != null)
            {
                foreach (var keyword in keywords)
                {
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        _definition.Keywords.Add(keyword);
                    }
                }
            }
            return this;
        }

        public UniversalToolBuilder AddInstructionHeader(string instructionHeader)
        {
            if (!string.IsNullOrEmpty(instructionHeader))
            {
                _definition.InstructionHeader = instructionHeader;
            }
            return this;
        }

        public UniversalToolBuilder AddInstructions(string instruction)
        {
            if (!string.IsNullOrEmpty(instruction))
            {
                _definition.Instructions.Add(instruction);
            }
            return this;
        }

        public UniversalNestedObjectBuilder AddNestedObject(string objectName, string objectDescription, bool isRequired = true, bool isArray = false)
        {
            return new UniversalNestedObjectBuilder(this, null, objectName, objectDescription, isRequired, isArray);
        }

        public UniversalToolBuilder AddProperty(
            string fieldName,
            string fieldType,
            string fieldDescription,
            bool isRequired = false,
            Dictionary<string, string> items = null)
        {
            var property = new UniversalProperty(fieldType, fieldDescription)
            {
                Items = items
            };

            _definition.Properties[fieldName] = property;

            if (isRequired)
            {
                _definition.RequiredFields.Add(fieldName);
            }

            return this;
        }

        internal void SetNestedObject(string objectName, UniversalProperty property, bool isRequired)
        {
            _definition.Properties[objectName] = property;

            if (isRequired)
            {
                _definition.RequiredFields.Add(objectName);
            }
        }

        public UniversalToolDefinition Build()
        {
            if (string.IsNullOrWhiteSpace(_definition.Name))
            {
                throw new InvalidOperationException("Tool name must be set before building.");
            }

            return _definition;
        }
    }

    /// <summary>
    /// Builder for nested objects within a universal tool definition.
    /// </summary>
    public class UniversalNestedObjectBuilder
    {
        private readonly UniversalToolBuilder _parentBuilder;
        private readonly UniversalNestedObjectBuilder _parentNestedBuilder;
        private readonly string _objectName;
        private readonly string _objectDescription;
        private readonly Dictionary<string, UniversalProperty> _properties = new();
        private readonly List<string> _requiredFields = new();
        private readonly bool _isArray;
        private readonly bool _isRequired;

        public UniversalNestedObjectBuilder(
            UniversalToolBuilder parentBuilder,
            UniversalNestedObjectBuilder parentNestedBuilder,
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

        public UniversalNestedObjectBuilder AddNestedObject(string objectName, string objectDescription, bool isRequired = true, bool isArray = false)
        {
            return new UniversalNestedObjectBuilder(null, this, objectName, objectDescription, isRequired, isArray);
        }

        public UniversalNestedObjectBuilder AddProperty(
            string fieldName,
            string fieldType,
            string fieldDescription,
            bool isRequired = false,
            Dictionary<string, string> items = null)
        {
            var property = new UniversalProperty(fieldType, fieldDescription)
            {
                Items = items
            };

            _properties[fieldName] = property;

            if (isRequired)
            {
                _requiredFields.Add(fieldName);
            }

            return this;
        }

        private UniversalProperty BuildDefinition()
        {
            var property = new UniversalProperty("object", _objectDescription)
            {
                NestedProperties = _properties,
                RequiredFields = _requiredFields.Count > 0 ? _requiredFields : null,
                IsArray = _isArray
            };

            return property;
        }

        public UniversalNestedObjectBuilder EndNestedObject()
        {
            var definition = BuildDefinition();
            if (_parentNestedBuilder != null)
            {
                _parentNestedBuilder._properties[_objectName] = definition;
                return _parentNestedBuilder;
            }
            return this;
        }

        public UniversalToolBuilder EndObject()
        {
            if (_parentBuilder == null)
            {
                throw new InvalidOperationException("Cannot end object without a parent builder");
            }

            var property = BuildDefinition();
            _parentBuilder.SetNestedObject(_objectName, property, _isRequired);
            return _parentBuilder;
        }
    }
}
