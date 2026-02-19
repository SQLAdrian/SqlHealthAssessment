using System.Text.Json.Serialization;

namespace SqlHealthAssessment.Data.Models
{
    public class DashboardConfigRoot
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("dashboards")]
        public List<DashboardDefinition> Dashboards { get; set; } = new();

        [JsonPropertyName("supportQueries")]
        public Dictionary<string, QueryPair> SupportQueries { get; set; } = new();
    }

    public class DashboardDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("route")]
        public string Route { get; set; } = "";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("showAllOption")]
        public bool ShowAllOption { get; set; } = false;

        [JsonPropertyName("panels")]
        public List<PanelDefinition> Panels { get; set; } = new();
    }

    public class PanelDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        /// <summary>Description shown below chart titles, or as tooltip on stat cards</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>TimeSeries, StatCard, BarGauge, DataGrid, CheckStatus, TextCard</summary>
        [JsonPropertyName("panelType")]
        public string PanelType { get; set; } = "TimeSeries";

        /// <summary>Line, Bar, Area (only for TimeSeries)</summary>
        [JsonPropertyName("chartType")]
        public string ChartType { get; set; } = "Line";

        [JsonPropertyName("height")]
        public int Height { get; set; } = 250;

        [JsonPropertyName("refreshIntervalSeconds")]
        public int? RefreshIntervalSeconds { get; set; }

        [JsonPropertyName("layout")]
        public PanelLayout Layout { get; set; } = new();

        [JsonPropertyName("query")]
        public QueryPair Query { get; set; } = new();

        // StatCard-specific
        [JsonPropertyName("statUnit")]
        public string StatUnit { get; set; } = "";

        [JsonPropertyName("statThresholdKey")]
        public string? StatThresholdKey { get; set; }

        // BarGauge-specific
        [JsonPropertyName("barGaugeThresholdKey")]
        public string? BarGaugeThresholdKey { get; set; }

        [JsonPropertyName("barGaugeUnitSuffix")]
        public string BarGaugeUnitSuffix { get; set; } = "%";

        // Value formatting
        /// <summary>Format for displaying values: N0 (integer), N2 (2 decimals), P0 (percent), 0.## GB, etc.</summary>
        [JsonPropertyName("valueFormat")]
        public string ValueFormat { get; set; } = "";

        // Conditional color formatting
        /// <summary>
        /// Color threshold rules evaluated in order. Last matching rule wins.
        /// If empty, falls back to StatThresholdKey or default blue.
        /// </summary>
        [JsonPropertyName("colorThresholds")]
        public List<ColorThresholdRule> ColorThresholds { get; set; } = new();

        // DataGrid-specific
        [JsonPropertyName("dataGridIsClickable")]
        public bool DataGridIsClickable { get; set; } = false;

        [JsonPropertyName("dataGridMaxRows")]
        public int DataGridMaxRows { get; set; } = 500;

        /// <summary>Evaluates colorThresholds rules in order, returns last matching color. Falls back to default.</summary>
        public string GetThresholdColor(double value, string defaultColor = "#2196f3")
        {
            if (ColorThresholds == null || ColorThresholds.Count == 0)
                return defaultColor;

            string color = defaultColor;
            foreach (var rule in ColorThresholds)
            {
                if (rule.Matches(value))
                    color = rule.Color;
            }
            return color;
        }

        /// <summary>Formats a value according to the ValueFormat string.</summary>
        public string FormatValue(object? value)
        {
            if (value == null || string.IsNullOrEmpty(ValueFormat))
                return value?.ToString() ?? "";

            try
            {
                if (value is IFormattable formattable)
                {
                    return formattable.ToString(ValueFormat, System.Globalization.CultureInfo.InvariantCulture);
                }
                return value.ToString() ?? "";
            }
            catch
            {
                return value.ToString() ?? "";
            }
        }
    }

    public class PanelLayout
    {
        /// <summary>0 = special section (stats/gauges), 1 = left column, 2 = right column</summary>
        [JsonPropertyName("column")]
        public int Column { get; set; } = 1;

        [JsonPropertyName("order")]
        public int Order { get; set; } = 0;

        [JsonPropertyName("spanColumns")]
        public bool SpanColumns { get; set; } = false;
    }

    /// <summary>
    /// A single threshold rule: if value meets the operator + threshold, apply this color.
    /// Rules are evaluated in order; the last matching rule wins.
    /// </summary>
    public class ColorThresholdRule
    {
        /// <summary>Comparison operator: >=, >, <=, <, ==</summary>
        [JsonPropertyName("operator")]
        public string Operator { get; set; } = ">=";

        [JsonPropertyName("value")]
        public double Value { get; set; }

        /// <summary>Hex color e.g. #4caf50</summary>
        [JsonPropertyName("color")]
        public string Color { get; set; } = "#2196f3";

        /// <summary>Optional label for the editor, e.g. "Warning", "Critical"</summary>
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        public bool Matches(double actual)
        {
            return Operator switch
            {
                ">=" => actual >= Value,
                ">"  => actual > Value,
                "<=" => actual <= Value,
                "<"  => actual < Value,
                "==" => Math.Abs(actual - Value) < 0.0001,
                _    => actual >= Value
            };
        }
    }

    public class QueryPair
    {
        [JsonPropertyName("sqlServer")]
        public string SqlServer { get; set; } = "";

        [JsonPropertyName("sqlite")]
        public string Sqlite { get; set; } = "";
    }
}
