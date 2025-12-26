
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 

namespace AnthropicToolUseBuffer
{
    public interface IMessageContentAnthropic
    {
        string type { get;}
        string? text { get; set; }
        CacheControl? CacheControl { get; set; }
    }
}
