using Microsoft.EntityFrameworkCore;
using Bank.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
// builder.Services.AddSingleton<Bank.Api.Storage.PaymentSessionStore>();

builder.Services.AddHttpClient<Bank.Api.Services.PspNotifyClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Psp:BaseUrl"]!);
});
builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));


var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// app.UseHttpsRedirection();


app.MapGet("/health", () => Results.Ok("OK"));

app.Run();
