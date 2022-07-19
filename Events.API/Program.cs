using Amazon.SQS;
using Events.API.DB;
using Events.API.DB.SecretsManager;
using Events.API.Repository;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

var builder = WebApplication.CreateBuilder(args);

//Get SecretValue
string dbEndpoint = Environment.GetEnvironmentVariable("RDS_ENDPOINT") ?? "";
string dbName = Environment.GetEnvironmentVariable("RDS_DB_NAME") ?? "";
string secretName = Environment.GetEnvironmentVariable("RDS_SECRET_NAME") ?? "";
var secretString = new SecretManager(secretName).Get();
var secretValues = JObject.Parse(secretString);
string? dbAdminId = null;
string? dbAdminPassword = null;

if (secretValues != null && secretValues["username"] != null && secretValues["password"] != null)
{
    dbAdminId = secretValues["username"].ToString();
    dbAdminPassword = secretValues["password"].ToString();
}
else
{
    throw new Exception("Cannot get SecretManagerValues");
}

var connectionString = $"Host={dbEndpoint};Username={dbAdminId};Password={dbAdminPassword};Database={dbName}";

// Add services to the container.
builder.Services.AddDbContext<EventDBContext>(
    opts =>
    {
        opts.EnableSensitiveDataLogging();
        opts.EnableDetailedErrors();
        //if (builder.Environment.IsDevelopment())
        //    opts.UseInMemoryDatabase("test");
        //else
        opts.UseNpgsql(connectionString);
    }, ServiceLifetime.Scoped
);
builder.Services.AddSingleton<IAmazonSQS>(_ =>
    new AmazonSQSClient(Amazon.RegionEndpoint.APNortheast3)
);
builder.Services.AddTransient<IEventRepository, EventRepository>();
builder.Services.AddControllers();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
