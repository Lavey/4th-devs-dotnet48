using System;
using System.Text.RegularExpressions;

namespace FourthDevs.Mcp.Files.Lib
{
    internal static class PatternMatcher
    {
        // Minimum acceptable score for in-order character fuzzy matches
        private const int MinFuzzyScoreOrdered = 30;
        // Minimum acceptable score for pure Levenshtein matches on short strings
        private const int MinFuzzyScoreLevenshtein = 60;
        /// <summary>
        /// Glob pattern matching. '*' matches anything except '/', '**' matches across directories.
        /// </summary>
        public static bool MatchGlob(string pattern, string path)
        {
            if (string.IsNullOrEmpty(pattern)) return false;

            // Normalize separators
            path = path.Replace('\\', '/');
            pattern = pattern.Replace('\\', '/');

            string regex = GlobToRegex(pattern);
            return Regex.IsMatch(path, regex, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Fuzzy matching: returns true if the query loosely matches the target.
        /// score is 0–100, higher is better.
        /// </summary>
        public static bool MatchFuzzy(string query, string target, out int score)
        {
            score = 0;
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target)) return false;

            query  = query.ToLowerInvariant();
            target = target.ToLowerInvariant();

            // Exact match
            if (target.Contains(query))
            {
                score = 100;
                return true;
            }

            // All chars of query appear in order in target
            int qi = 0;
            for (int ti = 0; ti < target.Length && qi < query.Length; ti++)
            {
                if (target[ti] == query[qi]) qi++;
            }
            if (qi == query.Length)
            {
                // Score by how "tight" the match is
                int dist = LevenshteinDistance(query, target);
                int maxLen = Math.Max(query.Length, target.Length);
                score = Math.Max(0, 100 - (dist * 100 / maxLen));
                return score >= MinFuzzyScoreOrdered;
            }

            // Levenshtein fallback for short strings
            if (query.Length <= 8 && target.Length <= 20)
            {
                int dist = LevenshteinDistance(query, target);
                int maxLen = Math.Max(query.Length, target.Length);
                score = Math.Max(0, 100 - (dist * 100 / maxLen));
                return score >= MinFuzzyScoreLevenshtein;
            }

            return false;
        }

        /// <summary>
        /// Compute the Levenshtein distance between two strings.
        /// </summary>
        public static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int m = s.Length;
            int n = t.Length;
            int[] prev = new int[n + 1];
            int[] curr = new int[n + 1];

            for (int j = 0; j <= n; j++) prev[j] = j;

            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= n; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }
                int[] tmp = prev; prev = curr; curr = tmp;
            }
            return prev[n];
        }

        private static string GlobToRegex(string pattern)
        {
            var sb = new System.Text.StringBuilder("^");
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    sb.Append(".*");
                    i++;
                    if (i + 1 < pattern.Length && pattern[i + 1] == '/')
                        i++;
                }
                else if (c == '*')
                {
                    sb.Append("[^/]*");
                }
                else if (c == '?')
                {
                    sb.Append("[^/]");
                }
                else if (c == '.' || c == '(' || c == ')' || c == '[' || c == ']' ||
                         c == '{' || c == '}' || c == '+' || c == '^' || c == '$' ||
                         c == '|' || c == '\\')
                {
                    sb.Append('\\');
                    sb.Append(c);
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append("$");
            return sb.ToString();
        }
    }
}
