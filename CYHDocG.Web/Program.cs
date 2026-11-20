using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Implementations;
using CYHDocG.Repo.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC & Controllers with Views
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

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

// 2. Register Repositories (Dapper + Stored Procedures)
builder.Services.AddScoped<IBaseRepository, BaseRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IConsultationRepository, ConsultationRepository>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
builder.Services.AddScoped<IAiSearchService, AiSearchService>();
builder.Services.AddScoped<IImageCompressionService, ImageCompressionService>();
builder.Services.AddScoped<IOcrPrescriptionService, OcrPrescriptionService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ILiveKitTokenService, LiveKitTokenService>();
builder.Services.AddHttpClient();

// 3. Cookie Authentication for Role-Based Portals (Admin, Doctor, User)
builder.Services.AddAuthentication("CYHAuthCookie")
    .AddCookie("CYHAuthCookie", options =>
    {
        options.Cookie.Name = "CYHDocGuru.Auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 4. HTTP Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

    // Block direct access to client .map source files
    if (context.Request.Path.Value?.EndsWith(".map", StringComparison.OrdinalIgnoreCase) == true)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Area routing (Admin, Doctor, User)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Default public route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Real-Time Hubs
app.MapHub<CYHDocG.Web.Hubs.LiveReelHub>("/hubs/live-reel");
app.MapHub<CYHDocG.Web.Hubs.ConsultationHub>("/hubs/consultation");

app.Run();
