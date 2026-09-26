using Credit.Infrastructure.Data;
using Credit.Application.Interfaces;
using Credit.Application.Services;
using Credit.Domain.Interfaces;
using Credit.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Credit.Infrastructure.MessageBroker;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ICreditProposalRepository, CreditProposalRepository>();
builder.Services.AddScoped<ICreditProposalService, CreditProposalService>();
builder.Services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddSingleton<IScoreGenerator, RandomScoreGenerator>();
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
builder.Services.AddHostedService<ClienteCadastradoConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
