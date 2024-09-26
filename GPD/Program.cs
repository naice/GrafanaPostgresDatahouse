using Microsoft.AspNetCore.Mvc;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var configuration = new Configuration();
builder.Configuration.GetSection(Configuration.SECTION_KEY).Bind(configuration);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

async Task TryCreateDataPointTable(string name)
{
    var sql = @$"
    CREATE TABLE IF NOT EXISTS {name}(
        id timestamp with time zone NOT NULL UNIQUE,
        value VARCHAR(255),
        PRIMARY KEY (id)
    )";
    
    using var dataSource =  NpgsqlDataSource.Create(configuration.PgSqlConnectionString);
    await using var cmd = dataSource.CreateCommand(sql);
    await cmd.ExecuteNonQueryAsync();
}

async Task AddDataPoint(DataPoint dp)
{
    await TryCreateDataPointTable(dp.Tn);
    
    using var dataSource = NpgsqlDataSource.Create(configuration.PgSqlConnectionString);
    var sql = @$"INSERT INTO {dp.Tn}(id, value) " +
              "VALUES(@t,@v)";
    await using var cmd = dataSource.CreateCommand(sql);
    cmd.Parameters.AddWithValue("@t", dp.T);
    cmd.Parameters.AddWithValue("@v", dp.V!);
    await cmd.ExecuteNonQueryAsync();
}

void AddDataPoints(DataPoint[] dp)
{
    
}

app.MapPost("/data", ([FromBody] DataPoint dataPoint) => AddDataPoint(dataPoint))
    .WithTags("Data Points")
    .WithOpenApi();
app.MapGet("/data", ([FromQuery] DateTimeOffset t, [FromQuery] string tn, [FromQuery] decimal? v) =>
        AddDataPoint(new DataPoint(t, tn, v)))
    .WithTags("Data Points")
    .WithOpenApi();

app.Run();

public record DataPoint(DateTimeOffset T, string Tn, decimal? V);
public class Configuration
{
    public const string SECTION_KEY = nameof(Configuration);
    public string PgSqlConnectionString { get; set; } = string.Empty;
}