using MCP.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add CORS policy for Blazor client and external MCP clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:5001", "http://localhost:5000", "https://localhost:5267", 
                           "https://localhost:7030", "http://localhost:5267", "http://localhost:5268", 
                           "https://localhost:7232")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
    
    // Add a more permissive policy for MCP clients
    options.AddPolicy("AllowMcpClients", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add MCP service
builder.Services.AddScoped<McpGraphService>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
// Enable CORS - use specific policies for different endpoints
app.UseCors();
app.UseAuthorization();
// Map controllers with specific CORS policy for MCP endpoints
app.MapControllers().RequireCors("AllowMcpClients");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
