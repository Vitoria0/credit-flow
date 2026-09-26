using Costumer.Application.Interfaces;
using Costumer.Application.Services;
using Costumer.Domain.Interfaces;
using Costumer.Infrastructure.Data;
using Costumer.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Costumer.Infrastructure.MessageBroker;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ICostumerRepository, CostumerRepository>();
builder.Services.AddScoped<ICostumerService, CostumerService>();
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
builder.Services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
