using Npgsql;

namespace DndCampaign.Api.Composition;

public static class DatabaseConnectionString
{
    public static string Resolve(IConfiguration configuration)
    {
        var configuredConnectionString = configuration.GetConnectionString("Campaigns");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return Normalize(configuredConnectionString);
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = GetRequiredConfiguration(configuration, "Database:Host"),
            Port = configuration.GetValue("Database:Port", 5432),
            Database = GetRequiredConfiguration(configuration, "Database:Name"),
            Username = GetRequiredConfiguration(configuration, "Database:User"),
            Password = ReadRequiredSecret(configuration, "Database:Password"),
        }.ConnectionString;
    }

    public static string Normalize(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var databaseUri)
            || (databaseUri.Scheme != "postgres" && databaseUri.Scheme != "postgresql"))
        {
            return connectionString;
        }

        var userInfoSeparator = databaseUri.UserInfo.IndexOf(':', StringComparison.Ordinal);
        if (userInfoSeparator <= 0)
        {
            throw new InvalidOperationException("The PostgreSQL URI must include username and password.");
        }

        var databaseName = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/'));
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("The PostgreSQL URI must include a database name.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
            Database = databaseName,
            Username = Uri.UnescapeDataString(databaseUri.UserInfo[..userInfoSeparator]),
            Password = Uri.UnescapeDataString(databaseUri.UserInfo[(userInfoSeparator + 1)..]),
        };

        foreach (var queryParameter in databaseUri.Query.TrimStart('?').Split(
            '&',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var keyValue = queryParameter.Split('=', 2);
            if (keyValue.Length == 2
                && keyValue[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(Uri.UnescapeDataString(keyValue[1]), true, out var sslMode))
            {
                builder.SslMode = sslMode;
            }
        }

        return builder.ConnectionString;
    }

    private static string GetRequiredConfiguration(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Configuration '{key}' is required.");
    }

    private static string ReadRequiredSecret(IConfiguration configuration, string key)
    {
        var secretFile = configuration[$"{key}_FILE"];
        if (!string.IsNullOrWhiteSpace(secretFile))
        {
            try
            {
                var secret = File.ReadAllText(secretFile).Trim();
                return !string.IsNullOrWhiteSpace(secret)
                    ? secret
                    : throw new InvalidOperationException($"Secret file configured by '{key}_FILE' is empty.");
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException(
                    $"Secret file configured by '{key}_FILE' could not be read.",
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new InvalidOperationException(
                    $"Secret file configured by '{key}_FILE' is not readable by the application user.",
                    exception);
            }
        }

        return GetRequiredConfiguration(configuration, key);
    }
}
