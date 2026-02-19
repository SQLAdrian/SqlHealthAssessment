namespace SqlHealthAssessment.Data.Models
{
    /// <summary>
    /// Constants for server authentication types.
    /// </summary>
    public static class AuthenticationTypes
    {
        /// <summary>
        /// Windows integrated authentication.
        /// </summary>
        public const string Windows = "Windows";

        /// <summary>
        /// SQL Server username/password authentication.
        /// </summary>
        public const string SqlServer = "SqlServer";

        /// <summary>
        /// Microsoft Entra MFA (Azure AD) interactive authentication.
        /// Uses SqlAuthenticationMethod.ActiveDirectoryInteractive.
        /// </summary>
        public const string EntraMFA = "EntraMFA";
    }
}
