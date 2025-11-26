using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace BbcSoundz.Helpers
{
    public static class RichTextBoxHelper
    {
        private static bool _lastLineWasProgress = false;
        private const int MaxLines = 100; // Limit output to 100 lines

        public static void AppendColoredText(System.Windows.Controls.RichTextBox richTextBox, string text)
        {
            if (richTextBox.Dispatcher.CheckAccess())
            {
                AppendColoredTextInternal(richTextBox, text);
            }
            else
            {
                richTextBox.Dispatcher.Invoke(() => AppendColoredTextInternal(richTextBox, text));
            }
        }

        private static void AppendColoredTextInternal(System.Windows.Controls.RichTextBox richTextBox, string text)
        {
            // Check if this is a progress line that should replace the previous one
            bool isProgressLine = IsProgressLine(text);
            
            // If the current line is progress and the last line was also progress, replace it
            if (isProgressLine && _lastLineWasProgress && richTextBox.Document.Blocks.Count > 0)
            {
                // Remove the last paragraph (progress line)
                var lastBlock = richTextBox.Document.Blocks.LastBlock;
                if (lastBlock != null)
                {
                    richTextBox.Document.Blocks.Remove(lastBlock);
                }
            }

            var paragraph = new Paragraph();
            var run = new Run(text + Environment.NewLine);

            // Apply colors based on content
            if (text.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            {
                run.Foreground = Brushes.Red;
                run.FontWeight = FontWeights.Bold;
            }
            else if (text.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
            {
                run.Foreground = Brushes.Orange;
            }
            else if (text.Contains("[download]"))
            {
                run.Foreground = Brushes.Cyan;
            }
            else if (text.Contains("100%") || text.Contains("completed successfully"))
            {
                run.Foreground = Brushes.LimeGreen;
                run.FontWeight = FontWeights.Bold;
            }
            else if (isProgressLine)
            {
                run.Foreground = Brushes.Yellow;
            }
            else if (text.StartsWith("Starting download:") || text.StartsWith("Command:"))
            {
                run.Foreground = Brushes.LightBlue;
                run.FontWeight = FontWeights.Bold;
            }
            else if (text.Contains("successfully") || text.Contains("Downloaded"))
            {
                run.Foreground = Brushes.LimeGreen;
            }
            else if (text.Contains("Files saved to:"))
            {
                run.Foreground = Brushes.LightGreen;
                run.FontWeight = FontWeights.Bold;
            }
            else
            {
                run.Foreground = Brushes.White;
            }

            paragraph.Inlines.Add(run);
            richTextBox.Document.Blocks.Add(paragraph);

            // Track if this was a progress line
            _lastLineWasProgress = isProgressLine;

            // Limit the number of lines to prevent excessive memory usage
            while (richTextBox.Document.Blocks.Count > MaxLines)
            {
                var firstBlock = richTextBox.Document.Blocks.FirstBlock;
                if (firstBlock != null)
                {
                    richTextBox.Document.Blocks.Remove(firstBlock);
                }
            }

            // Auto-scroll to bottom
            richTextBox.ScrollToEnd();
        }

        private static bool IsProgressLine(string text)
        {
            // Check for various progress indicators that should replace previous lines
            return Regex.IsMatch(text, @"\d+\.?\d*%") || 
                   text.Contains("ETA") || 
                   text.Contains(" at ") ||
                   text.Contains("MiB/s") ||
                   text.Contains("KiB/s") ||
                   text.Contains("MB/s") ||
                   text.Contains("KB/s") ||
                   text.Contains("downloading") ||
                   (text.Contains("[download]") && (text.Contains("%") || text.Contains("ETA"))) ||
                   text.StartsWith("[") && text.Contains("]") && text.Contains("%");
        }

        public static void ClearText(System.Windows.Controls.RichTextBox richTextBox)
        {
            if (richTextBox.Dispatcher.CheckAccess())
            {
                richTextBox.Document.Blocks.Clear();
                _lastLineWasProgress = false;
            }
            else
            {
                richTextBox.Dispatcher.Invoke(() => 
                {
                    richTextBox.Document.Blocks.Clear();
                    _lastLineWasProgress = false;
                });
            }
        }
    }
}
