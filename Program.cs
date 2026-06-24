using MiPrinter.Interface;
using MiPrinter.Model;
using MiPrinter.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<IPrints<Print, DataPrint>, Printer>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Crear carpeta config
var configDir = Path.Combine(
    AppContext.BaseDirectory,
    "config"
);

Directory.CreateDirectory(configDir);

// Crear archivo printers.json si no existe
var printersFile = Path.Combine(
    configDir,
    "printers.json"
);

if (!System.IO.File.Exists(printersFile))
{
    await System.IO.File.WriteAllTextAsync(
        printersFile,
        "[]"
    );
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFronts", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFronts");

app.UseAuthorization();

app.MapControllers();

app.Run();
