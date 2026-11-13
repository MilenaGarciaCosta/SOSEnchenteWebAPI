using Microsoft.EntityFrameworkCore;
using SOSEnchenteWebAPI.DbAdapter;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found");

builder.Services.AddDbContext<AppDbContext>(
    options =>options.UseMySql(
        connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();
app.MapControllers();
app.Run();
