using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;



var builder = WebApplication.CreateBuilder(args);

// Read the connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Register your connection string so it can be used in services
builder.Services.AddSingleton(new SqlConnection(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Try database connection manually (like EF's OpenConnection/CloseConnection)
try
{
    using (var connection = new SqlConnection(connectionString))
    {
        connection.Open();
        Console.WriteLine("Database connection established");
        connection.Close();
    }
}
catch (Exception ex)
{
    Console.WriteLine("Database connection failed: " + ex.Message);
    throw;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();