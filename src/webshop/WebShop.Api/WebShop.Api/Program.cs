using System.Text;
using Common.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WebShop.Api.Data;
using WebShop.Api.Services;
using WebShop.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// DB
builder.Services.AddDbContext<WebShopDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<PspClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Psp:BaseUrl"] ?? "http://psp-api:7001");
    client.Timeout = TimeSpan.FromSeconds(10);
});
// JWT auth
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    Console.WriteLine("WARNING: Jwt:Key is missing. Auth will fail until configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "webshop",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "webshop-ui",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey ?? "DEV_ONLY_CHANGE_ME"))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", p => p.RequireRole("Customer", "Admin"));
    options.AddPolicy("Admin", p => p.RequireRole("Admin"));
});

var app = builder.Build();

// Auto-migrate on startup (same pattern as Bank/PSP)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WebShopDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestBodyCaptureMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("OK"));

app.Run();
