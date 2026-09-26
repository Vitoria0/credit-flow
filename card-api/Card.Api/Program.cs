using Card.Application.Interfaces;
using Card.Application.Services;
using Card.Domain.Interfaces;
using Card.Infrastructure.Data;
using Card.Infrastructure.MessageBroker;
using Card.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ICardRepository, CardRepository>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddSingleton(_ =>
{
    var configuration = builder.Configuration.GetSection("RabbitMq");
    return new ConnectionFactory
    {
        HostName = configuration["HostName"] ?? "localhost",
        Port = configuration.GetValue<int>("Port", 5672),
        UserName = configuration["UserName"] ?? "guest",
        Password = configuration["Password"] ?? "guest",
        AutomaticRecoveryEnabled = false
    };
});
builder.Services.AddHostedService<PropostaGeradaConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
