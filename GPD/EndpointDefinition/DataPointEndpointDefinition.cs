using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace GPD.EndpointDefinition;

public record DataPoint(DateTimeOffset T, string Tn, decimal? V);

public class DataPointEndpointDefinition(Configuration configuration) : IEndpointDefinition
{
    async Task TryCreateDataPointTable(NpgsqlDataSource dataSource, string name)
    {
        var sql = @$"
            CREATE TABLE IF NOT EXISTS {name}(
                id timestamp with time zone NOT NULL UNIQUE,
                value numeric,
                PRIMARY KEY (id)
        )";
        await using var cmd = dataSource.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task TryCreateTableAndAddDataPoint(DataPoint dp)
    {
        using var dataSource = NpgsqlDataSource.Create(configuration.PgSqlConnectionString);
        await TryCreateDataPointTable(dataSource, dp.Tn);
        await AddDataPoint(dataSource, dp);
    }
    
    private async Task AddDataPoint(NpgsqlDataSource dataSource, DataPoint dp)
    {
        var sql = @$"INSERT INTO {dp.Tn} (id, value) VALUES (@t,@v)";
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("@t", dp.T);
        cmd.Parameters.AddWithValue("@v", dp.V!);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task AddDataPoints(IEnumerable<DataPoint> inputDps)
    {
        using var dataSource = NpgsqlDataSource.Create(configuration.PgSqlConnectionString);
        var groupedByTableName = inputDps.GroupBy(x => x.Tn).ToArray();
        foreach (var byTableName in groupedByTableName)
        {
            await TryCreateDataPointTable(dataSource, byTableName.Key);
            foreach (var dps in byTableName.Batch(500))
            {
                var data = dps.ToArray();
                await using var conn = await dataSource.OpenConnectionAsync();
                await using var writer = await conn.BeginBinaryImportAsync(
                    $"COPY {data.First().Tn} (id, value) FROM STDIN (FORMAT BINARY)");
                
                foreach (var dp in data)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(dp.T.ToUniversalTime(), NpgsqlDbType.TimestampTz);
                    await writer.WriteAsync(dp.V, NpgsqlDbType.Numeric);
                }

                await writer.CompleteAsync();

                await conn.CloseAsync();
            }
        }
    }
    
    public void DefineEndpoints(WebApplication app)
    {
        app.MapPost("/datapoint", ([FromBody] DataPoint dataPoint) => TryCreateTableAndAddDataPoint(dataPoint))
            .WithTags("Data Points")
            .WithOpenApi();
        app.MapGet("/datapoint", ([FromQuery] DateTimeOffset t, [FromQuery] string tn, [FromQuery] decimal? v) =>
            TryCreateTableAndAddDataPoint(new DataPoint(t, tn, v)))
            .WithTags("Data Points")
            .WithOpenApi();
        app.MapPost("/datapoints", ([FromBody] DataPoint[] dataPoint) => AddDataPoints(dataPoint))
            .WithTags("Data Points")
            .WithOpenApi();
    }
}