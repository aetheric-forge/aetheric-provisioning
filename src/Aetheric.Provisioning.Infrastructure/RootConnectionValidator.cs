using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Engine;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;
using StackExchange.Redis;

namespace Aetheric.Provisioning.Infrastructure;

public sealed class RootConnectionValidator : IRootConnectionValidator
{
    public async Task<ConnectionCheck> TestAsync(string system, RootCredential credential, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            credential = InfrastructureConnections.Normalize(system, credential);
            return system switch
            {
                "redis" => await RedisAsync(credential, deadline.Token),
                "rabbitmq" => await RabbitAsync(credential, deadline.Token),
                "postgres" => await PostgresAsync(credential, deadline.Token),
                "mongo" => await MongoAsync(credential, deadline.Token),
                _ => new(false, "invalid")
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return new(false, "timeout"); }
        catch (TimeoutException) { return new(false, "timeout"); }
        catch (ArgumentException) { return new(false, "invalid"); }
        catch (RedisServerException ex)
        {
            return new(false, ex.Message.StartsWith("NOPERM", StringComparison.Ordinal) ? "permission"
                : ex.Message.StartsWith("ERR unknown", StringComparison.OrdinalIgnoreCase) ? "unsupported" : "authentication");
        }
        catch (RedisConnectionException ex)
        { return new(false, ex.FailureType == ConnectionFailureType.AuthenticationFailure ? "authentication" : "unreachable"); }
        catch (PostgresException ex) { return new(false, ex.SqlState.StartsWith("28", StringComparison.Ordinal) ? "authentication" : "database"); }
        catch (MongoAuthenticationException) { return new(false, "authentication"); }
        catch (MongoCommandException ex) { return new(false, ex.Code == 13 ? "permission" : ex.Code == 18 ? "authentication" : "database"); }
        catch (Exception) { return new(false, "unreachable"); }
    }

    private static async Task<ConnectionCheck> RedisAsync(RootCredential credential, CancellationToken ct)
    {
        var options = new ConfigurationOptions
        {
            User = credential.Username, Password = credential.Password, ConnectTimeout = 5000,
            AsyncTimeout = 5000, SyncTimeout = 5000, ConnectRetry = 0, AbortOnConnectFail = true,
            AllowAdmin = true, ClientName = "aetheric-bootstrap-check"
        };
        options.EndPoints.Add(credential.Host, credential.Port);
        using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        ct.ThrowIfCancellationRequested();
        var database = connection.GetDatabase();
        await database.PingAsync().WaitAsync(ct);
        var user = (string?)await database.ExecuteAsync("ACL", "WHOAMI").WaitAsync(ct);
        if (user != (credential.Username ?? "default")) return new(false, "authentication");
        foreach (var operation in new[] { "SETUSER", "DELUSER" })
        {
            var result = (string?)await database.ExecuteAsync("ACL", "DRYRUN", user, "ACL", operation, "aetheric-dryrun-only").WaitAsync(ct);
            if (result != "OK") return new(false, "permission");
        }
        return ConnectionCheck.Verified;
    }

    private static async Task<ConnectionCheck> RabbitAsync(RootCredential credential, CancellationToken ct)
    {
        var options = credential.RabbitMq!;
        var origin = new UriBuilder(options.Scheme, credential.Host, credential.Port, options.BasePath).Uri;
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, "api/whoami"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credential.Username + ":" + credential.Password)));
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return new(false, "authentication");
        if (response.StatusCode == HttpStatusCode.Forbidden) return new(false, "permission");
        if (!response.IsSuccessStatusCode) return new(false, "management_api");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = json.RootElement;
        if (!root.TryGetProperty("name", out var name) || name.GetString() != credential.Username) return new(false, "authentication");
        if (!root.TryGetProperty("tags", out var tags)) return new(false, "permission");
        var isAdmin = tags.ValueKind == JsonValueKind.Array
            ? tags.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == "administrator")
            : tags.ValueKind == JsonValueKind.String && tags.GetString()!.Split(',').Select(x => x.Trim()).Contains("administrator");
        return isAdmin ? ConnectionCheck.Verified : new(false, "permission");
    }

    private static async Task<ConnectionCheck> PostgresAsync(RootCredential credential, CancellationToken ct)
    {
        var settings = new NpgsqlConnectionStringBuilder
        {
            Host = credential.Host, Port = credential.Port, Username = credential.Username, Password = credential.Password,
            Database = credential.Postgres!.Database, Timeout = 5, CommandTimeout = 5, Pooling = false,
            IncludeErrorDetail = false, PersistSecurityInfo = false, ApplicationName = "aetheric-bootstrap-check"
        };
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT rolsuper FROM pg_catalog.pg_roles WHERE rolname = current_user", connection);
        return await command.ExecuteScalarAsync(ct) is true ? ConnectionCheck.Verified : new(false, "permission");
    }

    private static async Task<ConnectionCheck> MongoAsync(RootCredential credential, CancellationToken ct)
    {
        var options = credential.Mongo!;
        var settings = new MongoClientSettings
        {
            Server = new MongoServerAddress(credential.Host, credential.Port),
            Credential = MongoCredential.CreateCredential(options.AuthDatabase, credential.Username, credential.Password),
            DirectConnection = options.DirectConnection, ConnectTimeout = TimeSpan.FromSeconds(5),
            ServerSelectionTimeout = TimeSpan.FromSeconds(5), SocketTimeout = TimeSpan.FromSeconds(5),
            ApplicationName = "aetheric-bootstrap-check"
        };
        using var client = new MongoClient(settings);
        var database = client.GetDatabase("admin");
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
        var status = await database.RunCommandAsync<BsonDocument>(new BsonDocument("connectionStatus", 1), cancellationToken: ct);
        var auth = status["authInfo"].AsBsonDocument;
        var authenticated = auth["authenticatedUsers"].AsBsonArray.Any(x => x["user"].AsString == credential.Username && x["db"].AsString == options.AuthDatabase);
        var root = auth["authenticatedUserRoles"].AsBsonArray.Any(x => x["role"].AsString == "root" && x["db"].AsString == "admin");
        return !authenticated ? new(false, "authentication") : root ? ConnectionCheck.Verified : new(false, "permission");
    }
}
