using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Implementations;
using CYHDocG.Repo.Interfaces;

using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// High-Speed Response Compression (Brotli + Gzip)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// 2. Register Repositories (Dapper + Stored Procedures) & Services
builder.Services.AddScoped<IBaseRepository, BaseRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IConsultationRepository, ConsultationRepository>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
builder.Services.AddScoped<IAiSearchService, AiSearchService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ILiveKitTokenService, LiveKitTokenService>();
builder.Services.AddHttpClient();

// Configure LiveKit Settings from appsettings
builder.Services.Configure<LiveKitSettings>(options =>
{
    var section = builder.Configuration.GetSection("LiveKitSettings");
    if (!section.Exists() || string.IsNullOrEmpty(section["ApiKey"]))
    {
        section = builder.Configuration.GetSection("LiveKit");
    }
    section.Bind(options);
    var explicitUrl = section["LiveKitUrl"] ?? section["Url"] ?? section["ServerUrl"];
    if (!string.IsNullOrWhiteSpace(explicitUrl))
    {
        options.LiveKitUrl = explicitUrl;
    }
});

// 3. Configure JWT Authentication
var jwtKey = builder.Configuration["Authentication:Jwt:SecretKey"] 
          ?? builder.Configuration["Jwt:Key"] 
          ?? "PassCyh123456789987654321987654321_PROD";
var jwtIssuer = builder.Configuration["Authentication:Jwt:Issuer"] 
             ?? builder.Configuration["Jwt:Issuer"] 
             ?? "CYHDocGuru";
var jwtAudience = builder.Configuration["Authentication:Jwt:Audience"] 
               ?? builder.Configuration["Jwt:Audience"] 
               ?? "CYHDocGuruUsers";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 4. Add CORS for Mobile & Client Web Apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "CYHDocGuru REST API", 
        Version = "v1",
        Description = "Core RESTful API with Live MSSQL Stored Procedures, Google SSO, JWT Auth & AI Triage."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT Bearer token: Bearer {your token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Configure Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CYHDocGuru API v1");
    c.RoutePrefix = "swagger";
});

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Run();
