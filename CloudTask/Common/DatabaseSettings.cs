using System.Fabric;

namespace Common
{
    /// <summary>
    /// Reads the Postgres connection string from the service's Config package (PackageRoot/Config/Settings.xml,
    /// section "Database", parameter "ConnectionString") so it can be changed without recompiling.
    /// </summary>
    public static class DatabaseSettings
    {
        private const string ConfigPackageName = "Config";
        private const string SectionName = "Database";
        private const string ConnectionStringParameter = "ConnectionString";

        public static string GetConnectionString(ServiceContext context)
        {
            var config = context.CodePackageActivationContext.GetConfigurationPackageObject(ConfigPackageName);
            return config.Settings.Sections[SectionName].Parameters[ConnectionStringParameter].Value;
        }
    }
}
