var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddHttpClient<WebShop.Api.Services.PspClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Psp:BaseUrl"]!);
});

var app = builder.Build();

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

