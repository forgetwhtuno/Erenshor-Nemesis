using System;

namespace ErenshorNemesis
{
    internal static class NemesisHubPresentation
    {
        internal const int MaxStatusLength = 180;

        internal static string Build(bool enabled, bool hasNemesis, string name, int grudge, string record, bool pending, int candidateCount)
        {
            string status;
            if (!enabled) status = "Disabled";
            else if (!hasNemesis) status = "No rival | " + Math.Max(0, candidateCount) + " candidate(s)";
            else
            {
                status = "Rival: " + Clean(name, 42) + " | grudge " + Math.Max(0, grudge);
                string compactRecord = Clean(record, 48);
                if (compactRecord.Length > 0) status += " | " + compactRecord;
                if (pending) status += " | confirmation pending";
            }
            return status.Length <= MaxStatusLength ? status : status.Substring(0, MaxStatusLength);
        }

        private static string Clean(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string s = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
