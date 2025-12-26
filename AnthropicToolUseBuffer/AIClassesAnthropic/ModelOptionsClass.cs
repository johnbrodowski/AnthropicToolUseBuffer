using System;
using System.Collections.Generic;

namespace AnthropicToolUseBuffer
{
    public class ModelOption
    {
        private ModelOption(string value) { Value = value; }

        public string Value { get; }

        public static readonly ModelOption Claude45Sonnet = new ModelOption("claude-sonnet-4-5-20250929");
        public static readonly ModelOption Claude45Haiku = new ModelOption("claude-haiku-4-5-20251001");
        
        public static readonly ModelOption Claude41Opus = new ModelOption("claude-opus-4-1-20250805");


        public static readonly ModelOption Claude4Sonnet = new ModelOption("claude-sonnet-4-20250514");
        public static readonly ModelOption Claude4Opus = new ModelOption("claude-opus-4-20250514");
       
        public static readonly ModelOption Claude37Sonnet = new ModelOption("claude-3-7-sonnet-20250219");
        
        public static readonly ModelOption Claude35Sonnet_deprecated  = new ModelOption("claude-3-5-sonnet-latest");
        public static readonly ModelOption Claude35Haiku = new ModelOption("claude-3-5-haiku-latest");



        public static readonly List<ModelOption> AllModels = new List<ModelOption>
        {
            Claude4Sonnet,
            Claude45Sonnet,
            Claude45Haiku,
            Claude4Opus,
            Claude41Opus,
            Claude37Sonnet,
            Claude35Sonnet_deprecated,
            Claude35Haiku
        };

        public static implicit operator string(ModelOption modelOption) => modelOption.Value;

        public static bool operator ==(ModelOption left, ModelOption right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);
            return left.Equals(right);
        }

        public static bool operator !=(ModelOption left, ModelOption right) => !(left == right);

        public override bool Equals(object obj)
        {
            if (obj is ModelOption other)
                return Value == other.Value;
            return false;
        }

        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;
    }
}
