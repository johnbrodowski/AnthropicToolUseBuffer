using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    public class ApiError
    {
        public int StatusCode { get; set; }
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public static ApiError FromStatusCode(int statusCode, string responseContent)
        {
            return statusCode switch
            {
                400 => new ApiError { StatusCode = 400, ErrorType = "invalid_request_error", ErrorMessage = "There was an issue with the format or content of your request." },
                401 => new ApiError { StatusCode = 401, ErrorType = "authentication_error", ErrorMessage = "There’s an issue with your API key." },
                403 => new ApiError { StatusCode = 403, ErrorType = "permission_error", ErrorMessage = "Your API key does not have permission to use the specified resource." },
                404 => new ApiError { StatusCode = 404, ErrorType = "not_found_error", ErrorMessage = "The requested resource was not found." },
                413 => new ApiError { StatusCode = 413, ErrorType = "request_too_large", ErrorMessage = "Request exceeds the maximum allowed number of bytes." },
                429 => new ApiError { StatusCode = 429, ErrorType = "rate_limit_error", ErrorMessage = "Your account has hit a rate limit." },
                500 => new ApiError { StatusCode = 500, ErrorType = "api_error", ErrorMessage = "An unexpected error has occurred internal to Anthropic’s systems." },
                529 => new ApiError { StatusCode = 529, ErrorType = "overloaded_error", ErrorMessage = "Anthropic’s API is temporarily overloaded." },
                _ => new ApiError { StatusCode = statusCode, ErrorType = "unknown_error", ErrorMessage = "An unknown error occurred." }
            };
        }
    }

}
