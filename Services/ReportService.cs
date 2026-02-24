/* In the name of God, the Merciful, the Compassionate */

using System.Data;
using System.IO;
using System.Text;
using Microsoft.Reporting.NETCore;

namespace SqlHealthAssessment.Services;

/// <summary>
/// Renders the sp_Blitz .rdl report against a DataTable and returns a
/// self-contained base64 data-URL for embedding in an &lt;iframe&gt;.
/// Falls back to a plain themed HTML table when the report file is absent
/// or the renderer encounters an error.
/// </summary>
public class ReportService
{
    private const string DataSetName = "DataSet1";

    private static readonly string[] CandidatePaths =
    [
        Path.Combine(AppContext.BaseDirectory, "reports", "BlitzReport.rdlc"),
        Path.Combine(AppContext.BaseDirectory, "reports", "BlitzReport.rdl"),
        Path.Combine(AppContext.BaseDirectory, "BlitzReport.rdlc"),
        Path.Combine(AppContext.BaseDirectory, "BlitzReport.rdl"),
    ];

    /// <summary>
    /// Renders the report as a base64-encoded HTML data-URL.
    /// Returns null when no report file is found (caller should hide the iframe).
    /// </summary>
    public string? RenderAsDataUrl(IEnumerable<BlitzRow> rows)
    {
        var reportPath = CandidatePaths.FirstOrDefault(File.Exists);
        if (reportPath == null)
            return null;

        var dt = BuildDataTable(rows);

        try
        {
            using var report = new LocalReport();
            report.ReportPath = reportPath;
            report.DataSources.Add(new ReportDataSource(DataSetName, dt));
            var bytes = report.Render("HTML4.0");
            return "data:text/html;base64," + Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            // Walk the exception chain so inner messages (e.g. from the RDLC renderer) are visible.
            var sb = new StringBuilder();
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (sb.Length > 0) sb.Append("\n→ ");
                sb.Append(e.Message);
            }

            // Surface the full error inside the iframe rather than crashing the page
            var errorHtml = $"""
                <html><body style="font-family:Segoe UI,sans-serif;padding:20px;background:#1a1a2e;color:#ccc">
                <h3 style="color:#f44336">Report render error</h3>
                <pre style="background:#0d0d1a;padding:14px;border-radius:6px;overflow:auto;white-space:pre-wrap">{System.Net.WebUtility.HtmlEncode(sb.ToString())}</pre>
                <p style="color:#888">Report file: <code>{System.Net.WebUtility.HtmlEncode(reportPath)}</code></p>
                <p style="color:#888">DataSet name in .rdl must be <code>{DataSetName}</code>.</p>
                </body></html>
                """;
            return "data:text/html;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(errorHtml));
        }
    }

    private static DataTable BuildDataTable(IEnumerable<BlitzRow> rows)
    {
        var dt = new DataTable();
        dt.Columns.Add("CheckID",  typeof(int));
        dt.Columns.Add("Category", typeof(string));
        dt.Columns.Add("Finding",  typeof(string));
        dt.Columns.Add("Findings", typeof(string));
        dt.Columns.Add("Priority", typeof(string));
        dt.Columns.Add("URL",      typeof(string));

        foreach (var r in rows)
            dt.Rows.Add(r.CheckID, r.Category, r.Finding, r.Findings, r.Priority, r.URL);

        return dt;
    }

    public record BlitzRow(int CheckID, string Category, string Finding, string Findings, string Priority, string URL);
}
