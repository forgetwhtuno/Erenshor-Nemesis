using System;
using System.Collections.Generic;
using System.Text;

namespace ErenshorNemesis
{
    internal sealed class NemesisConversationLine
    {
        internal bool FromPlayer;
        internal string Text;

        internal NemesisConversationLine(bool fromPlayer, string text)
        {
            FromPlayer = fromPlayer;
            Text = text ?? string.Empty;
        }
    }

    // Deterministic ownership and bounded HEARD-chat policy. This file deliberately knows nothing
    // about Unity, Deep Sims, or game state so the routing boundary can be tested without Erenshor.
    internal static class NemesisConversationPolicy
    {
        internal const int MaxRecentLines = 6;
        internal const int MaxStoredLineChars = 120;

        internal static bool TryExtractDirectAddress(string rawText, string nemesisName, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(rawText) || string.IsNullOrWhiteSpace(nemesisName)) return false;

            string text = rawText.Trim();
            // Group chat is still eligible only when the CURRENT Nemesis is explicitly addressed.
            // No other /command is ever consumed here.
            if (text.StartsWith("/group ", StringComparison.OrdinalIgnoreCase)) text = text.Substring(7).TrimStart();
            else if (text.StartsWith("/p ", StringComparison.OrdinalIgnoreCase)) text = text.Substring(3).TrimStart();
            else if (text.StartsWith("/", StringComparison.Ordinal)) return false;

            string name = nemesisName.Trim();
            if (text.Length <= name.Length || !text.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return false;

            // A name prefix without directed punctuation is not strong enough: "Ariadne is here"
            // must remain ordinary chat. Require comma/colon/dash after the exact name.
            int cursor = name.Length;
            // The separator may be spaced off the name: a dash is normally written
            // "Ariadne - ...", and requiring it immediately after the name rejected that
            // ordinary form. Skipping leading whitespace does not weaken the guard, because
            // the next character must still be directed punctuation - "Ariadne is here"
            // reaches 'i' and is refused.
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor])) cursor++;
            if (cursor >= text.Length) return false;
            char separator = text[cursor];
            if (separator != ',' && separator != ':' && separator != '-' && separator != '\u2014') return false;
            cursor++;
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor])) cursor++;
            if (cursor >= text.Length) return false;

            message = text.Substring(cursor).Trim();
            return message.Length > 0;
        }

        // A live standalone test showed a plain whisper to the Nemesis ("/t Ariadne hey") falling
        // straight through to native Erenshor tell-handling instead of ever reaching Nemesis's own
        // reply pipeline - the direct-address path above only recognizes party/group/local chat that
        // starts with the exact name plus punctuation, and a "/" command other than /group or /p is
        // refused before that check even runs. This mirrors the same targeted-command shape used
        // elsewhere in this project for whisper detection (command target message), independently,
        // so Nemesis has no dependency on any other mod for it. The target must match the CURRENT
        // Nemesis's exact name - a whisper to any other Sim is refused and left untouched.
        private static readonly string[] WhisperCommands = { "/whisper", "/tell", "/w", "/t" };

        internal static bool TryExtractWhisperAddress(string rawText, string nemesisName, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(rawText) || string.IsNullOrWhiteSpace(nemesisName)) return false;
            string trimmed = rawText.Trim();
            string matched = null;
            for (int i = 0; i < WhisperCommands.Length; i++)
            {
                string command = WhisperCommands[i];
                if (trimmed.Equals(command, StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase))
                { matched = command; break; }
            }
            if (matched == null || trimmed.Length <= matched.Length) return false;
            string rest = trimmed.Substring(matched.Length).Trim();
            int firstSpace = rest.IndexOf(' ');
            if (firstSpace <= 0 || firstSpace >= rest.Length - 1) return false;
            string target = rest.Substring(0, firstSpace).Trim();
            if (!string.Equals(target, nemesisName.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            message = rest.Substring(firstSpace + 1).Trim();
            return message.Length > 0;
        }

        internal static void AddBounded(List<NemesisConversationLine> lines, bool fromPlayer, string text)
        {
            if (lines == null) return;
            string clean = Compact(text, MaxStoredLineChars);
            if (clean.Length == 0) return;
            lines.Add(new NemesisConversationLine(fromPlayer, clean));
            while (lines.Count > MaxRecentLines) lines.RemoveAt(0);
        }

        internal static string BuildHeardContext(IList<NemesisConversationLine> lines, string newestPlayerMessage, int maxChars)
        {
            int cap = Math.Max(40, maxChars);
            StringBuilder sb = new StringBuilder(cap + 32);
            string newest = Compact(newestPlayerMessage, 100);
            if (newest.Length > 0) sb.Append("PLAYER MESSAGE (HEARD): ").Append(newest);

            if (lines != null && lines.Count > 0)
            {
                StringBuilder recent = new StringBuilder();
                int start = Math.Max(0, lines.Count - 4);
                for (int i = start; i < lines.Count; i++)
                {
                    NemesisConversationLine line = lines[i];
                    if (line == null || string.IsNullOrWhiteSpace(line.Text)) continue;
                    if (recent.Length > 0) recent.Append(" | ");
                    recent.Append(line.FromPlayer ? "P: " : "N: ").Append(Compact(line.Text, 70));
                }
                if (recent.Length > 0)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append("RECENT HEARD CHAT: ").Append(recent);
                }
            }

            string value = sb.ToString();
            return value.Length <= cap ? value : value.Substring(0, cap).TrimEnd();
        }

        internal static string Compact(string value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            while (clean.Contains("  ")) clean = clean.Replace("  ", " ");
            int cap = Math.Max(1, maxChars);
            return clean.Length <= cap ? clean : clean.Substring(0, cap).TrimEnd();
        }
    }
}
