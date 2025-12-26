using System;
using System.Collections.Generic;
using System.Text;

namespace AnthropicToolUseBuffer.AIClassesAnthropic
{
    public enum ChatUser
    {
        User,
        Usage,
        Assistant,
        AssistantStream,
        System,
        Error,
        Debug,
        Warning,
        Info,
        RawData,
        PythonStatusUpdated,
        PythonCompileResults,
        PythonErrorOccurred,
        PythonErrorCompile,
        PythonSuccessCompile,
        PythonOutPutMessage,
        PythonInputRequested,
        CodeStream
    }
}
