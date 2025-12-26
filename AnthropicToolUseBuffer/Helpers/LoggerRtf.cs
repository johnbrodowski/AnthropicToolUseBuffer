using System.Drawing;

using static System.Net.Mime.MediaTypeNames;

using Font = System.Drawing.Font;

namespace AnthropicToolUseBuffer.Helpers
{
    public class TextStyleRtb
    {
        public Color Color { get; set; } = Color.Black;
        public int FontSize { get; set; } = 12;
        public bool Bold { get; set; } = false;
        public bool Italic { get; set; } = false;
        public bool Underlined { get; set; } = false;
    }

    public class LoggerRtb
    {
        public async Task WriteLineRtb(RichTextBox richTextBox, string text = "", Color color = default)
        {
            if (color == default)
            {
                color = Color.Black;
            }

            if (richTextBox.InvokeRequired)
            {
                await richTextBox.InvokeAsync(async () => await WriteLineRtb(richTextBox, text, color));
            }
            else
            {
                AppendColoredTextRtb(richTextBox, text + Environment.NewLine, color);
            }
        }

        // WriteLine: Two texts with two colors
        public async Task WriteLineRtb(RichTextBox richTextBox, string text1, string text2, Color color1, Color color2)
        {
            if (richTextBox.InvokeRequired)
            {
                await richTextBox.InvokeAsync(async () => await WriteLineRtb(richTextBox, text1, text2, color1, color2));
            }
            else
            {
                AppendColoredTextRtb(richTextBox, text1, color1);
                AppendColoredTextRtb(richTextBox, text2 + Environment.NewLine, color2);
            }
        }

        // Write: Single text with single color
        public async Task WriteRtb(RichTextBox richTextBox, string text = "", Color color = default)
        {
            if (color == default)
            {
                color = Color.Black;
            }

            if (richTextBox.InvokeRequired)
            {
                await richTextBox.InvokeAsync(async () => await WriteRtb(richTextBox, text, color));
            }
            else
            {
                AppendColoredTextRtb(richTextBox, text, color);
            }
        }

        // Write: Two texts with two colors
        public async Task WriteRtb(RichTextBox richTextBox, string text1 = "", string text2 = "", Color color1 = default, Color color2 = default)
        {
            if (color1 == default)
            {
                color1 = Color.Black;
            }
            if (color2 == default)
            {
                color2 = Color.Black;
            }

            if (richTextBox.InvokeRequired)
            {
                await richTextBox.InvokeAsync(async () => await WriteRtb(richTextBox, text1, text2, color1, color2));

                //  await richTextBox.InvokeAsync(() => WriteLine(richTextBox, text1, text2, color1, color2)).ConfigureAwait(false);
            }
            else
            {
                AppendColoredTextRtb(richTextBox, text1, color1);
                AppendColoredTextRtb(richTextBox, text2, color2);
            }
        }
 
        public async Task WriteLineRtb(
        RichTextBox richTextBox,
        string text,
        TextStyleRtb? style = null
    )
        {
            style ??= new TextStyleRtb();

            if (richTextBox.InvokeRequired)
            {
                //  await richTextBox.InvokeAsync(() => WriteLine(richTextBox, text, style));
                await richTextBox.InvokeAsync(async () => await WriteLineRtb(richTextBox, text, style));
            }
            else
            {
                AppendStyledTextRtb(richTextBox, text + Environment.NewLine, style);
            }
        }

        // WriteLine: Two texts with two TextStyles
        public async Task WriteLineRtb(
            RichTextBox richTextBox,
            string text1,
            string text2,
            TextStyleRtb? style1 = null,
            TextStyleRtb? style2 = null
        )
        {
            style1 ??= new TextStyleRtb();
            style2 ??= new TextStyleRtb();

            if (richTextBox.InvokeRequired)
            {
                await richTextBox.InvokeAsync(async () => await WriteLineRtb(richTextBox, text1, text2, style1, style2));
            }
            else
            {
                AppendStyledTextRtb(richTextBox, text1, style1);
                AppendStyledTextRtb(richTextBox, text2 + Environment.NewLine, style2);
            }
        }

        // Write: Single text with a TextStyle
        public async Task WriteRtb(
            RichTextBox richTextBox,
            string text,
            TextStyleRtb? style = null
        )
        {
            style ??= new TextStyleRtb();

            if (richTextBox.InvokeRequired)
            {
                await richTextBox.InvokeAsync(async () => await WriteRtb(richTextBox, text, style));
            }
            else
            {
                AppendStyledTextRtb(richTextBox, text, style);
            }
        }

        // Write: Two texts with two TextStyles
        public async Task WriteRtb(
            RichTextBox richTextBox,
            string text1,
            string text2,
            TextStyleRtb? style1 = null,
            TextStyleRtb? style2 = null
        )
        {
            style1 ??= new TextStyleRtb();
            style2 ??= new TextStyleRtb();

            if (richTextBox.InvokeRequired)
            {
                await richTextBox.InvokeAsync(async () => await WriteRtb(richTextBox, text1, text2, style1, style2));
            }
            else
            {
                AppendStyledTextRtb(richTextBox, text1, style1);
                AppendStyledTextRtb(richTextBox, text2, style2);
            }
        }


        private void AppendColoredTextRtb(RichTextBox richTextBox, string text, Color color)
        {
            int selectionStart = richTextBox.TextLength;

            richTextBox.AppendText(text);
            richTextBox.Select(selectionStart, text.Length);
            richTextBox.SelectionColor = color;

            richTextBox.Select(richTextBox.TextLength, 0);
            richTextBox.ScrollToCaret();
        }



        // Helper to append styled text to a RichTextBox
        private void AppendStyledTextRtb(RichTextBox richTextBox, string text, TextStyleRtb style )
        {
            int selectionStart = richTextBox.TextLength;

            richTextBox.AppendText(text);
            richTextBox.Select(selectionStart, text.Length);
            richTextBox.SelectionColor = style.Color;

            // Apply font styles
            FontStyle fontStyle = FontStyle.Regular;
            if (style.Bold) fontStyle |= FontStyle.Bold;
            if (style.Italic) fontStyle |= FontStyle.Italic;
            if (style.Underlined) fontStyle |= FontStyle.Underline;

            richTextBox.SelectionFont = new Font(richTextBox.Font.FontFamily, style.FontSize, fontStyle);

            richTextBox.Select(richTextBox.TextLength, 0);
            richTextBox.ScrollToCaret();
        }
    }
 
}