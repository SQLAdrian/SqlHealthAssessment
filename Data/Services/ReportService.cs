/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SQLTriage.Data.Models;
using SQLTriage.Data.Services;

namespace SQLTriage.Data.Services
{
    /// <summary>
    /// Generates governance assessment PDF reports using QuestPDF.
    /// </summary>
    public interface IReportService
    {
        Task<byte[]> GenerateGovernanceReportAsync(
            string serverName,
            IEnumerable<CheckResult> checkResults,
            CancellationToken cancellationToken = default);
    }

    public sealed class ReportService : IReportService
    {
        private readonly IGovernanceService _governance;
        private readonly IFindingTranslator _translator;
        private readonly IErrorCatalog _errorCatalog;
        private readonly ILogger<ReportService> _logger;

        public ReportService(
            IGovernanceService governance,
            IFindingTranslator translator,
            IErrorCatalog errorCatalog,
            ILogger<ReportService> logger)
        {
            _governance = governance ?? throw new ArgumentNullException(nameof(governance));
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
            _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<byte[]> GenerateGovernanceReportAsync(
            string serverName,
            IEnumerable<CheckResult> checkResults,
            CancellationToken cancellationToken = default)
        {
            var results = checkResults?.ToList() ?? new List<CheckResult>();
            var score = await _governance.ComputeFullAsync(results, cancellationToken).ConfigureAwait(false);

            // Translate top 10 failed findings
            var failedFindings = results.Where(r => !r.Passed).Take(10).ToList();
            var translations = new List<FindingTranslation>();
            foreach (var f in failedFindings)
            {
                try
                {
                    translations.Add(await _translator.TranslateAsync(f, cancellationToken).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Translation failed for {CheckId}", f.CheckId);
                }
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Inch);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Inter"));

                    // ── Page 1: Cover + Executive Snapshot ──
                    page.Content().Column(col =>
                    {
                        // Header
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("SQLTriage Governance Report")
                                .FontSize(24).Bold().FontColor(Colors.Teal.Medium);
                            row.ConstantItem(120).AlignRight().Text(DateTime.Now.ToString("yyyy-MM-dd"))
                                .FontSize(10).FontColor(Colors.Grey.Medium);
                        });

                        col.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Server + Score
                        col.Item().Text($"Server: {serverName}").FontSize(14).Bold();
                        col.Item().PaddingVertical(4);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(scoreCol =>
                            {
                                scoreCol.Item().Text($"Overall Score: {score.Overall:F1} / 100")
                                    .FontSize(28).Bold()
                                    .FontColor(BandColor(score.Band));
                                scoreCol.Item().Text($"Maturity Band: {score.Band}")
                                    .FontSize(14).FontColor(Colors.Grey.Darken2);
                                scoreCol.Item().Text($"Findings: {score.PassedFindings} passed / {score.FailedFindings} failed")
                                    .FontSize(11).FontColor(Colors.Grey.Medium);
                            });

                            // Mini gauge badge
                            row.ConstantItem(100).Height(60).Background(BandColorHex(score.Band))
                                .AlignCenter().AlignMiddle()
                                .Text(score.Band.ToString())
                                .FontSize(14).Bold().FontColor(Colors.White);
                        });

                        col.Item().PaddingVertical(12);

                        // Top 3 risks (executive summary)
                        col.Item().Text("Top 3 Risks").FontSize(14).Bold();
                        foreach (var t in translations.Take(3))
                        {
                            col.Item().PaddingVertical(4).Column(risk =>
                            {
                                risk.Item().Text(t.Executive.BusinessRisk).FontSize(11).Bold();
                                risk.Item().Text(t.Executive.PlainLanguageSummary).FontSize(9).FontColor(Colors.Grey.Darken1);
                                risk.Item().Text($"Estimated monthly cost: {t.Executive.EstimatedMonthlyCost}").FontSize(9).Italic();
                            });
                        }

                        col.Item().PaddingVertical(12);
                        col.Item().Text("This report was generated by SQLTriage v1.0.0")
                            .FontSize(8).FontColor(Colors.Grey.Medium).AlignCenter();
                    });
                });

                // ── Page 2: Risk Register ──
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Inch);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Inter"));

                    page.Content().Column(col =>
                    {
                        col.Item().Text("Risk Register").FontSize(18).Bold().FontColor(Colors.Teal.Medium);
                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Table header
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(60);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(70);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Check ID").Bold();
                                header.Cell().Text("Category").Bold();
                                header.Cell().Text("DBA Remediation").Bold();
                                header.Cell().Text("Severity").Bold();
                            });

                            foreach (var t in translations)
                            {
                                table.Cell().Text(t.CheckId).FontSize(8);
                                table.Cell().Text(t.ItManager.BusinessCategory).FontSize(8);
                                table.Cell().Text(t.Dba.TSqlRemediation).FontSize(8);
                                table.Cell().Text(t.ItManager.SlaImpact).FontSize(8);
                            }
                        });
                    });
                });

                // ── Page 3: Action Plan ──
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Inch);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Inter"));

                    page.Content().Column(col =>
                    {
                        col.Item().Text("Action Plan").FontSize(18).Bold().FontColor(Colors.Teal.Medium);
                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Prioritized by severity
                        var prioritized = translations
                            .OrderByDescending(t => SeverityRank(t.ItManager.SlaImpact))
                            .ToList();

                        int rank = 1;
                        foreach (var t in prioritized)
                        {
                            col.Item().PaddingVertical(4).Column(action =>
                            {
                                action.Item().Row(row =>
                                {
                                    row.ConstantItem(30).Text($"#{rank}").FontSize(12).Bold();
                                    row.RelativeItem().Text(t.Dba.Title).FontSize(11).Bold();
                                });
                                action.Item().Text(t.Executive.RecommendedAction).FontSize(9);
                                action.Item().Text($"Effort: {t.ItManager.RemediationEffort} | Change control: {(t.ItManager.RequiresChangeControl ? "Yes" : "No")}")
                                    .FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                            rank++;
                        }

                        // Cost-of-downtime summary
                        col.Item().PaddingVertical(12);
                        col.Item().Text("Cost-of-Downtime Summary").FontSize(14).Bold();
                        col.Item().PaddingVertical(4);

                        var totalCost = translations
                            .Select(t => ParseCost(t.Executive.EstimatedMonthlyCost))
                            .Where(c => c > 0)
                            .Sum();

                        col.Item().Text($"Aggregate estimated monthly impact of unresolved findings: {totalCost:C0}")
                            .FontSize(11).Bold().FontColor(Colors.Red.Medium);

                        col.Item().PaddingVertical(8);
                        col.Item().Text("This figure is derived from per-finding executive estimates and should be reviewed with your finance team.")
                            .FontSize(8).FontColor(Colors.Grey.Medium).Italic();
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static string BandColor(ScoreBand band) => band switch
        {
            ScoreBand.Emerging => Colors.Red.Medium,
            ScoreBand.Bronze => Colors.Orange.Medium,
            ScoreBand.Silver => Colors.Blue.Medium,
            ScoreBand.Gold => Colors.Teal.Medium,
            ScoreBand.Platinum => Colors.Green.Medium,
            _ => Colors.Grey.Medium
        };

        private static string BandColorHex(ScoreBand band) => band switch
        {
            ScoreBand.Emerging => "#EF4444",
            ScoreBand.Bronze => "#F97316",
            ScoreBand.Silver => "#3B82F6",
            ScoreBand.Gold => "#14B8A6",
            ScoreBand.Platinum => "#10B981",
            _ => "#9CA3AF"
        };

        private static int SeverityRank(string severity)
        {
            var s = severity?.ToLowerInvariant() ?? "";
            if (s.Contains("critical") || s.Contains("high")) return 3;
            if (s.Contains("medium") || s.Contains("warn")) return 2;
            return 1;
        }

        private static double ParseCost(string costText)
        {
            if (string.IsNullOrWhiteSpace(costText)) return 0;
            // Extract first number from strings like "$5,000/month" or "~$1200"
            var digits = new System.Text.StringBuilder();
            bool foundDigit = false;
            foreach (var c in costText)
            {
                if (char.IsDigit(c) || (foundDigit && c == ',') || (foundDigit && c == '.'))
                {
                    if (char.IsDigit(c)) foundDigit = true;
                    if (c != ',') digits.Append(c);
                }
                else if (foundDigit) break;
            }
            if (double.TryParse(digits.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val))
                return val;
            return 0;
        }
    }
}
