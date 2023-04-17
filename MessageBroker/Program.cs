using MessageBroker.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlite("Data Source=MessageBroker.db");
});

var app = builder.Build();

app.UseHttpsRedirection();

app.Run();