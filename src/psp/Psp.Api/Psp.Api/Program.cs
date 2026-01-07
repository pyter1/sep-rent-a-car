using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// PSP -> Bank (typed client)
builder.Services.AddHttpClient<Psp.Api.Services.BankClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Bank:BaseUrl"]!);
});

// PSP -> Merchant (WebShop callback) (named client)
builder.Services.AddHttpClient("MerchantCallback", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

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

app.MapControllers();
app.MapGet("/health", () => Results.Ok("OK"));

app.Run();
