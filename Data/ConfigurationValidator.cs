/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SqlHealthAssessment.Data
{
    public class ConfigurationValidator
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ConfigurationValidator> _logger;

        public ConfigurationValidator(IConfiguration config, ILogger<ConfigurationValidator> logger)
        {
            _config = config;
            _logger = logger;
        }

        public (bool IsValid, List<string> Errors) Validate()
        {
            var errors = new List<string>();

            // Connection String
            var connStr = _config.GetConnectionString("SqlServer");
            if (string.IsNullOrWhiteSpace(connStr))
                errors.Add("SqlServer connection string is missing");

            // Query Timeout
            var timeout = _config.GetValue<int>("QueryTimeoutSeconds", -1);
            if (timeout <= 0 || timeout > 300)
                errors.Add("QueryTimeoutSeconds must be between 1 and 300");

            // Max Query Rows
            var maxRows = _config.GetValue<int>("MaxQueryRows", -1);
            if (maxRows <= 0 || maxRows > 100000)
                errors.Add("MaxQueryRows must be between 1 and 100000");

            // Refresh Interval
            var refreshInterval = _config.GetValue<int>("RefreshIntervalSeconds", -1);
            if (refreshInterval < 1 || refreshInterval > 3600)
                errors.Add("RefreshIntervalSeconds must be between 1 and 3600");

            // Log Level
            var logLevel = _config["Logging:LogLevel:Default"];
            if (string.IsNullOrEmpty(logLevel))
                errors.Add("Logging:LogLevel:Default is missing");

            var isValid = !errors.Any();
            if (isValid)
                _logger.LogInformation("Configuration validation passed");
            else
                _logger.LogError("Configuration validation failed: {Errors}", string.Join(", ", errors));

            return (isValid, errors);
        }
    }
}
