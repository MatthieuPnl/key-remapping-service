using KeyRemappingService.Data;
using KeyRemappingService.Endpoints;
using KeyRemappingService.KeyboardCatalog;
using KeyRemappingService.Services;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

//Read config variables with fallbacks
var connectionString = builder.Configuration.GetConnectionString("KeyMappingDb") ?? "Data Source=keymaps.db";
var keyboardPathConfig = builder.Configuration["KeyboardCatalog:Path"] ?? Path.Combine(builder.Environment.ContentRootPath, "Keyboards");


builder.Services.AddDbContextFactory<KeyMappingDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<IKeyboardCatalog>(sp =>
    new JsonKeyboardCatalog(
        keyboardPathConfig,
        sp.GetRequiredService<ILogger<JsonKeyboardCatalog>>()));
builder.Services.AddSingleton<IKeyMappingRepository, EfKeyMappingRepository>();
builder.Services.AddSingleton<KeyMappingService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Services.GetRequiredService<IKeyboardCatalog>();
app.Services.GetRequiredService<IKeyMappingRepository>().Initialize();

app.MapKeyMappingEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Logger.LogInformation("Key Remapping Services ready");

app.Run();
