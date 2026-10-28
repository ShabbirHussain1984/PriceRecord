using System.Text;
using System.Text.RegularExpressions;

namespace PriceRecord.Services;

/// <summary>
/// Minimal robots.txt evaluator (RFC 9309): picks the group for our product token, falling back to "*",
/// supports "*" and "$" wildcards, and resolves conflicts by longest match with Allow winning ties.
/// </summary>
public sealed class RobotsTxtPolicy
{
    private readonly List<(bool Allow, string Pattern, Regex Regex)> _rules;

    public static RobotsTxtPolicy AllowAll { get; } = new(new());

    private RobotsTxtPolicy(List<(bool, string, Regex)> rules) => _rules = rules;

    public int RuleCount => _rules.Count;

    /// <param name="content">robots.txt body.</param>
    /// <param name="userAgent">Full User-Agent string; any product token in it (e.g. "PriceRecordBot") is matched against group names.</param>
    public static RobotsTxtPolicy Parse(string content, string userAgent)
    {
        var groups = new List<(List<string> Agents, List<(bool, string)> Rules)>();
        List<string>? currentAgents = null;
        List<(bool, string)>? currentRules = null;
        var lastWasAgent = false;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine;
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            line = line.Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var field = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            if (field == "user-agent")
            {
                if (!lastWasAgent || currentAgents is null)
                {
                    currentAgents = new List<string>();
                    currentRules = new List<(bool, string)>();
                    groups.Add((currentAgents, currentRules));
                }
                currentAgents.Add(value.ToLowerInvariant());
                lastWasAgent = true;
            }
            else if (field is "allow" or "disallow")
            {
                lastWasAgent = false;
                if (currentRules is null || value.Length == 0) continue; // empty Disallow = allow everything
                currentRules.Add((field == "allow", value));
            }
            else
            {
                lastWasAgent = false;
            }
        }

        var token = ProductToken(userAgent);
        var specific = groups.Where(g => g.Agents.Any(a => a != "*" && a == token)).ToList();
        var chosen = specific.Count > 0 ? specific : groups.Where(g => g.Agents.Contains("*")).ToList();

        var rules = chosen
            .SelectMany(g => g.Rules)
            .Select(r => (r.Item1, r.Item2, ToRegex(r.Item2)))
            .ToList();
        return new RobotsTxtPolicy(rules);
    }

    /// <summary>Checks a path plus query, e.g. "/mobiles?page=2".</summary>
    public bool IsAllowed(string pathAndQuery)
    {
        if (string.IsNullOrEmpty(pathAndQuery)) pathAndQuery = "/";
        var bestLength = -1;
        var allowed = true;
        foreach (var (allow, pattern, regex) in _rules)
        {
            if (!regex.IsMatch(pathAndQuery)) continue;
            var length = pattern.Length;
            if (length > bestLength || (length == bestLength && allow))
            {
                bestLength = length;
                allowed = allow;
            }
        }
        return allowed;
    }

    public bool IsAllowed(Uri uri) => IsAllowed(uri.PathAndQuery);

    /// <summary>
    /// The crawler's product token: the last "Name/version" in the User-Agent
    /// ("... Safari/537.36 PriceRecordBot/1.0" → "pricerecordbot"), or the whole string when there is none.
    /// </summary>
    public static string ProductToken(string userAgent)
    {
        var matches = Regex.Matches(userAgent, @"([A-Za-z][\w-]*)/");
        return (matches.Count > 0 ? matches[^1].Groups[1].Value : userAgent.Trim()).ToLowerInvariant();
    }

    private static Regex ToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        // Patterns that don't start with "/" or "*" (e.g. "*?" is fine, "foo" is not standard) are treated as prefixes from root.
        if (!pattern.StartsWith('/') && !pattern.StartsWith('*')) sb.Append('/');
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch == '*') sb.Append(".*");
            else if (ch == '$' && i == pattern.Length - 1) sb.Append('$');
            else sb.Append(Regex.Escape(ch.ToString()));
        }
        return new Regex(sb.ToString(), RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
