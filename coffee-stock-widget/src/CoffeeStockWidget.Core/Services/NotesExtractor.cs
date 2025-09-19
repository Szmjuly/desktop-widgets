using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace CoffeeStockWidget.Core.Services;

public static class NotesExtractor
{
    public static string? ExtractNotesFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        static string Clean(string s)
        {
            try
            {
                s = Regex.Replace(s, "<script[\\s\\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
                s = Regex.Replace(s, "<style[\\s\\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
                s = System.Net.WebUtility.HtmlDecode(s);
                s = Regex.Replace(s, "<[^>]+>", " ");
                s = Regex.Replace(s, "\\s+", " ").Trim();
                if (s.Length > 300) s = s.Substring(0, 300);
                return s;
            }
            catch { return s; }
        }

        static string TrimLen(string s) => s.Length > 300 ? s.Substring(0, 300) : s;

        // Black & White: HOW IT TASTES disclosure content
        try
        {
            var rxBw = new Regex(
                @"<summary[^>]*>[\\s\\S]*?HOW\\s*IT\\s*TASTES[\\s\\S]*?</summary>\\s*<div[^>]*class\\s*=\\s*[""'][^""']*disclosure__panel[^""']*[ ""'][^>]*>\\s*<div[^>]*class\\s*=\\s*[""'][^""']*disclosure__content[^""']*[ ""'][^>]*>([\\s\\S]*?)</div>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var mBw = rxBw.Match(html);
            if (mBw.Success)
            {
                var content = Clean(mBw.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(content)) return content;
            }
        }
        catch { }

        // Brandywine: We Taste:
        try
        {
            var rxWeTaste = new Regex("<p[^>]*>\\s*We\\s*Taste\\s*:\\s*([\\s\\S]*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var mWt = rxWeTaste.Match(html);
            if (mWt.Success)
            {
                var content = Clean(mWt.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(content)) return content;
            }
        }
        catch { }

        // Meta description fallbacks
        static string? ExtractMeta(string html, string name)
        {
            try
            {
                var pattern = $"<meta[^>]+(?:name|property)=[\"']{name}[\"'][^>]*content=[\"'](.*?)[\"']";
                var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (m.Success) return System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            }
            catch { }
            return null;
        }
        var meta = ExtractMeta(html, "og:description") ?? ExtractMeta(html, "twitter:description") ?? ExtractMeta(html, "description");
        if (!string.IsNullOrWhiteSpace(meta))
        {
            var lm = meta!.ToLowerInvariant();
            if (lm.Contains("notes") || lm.Contains("tasting") || lm.Contains("flavor") || lm.Contains("flavour") || lm.Contains("we taste") || lm.Contains("how it tastes"))
            {
                return TrimLen(meta);
            }
        }

        // 3b) Meta image alt (some sites place tasting notes in og:image:alt or twitter:image:alt)
        try
        {
            var metaAlt = ExtractMeta(html, "og:image:alt") ?? ExtractMeta(html, "twitter:image:alt");
            if (!string.IsNullOrWhiteSpace(metaAlt))
            {
                var alt = metaAlt!.Trim();
                var lower = alt.ToLowerInvariant();
                int keyIdx = lower.IndexOf("flavor notes");
                if (keyIdx < 0) keyIdx = lower.IndexOf("notes of");
                if (keyIdx >= 0)
                {
                    int start = keyIdx;
                    var ofIdx = lower.IndexOf(" of ", keyIdx);
                    if (ofIdx >= 0) start = ofIdx + 4; else start = keyIdx + (lower.IndexOf("flavor notes", keyIdx) == keyIdx ? "flavor notes".Length : "notes of".Length);
                    if (start < alt.Length)
                    {
                        int end = alt.IndexOf(';', start);
                        if (end < 0) end = alt.IndexOf('.', start);
                        if (end < 0) end = alt.Length;
                        var slice = alt.Substring(start, end - start).Trim().Trim('"');
                        slice = Regex.Replace(slice, "\\s+and\\s+", ", ", RegexOptions.IgnoreCase);
                        slice = Regex.Replace(slice, "\\s+", " ");
                        if (!string.IsNullOrWhiteSpace(slice)) return TrimLen(slice);
                    }
                }
            }
        }
        catch { }

        // 4) Image alt attributes containing flavor notes (e.g., tasting card alt text)
        try
        {
            var imgRx = new Regex("<img[^>]+alt\\s*=\\s*[\"']([^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in imgRx.Matches(html))
            {
                var alt = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                if (string.IsNullOrWhiteSpace(alt)) continue;
                var lower = alt.ToLowerInvariant();
                // Look for patterns like "flavor notes of X" or "notes of X"
                int keyIdx = lower.IndexOf("flavor notes");
                if (keyIdx < 0) keyIdx = lower.IndexOf("notes of");
                if (keyIdx >= 0)
                {
                    int start = keyIdx;
                    // normalize to part after "of"
                    var ofIdx = lower.IndexOf(" of ", keyIdx);
                    if (ofIdx >= 0) start = ofIdx + 4; else start = keyIdx + (lower.IndexOf("flavor notes", keyIdx) == keyIdx ? "flavor notes".Length : "notes of".Length);
                    if (start < alt.Length)
                    {
                        // end at semicolon or period if present
                        int end = alt.IndexOf(';', start);
                        if (end < 0) end = alt.IndexOf('.', start);
                        if (end < 0) end = alt.Length;
                        var slice = alt.Substring(start, end - start).Trim().Trim('"');
                        // Basic cleanup: replace double spaces, ensure commas for ' and '
                        slice = Regex.Replace(slice, "\\s+and\\s+", ", ", RegexOptions.IgnoreCase);
                        slice = Regex.Replace(slice, "\\s+", " ");
                        if (!string.IsNullOrWhiteSpace(slice)) return TrimLen(slice);
                    }
                }
            }
        }
        catch { }

        // 5) Black & White: "TAKE A SIP" paragraph inside description
        try
        {
            var rxSip = new Regex("<p[^>]*>[\\s\\S]*?TAKE\\s*A\\s*SIP[\\s\\S]*?</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var mSip = rxSip.Match(html);
            if (mSip.Success)
            {
                var rawPara = mSip.Groups[0].Value;
                var para = Clean(rawPara);
                if (!string.IsNullOrWhiteSpace(para))
                {
                    var lower = para.ToLowerInvariant();
                    var idx = lower.IndexOf("take a sip");
                    if (idx >= 0)
                    {
                        int start = para.IndexOf('|', idx);
                        if (start < 0) start = para.IndexOf(':', idx);
                        if (start >= 0 && start + 1 < para.Length) para = para.Substring(start + 1).Trim();
                        else para = para.Substring(idx + "take a sip".Length).Trim();
                    }
                    // Heuristic: pull explicit flavor mentions like "like jasmine", "like yuzu", "wildflower honey", "reminiscent of hops"
                    var rxTokens = new Regex(@"(?:like|reminiscent\s+of|notes?\s+of)\s+([^,.;\n\r]+)", RegexOptions.IgnoreCase);
                    var toks = rxTokens.Matches(para)
                        .Select(m => m.Groups[1].Value.Trim())
                        .Select(t => System.Text.RegularExpressions.Regex.Replace(t, @"^(a|an|the)\s+", string.Empty, RegexOptions.IgnoreCase))
                        .Select(t => System.Text.RegularExpressions.Regex.Replace(t, @"^(jar\s+of\s+)", string.Empty, RegexOptions.IgnoreCase))
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (toks.Count >= 2)
                    {
                        return TrimLen(string.Join(", ", toks));
                    }
                    // If tokens not found, return the descriptive sentence as a fallback
                    if (!string.IsNullOrWhiteSpace(para)) return TrimLen(para);
                }
            }
        }
        catch { }

        // Plain-text fallback with markers
        string text;
        try
        {
            text = Regex.Replace(html, "<script[\\s\\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<style[\\s\\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]+>", "\n");
            text = System.Net.WebUtility.HtmlDecode(text);
        }
        catch { text = html; }

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0 && s.Length < 400)
                        .ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var l = line.ToLowerInvariant();

            if (l.Contains("we taste"))
            {
                var idx = line.IndexOf(':');
                if (idx >= 0 && idx + 1 < line.Length)
                {
                    var val = line.Substring(idx + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(val)) return TrimLen(val);
                }
                var next = lines.Skip(i + 1).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                if (!string.IsNullOrWhiteSpace(next)) return TrimLen(next);
                return TrimLen(line);
            }

            if (l.Contains("how it tastes"))
            {
                var acc = new System.Text.StringBuilder();
                for (int j = i + 1; j < lines.Count; j++)
                {
                    var lj = lines[j];
                    if (string.IsNullOrWhiteSpace(lj)) break;
                    var low = lj.ToLowerInvariant();
                    if (low.StartsWith("coffee info") || low.StartsWith("process") || low.StartsWith("variety") || low.StartsWith("farm") || low.StartsWith("origin") || low.StartsWith("location") || low.StartsWith("altitude") || low.StartsWith("about"))
                        break;
                    if (acc.Length > 0) acc.Append(' ');
                    acc.Append(lj);
                    if (acc.Length > 300) break;
                }
                var result = acc.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(result)) return TrimLen(result);
            }

            if (l.Contains("tasting notes") || l.StartsWith("notes:") || l.Contains("flavor notes") || l.Contains("flavour notes"))
            {
                var idx = line.IndexOf(':');
                if (idx >= 0 && idx + 1 < line.Length)
                {
                    var val = line.Substring(idx + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(val)) return TrimLen(val);
                }
                return TrimLen(line);
            }
        }

        return null;
    }
}
