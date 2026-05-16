/* In the name of God, the Merciful, the Compassionate */

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SQLTriage.Data.Services;

// BM:ComplianceMappingService.Class — loads control_mappings.json and provides framework/control lookup
/// <summary>
/// Loads Config/control_mappings.json at construction and exposes read-only lookup APIs.
/// The mapping file is keyed by region → frameworks → categories, where each category
/// carries a sqlCheckHints array (e.g. "access_control", "audit_logging").
/// At construction we build a reverse lookup: sqlCheckHint → list of (framework, controlId, controlName).
/// VA results carry a Category ("Security", "Configuration", etc.) which we map to sqlCheckHints
/// via the static CategoryToHints dictionary.
/// </summary>
public sealed class ComplianceMappingService
{
    private readonly ILogger<ComplianceMappingService> _logger;

    // All loaded framework definitions
    private readonly List<FrameworkDefinition> _frameworks = new();

    // Reverse map: sqlCheckHint → controls that reference it
    private readonly Dictionary<string, List<ControlRef>> _hintToControls = new(StringComparer.OrdinalIgnoreCase);

    // ── VA Category → sqlCheckHint vocabulary ──────────────────────────────
    // Bridges AssessmentResult.Category (produced by SqlAssessmentService) to the
    // sqlCheckHints vocabulary used in control_mappings.json.
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CategoryToHints =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Security"]        = new[] { "access_control", "identity_authentication", "monitoring_detection" },
            ["Configuration"]   = new[] { "configuration_hardening", "change_management" },
            ["Performance"]     = new[] { "configuration_hardening" },
            ["Availability"]    = new[] { "backup_recovery" },
            ["BestPractices"]   = new[] { "configuration_hardening" },
            ["Information"]     = new[] { "configuration_hardening" },
            ["Encryption"]      = new[] { "cryptography_encryption", "key_management" },
            ["Auditing"]        = new[] { "audit_logging", "monitoring_detection" },
            ["Patching"]        = new[] { "patch_vulnerability_management" },
            ["Network"]         = new[] { "network_security" },
            ["General"]         = new[] { "configuration_hardening" },
        };

    public ComplianceMappingService(ILogger<ComplianceMappingService> logger)
    {
        _logger = logger;
        Load();
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Returns distinct framework acronyms loaded from control_mappings.json.</summary>
    public IReadOnlyList<string> GetFrameworks()
        => _frameworks.Select(f => f.Acronym).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a).ToList();

    /// <summary>Returns all framework definitions (includes region metadata).</summary>
    public IReadOnlyList<FrameworkDefinition> GetFrameworkDefinitions() => _frameworks.AsReadOnly();

    /// <summary>Returns distinct control IDs within a framework (matched by acronym).</summary>
    public IReadOnlyList<string> GetControlsForFramework(string framework)
    {
        var fw = FindFramework(framework);
        return fw?.Categories.Select(c => c.Id).ToList() ?? new List<string>();
    }

    /// <summary>
    /// Returns the VA category strings (e.g. "Security", "Configuration") whose sqlCheckHints
    /// overlap with the given framework control's sqlCheckHints.
    /// Used by VA page to show "Compliance:" hints per finding.
    /// </summary>
    public IReadOnlyList<string> GetVaCategoriesForControl(string framework, string controlId)
    {
        var fw = FindFramework(framework);
        var cat = fw?.Categories.FirstOrDefault(c => string.Equals(c.Id, controlId, StringComparison.OrdinalIgnoreCase));
        if (cat == null) return Array.Empty<string>();

        var result = new List<string>();
        foreach (var kv in CategoryToHints)
        {
            if (kv.Value.Any(h => cat.SqlCheckHints.Contains(h, StringComparer.OrdinalIgnoreCase)))
                result.Add(kv.Key);
        }
        return result;
    }

    /// <summary>
    /// Returns all (framework, controlId, controlName) tuples that apply to a given VA category.
    /// Used by VA page to show the "Compliance:" line per finding row.
    /// </summary>
    public IReadOnlyList<ControlRef> GetControlsForVaCategory(string vaCategory)
    {
        if (!CategoryToHints.TryGetValue(vaCategory, out var hints))
            return Array.Empty<ControlRef>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ControlRef>();
        foreach (var hint in hints)
        {
            if (!_hintToControls.TryGetValue(hint, out var refs)) continue;
            foreach (var r in refs)
            {
                var key = $"{r.Framework}|{r.ControlId}";
                if (seen.Add(key)) result.Add(r);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns all ControlRefs for a given framework, keyed by the sqlCheckHints they cover.
    /// Used by ComplianceScoreService to compute per-control-family scoring.
    /// </summary>
    public IReadOnlyList<FrameworkCategory> GetCategoriesForFramework(string framework)
    {
        var fw = FindFramework(framework);
        return fw?.Categories ?? new List<FrameworkCategory>();
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private FrameworkDefinition? FindFramework(string framework)
        => _frameworks.FirstOrDefault(f =>
            string.Equals(f.Acronym, framework, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Name, framework, StringComparison.OrdinalIgnoreCase));

    private void Load()
    {
        var baseDirs = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
        };

        string? jsonPath = null;
        foreach (var dir in baseDirs)
        {
            var candidate = Path.Combine(dir, "Config", "control_mappings.json");
            if (File.Exists(candidate)) { jsonPath = candidate; break; }
            candidate = Path.Combine(dir, "config", "control_mappings.json");
            if (File.Exists(candidate)) { jsonPath = candidate; break; }
        }

        if (jsonPath == null)
        {
            _logger.LogWarning("control_mappings.json not found — ComplianceMappingService will return empty results");
            return;
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Iterate regions → frameworks → categories
            foreach (var regionProp in root.EnumerateObject())
            {
                var regionName = regionProp.Name;
                if (regionName == "$schema") continue;

                if (!regionProp.Value.TryGetProperty("frameworks", out var fwArray)) continue;

                foreach (var fwEl in fwArray.EnumerateArray())
                {
                    var name    = fwEl.TryGetProperty("name",     out var n) ? n.GetString() ?? "" : "";
                    var acronym = fwEl.TryGetProperty("acronym",  out var a) ? a.GetString() ?? "" : name;
                    var url     = fwEl.TryGetProperty("url",      out var u) ? u.GetString() ?? "" : "";
                    var urlPending = fwEl.TryGetProperty("urlPending", out var up) && up.GetBoolean();

                    var categories = new List<FrameworkCategory>();
                    if (fwEl.TryGetProperty("categories", out var catArray))
                    {
                        foreach (var catEl in catArray.EnumerateArray())
                        {
                            var cid   = catEl.TryGetProperty("id",   out var ci) ? ci.GetString() ?? "" : "";
                            var cname = catEl.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                            var hints = new List<string>();
                            if (catEl.TryGetProperty("sqlCheckHints", out var hintsEl))
                                foreach (var h in hintsEl.EnumerateArray())
                                    if (h.GetString() is { } hs) hints.Add(hs);

                            categories.Add(new FrameworkCategory { Id = cid, Name = cname, SqlCheckHints = hints });
                        }
                    }

                    var fwDef = new FrameworkDefinition
                    {
                        Region     = regionName,
                        Name       = name,
                        Acronym    = acronym,
                        Url        = url,
                        UrlPending = urlPending,
                        Categories = categories,
                    };
                    _frameworks.Add(fwDef);

                    // Build reverse hint → ControlRef index
                    foreach (var cat in categories)
                    {
                        foreach (var hint in cat.SqlCheckHints)
                        {
                            if (!_hintToControls.TryGetValue(hint, out var list))
                            {
                                list = new List<ControlRef>();
                                _hintToControls[hint] = list;
                            }
                            list.Add(new ControlRef { Framework = acronym, ControlId = cat.Id, ControlName = cat.Name });
                        }
                    }
                }
            }

            _logger.LogInformation(
                "ComplianceMappingService loaded {FrameworkCount} frameworks across {RegionCount} regions from {Path}",
                _frameworks.Count,
                _frameworks.Select(f => f.Region).Distinct().Count(),
                jsonPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse control_mappings.json from {Path}", jsonPath);
        }
    }

    // ── Data models ─────────────────────────────────────────────────────────

    public sealed class FrameworkDefinition
    {
        public string Region     { get; init; } = "";
        public string Name       { get; init; } = "";
        public string Acronym    { get; init; } = "";
        public string Url        { get; init; } = "";
        public bool   UrlPending { get; init; }
        public List<FrameworkCategory> Categories { get; init; } = new();
    }

    public sealed class FrameworkCategory
    {
        public string       Id            { get; init; } = "";
        public string       Name          { get; init; } = "";
        public List<string> SqlCheckHints { get; init; } = new();
    }

    public sealed class ControlRef
    {
        public string Framework   { get; init; } = "";
        public string ControlId   { get; init; } = "";
        public string ControlName { get; init; } = "";
    }
}
