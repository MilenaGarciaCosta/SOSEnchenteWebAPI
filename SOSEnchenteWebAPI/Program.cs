using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SOSEnchenteWebAPI.DbAdapter;
using SOSEnchenteWebAPI.DbAdapter.Repositories;
using SOSEnchenteWebAPI.Domain.Interfaces;
using SOSEnchenteWebAPI.Hubs;
using SOSEnchenteWebAPI.Middlewares;
using SOSEnchenteWebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found");

builder.Services.AddDbContext<AppDbContext>(
    options => options.UseMySql(
        connectionString, ServerVersion.AutoDetect(connectionString)));

// Repositórios e serviços de domínio
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<IPriorityService, PriorityService>();

// Serviços de cibersegurança (Parsing PII, Taint Analysis e tradução via Gemini)
builder.Services.AddScoped<IPiiScannerService, PiiScannerService>();
builder.Services.AddScoped<ISafeLoggerService, SafeLoggerService>();
builder.Services.AddHttpClient<IAiRiskTranslatorService, AiRiskTranslatorService>();

// Alertas em tempo real (SignalR e IoT via MQTT)
builder.Services.AddSignalR();
builder.Services.AddHostedService<MqttAlertListener>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not found");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// Ordem obrigatória: mapeia PII na entrada (source) e valida vazamentos na saída (sink)
app.UseMiddleware<PiiParsingMiddleware>();
app.UseMiddleware<TaintSinkMiddleware>();

app.MapControllers();
app.MapHub<AlertHub>("/hubs/alerts");
app.Run();
