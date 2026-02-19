using MultiplayerMatchmaking.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Built-in OpenAPI (new .NET 8 way)
builder.Services.AddOpenApi();

// Our services
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddHostedService<MatchmakingWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Generates OpenAPI JSON
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
