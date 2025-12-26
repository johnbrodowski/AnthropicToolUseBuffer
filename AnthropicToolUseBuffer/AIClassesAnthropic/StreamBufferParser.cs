using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Text.RegularExpressions;

    public class StreamBufferParser
    {
        // Our buffer to accumulate incoming chunks.
        private readonly StringBuilder _buffer = new StringBuilder();

        // Define our tags. For each tag key (here "code") we store the start and stop markers.
        private readonly Dictionary<string, (string Start, string Stop)> _tags =
            new Dictionary<string, (string Start, string Stop)>
            {
            { "code", ("<code_start>", "<code_stop>") },
                // You can add more tags here if needed.
            };



        public StreamBufferParser()
        {

        }






        /// <summary>
        /// Call this each time a new chunk of data arrives.
        /// </summary>
        public void AppendChunk(string chunk)
        {
            _buffer.Append(chunk);
            ProcessBuffer();
        }

        /// <summary>
        /// Process complete tokens (plain text or tagged sections) from the buffer.
        /// Incomplete tokens remain in the buffer until more data arrives.
        /// </summary>
        private void ProcessBuffer()
        {
            string data = _buffer.ToString();
            int currentIndex = 0;

            // Process as long as there is data in the buffer.
            while (currentIndex < data.Length)
            {
                // Look for the earliest occurrence of any start tag.
                int nextTagPos = data.Length; // initialize to end of string
                string? foundTagKey = null;
                (string Start, string Stop) foundTag;

                foreach (var tag in _tags)
                {
                    int pos = data.IndexOf(tag.Value.Start, currentIndex, StringComparison.Ordinal);
                    if (pos >= 0 && pos < nextTagPos)
                    {
                        nextTagPos = pos;
                        foundTagKey = tag.Key;
                    }
                }

                if (foundTagKey == null)
                {
                    // No start tag found.
                    // However, the end of the buffer might contain an incomplete tag.
                    int incompleteLength = GetIncompleteTagPrefixLength(data);
                    // Everything except the last 'incompleteLength' characters is complete plain text.
                    int plainTextLength = data.Length - incompleteLength;
                    if (plainTextLength > currentIndex)
                    {
                        string plainText = data.Substring(currentIndex, plainTextLength - currentIndex);
                        ProcessPlainText(plainText);
                    }

                    // Remove the processed portion from the buffer, keeping the incomplete end.
                    _buffer.Clear();
                    _buffer.Append(data.Substring(plainTextLength));
                    return;
                }
                else
                {
                    // Process any plain text that comes before the tag.
                    if (nextTagPos > currentIndex)
                    {
                        string plainText = data.Substring(currentIndex, nextTagPos - currentIndex);
                        ProcessPlainText(plainText);
                        currentIndex = nextTagPos;
                    }

                    // At this point, currentIndex is at the start of a tag.
                    foundTag = _tags[foundTagKey];

                    // Check if the start tag itself is complete.
                    if (!IsTagComplete(data, currentIndex, foundTag.Start))
                    {
                        // Incomplete tag marker: wait for more data.
                        break;
                    }
                    // Skip past the start tag marker.
                    currentIndex += foundTag.Start.Length;

                    // Now try to find the corresponding end tag.
                    int endTagPos = data.IndexOf(foundTag.Stop, currentIndex, StringComparison.Ordinal);
                    if (endTagPos < 0)
                    {
                        // The end tag wasn’t found (or it may be incomplete), so wait for more data.
                        break;
                    }
                    // Extract the content between the start and end tags.
                    string tagContent = data.Substring(currentIndex, endTagPos - currentIndex);
                    ProcessTagContent(foundTagKey, tagContent);

                    // Move past the end tag marker.
                    currentIndex = endTagPos + foundTag.Stop.Length;
                }
            }

            // Remove the processed text from the buffer.
            _buffer.Clear();
            if (currentIndex < data.Length)
            {
                _buffer.Append(data.Substring(currentIndex));
            }
        }

        /// <summary>
        /// Checks if the tag marker at position 'index' in 'data' is fully present.
        /// </summary>
        private bool IsTagComplete(string data, int index, string tagMarker)
        {
            return (data.Length - index) >= tagMarker.Length;
        }

        /// <summary>
        /// Checks the end of the string for any incomplete tag start marker.
        /// Returns the length of the incomplete portion (if any), so that we don’t process it.
        /// </summary>
        private int GetIncompleteTagPrefixLength(string data)
        {
            int maxIncomplete = 0;
            // For each tag, check if the end of the data might be the beginning of the tag marker.
            foreach (var tag in _tags.Values)
            {
                int incompleteLength = GetMatchingPrefixLength(tag.Start, data);
                if (incompleteLength > maxIncomplete)
                {
                    maxIncomplete = incompleteLength;
                }
            }
            return maxIncomplete;
        }

        /// <summary>
        /// Returns how many characters at the end of 'data' match the beginning of 'tagMarker'.
        /// </summary>
        private int GetMatchingPrefixLength(string tagMarker, string data)
        {
            int maxPossible = Math.Min(tagMarker.Length, data.Length);
            for (int len = maxPossible; len > 0; len--)
            {
                if (tagMarker.StartsWith(data.Substring(data.Length - len, len)))
                {
                    return len;
                }
            }
            return 0;
        }

        /// <summary>
        /// Processes plain text portions (outside any tag).
        /// Replace this with whatever handling you need.
        /// </summary>
        private void ProcessPlainText(string text)
        {
            // For example, simply output it.
          //  Debug.Write("Plain Text: " + text);
        }

        /// <summary>
        /// Processes the content within a tag. In this example we only have a "code" tag.
        /// Extend this method to handle additional tags.
        /// </summary>
        private void ProcessTagContent(string tagKey, string content)
        {
            if (tagKey == "code")
            {
                string unescapedPythonCode = Regex.Unescape(content);
                //Debug.Write(content);
                Debug.WriteLine($"{tagKey} Code: \n" + unescapedPythonCode);
            }
            else
            {
                // Add handling for other tags as needed.
              //  Debug.WriteLine($"{tagKey} Content: " + content);
            }
        }
    }

}
