using System.Collections.Generic;

namespace SqlHealthAssessment.Data
{
    public class QueryDefinition
    {
        public string SqlServer { get; set; } = "";
        public string Sqlite { get; set; } = "";
    }

    public class QueryStore
    {
        private readonly DashboardConfigService _configService;

        public QueryStore(DashboardConfigService configService)
        {
            _configService = configService;
        }

        /// <summary>
        /// Retrieves the SQL query text for the given query ID and data source type.
        /// Delegates to DashboardConfigService which reads from dashboard-config.json.
        /// </summary>
        public string GetQuery(string queryId, string dataSourceType)
        {
            return _configService.GetQuery(queryId, dataSourceType);
        }

        /// <summary>
        /// Checks whether a query ID exists in the config.
        /// </summary>
        public bool HasQuery(string queryId) => _configService.HasQuery(queryId);
    }
}
