using CoreWebApi;
using CoreWebApi.Mapper;
using CoreWebApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration; // ÷±Ω”∑√Œ 

// Add services to the container.
builder.Services.AddDbContext<MyDbContext>(options => options.UseMySql(configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion));
builder.Services.AddScoped<IMysqlService, MysqlServiceImpl>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<Worker2>();
builder.Services.AddHostedService<Worker1>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


