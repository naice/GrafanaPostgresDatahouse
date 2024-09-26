using System.Reflection;
using GPD.EndpointDefinition;

var builder = WebApplication.CreateBuilder(args);
var configuration = new Configuration();
builder.Configuration.GetSection(Configuration.SECTION_KEY).Bind(configuration);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

var endpointDefinitionIType = typeof(IEndpointDefinition);
var endpointDefinitionTypes = Assembly.GetExecutingAssembly()
    .GetTypes().Where(t => t is { IsAbstract: false, IsClass: true, } && t.IsAssignableTo(endpointDefinitionIType));
foreach (var type in endpointDefinitionTypes)
{
    var endpointDefinition = ActivatorUtilities.CreateInstance(app.Services, type) as IEndpointDefinition;
    endpointDefinition?.DefineEndpoints(app);
}

app.Run();
