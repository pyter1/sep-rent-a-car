using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;
using Common.Observability;
using Psp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<BankClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Bank:BaseUrl"] ?? "http://bank-api:7002");
});

// PSP -> Merchant (WebShop callback) (named client)
builder.Services.AddHttpClient("MerchantCallback", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<Psp.Api.Services.MerchantCallbackClient>();
builder.Services.AddSingleton<CurrencyConverter>();

builder.Services.AddDbContext<PspDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PspDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (ctx, next) =>
{
    ctx.Request.EnableBuffering();
    await next();
});

app.MapControllers();

app.Run();
