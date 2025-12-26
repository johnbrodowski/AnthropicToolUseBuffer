using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public class AnthropicRateLimits
    {
        public string RequestsLimit { get; set; }
        public string RequestsRemaining { get; set; }
        public DateTime? RequestsReset { get; set; }

        public string TokensLimit { get; set; }
        public string TokensRemaining { get; set; }
        public DateTime? TokensReset { get; set; }

        public string InputTokensLimit { get; set; }
        public string InputTokensRemaining { get; set; }
        public DateTime? InputTokensReset { get; set; }

        public string OutputTokensLimit { get; set; }
        public string OutputTokensRemaining { get; set; }
        public DateTime? OutputTokensReset { get; set; }

        public int? RetryAfter { get; set; }
    }
}
