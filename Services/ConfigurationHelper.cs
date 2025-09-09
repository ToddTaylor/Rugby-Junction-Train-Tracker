using Microsoft.Extensions.Configuration;

namespace Services
{
    public static class ConfigurationHelper
    {
        public static IConfiguration LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory());

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            if (!string.IsNullOrEmpty(environment))
            {
                builder.AddJsonFile($"appsettings.{environment}.json", optional: false, reloadOnChange: true);
            }
            else
            {
                builder.AddJsonFile("appsettings.Local.json", optional: false, reloadOnChange: true);
            }

            return builder.Build();
        }
    }
}
