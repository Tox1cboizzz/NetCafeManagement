using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SessionService.Application.Commands;
using SessionService.Infrastructure.BackgroundJobs;
using SessionService.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SessionService", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.ApiKey, Scheme = "Bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});
builder.Services.AddDbContext<SessionDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(StartSessionCommandHandler).Assembly));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddHttpClient("WalletService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:WalletService"] ?? "http://localhost:5002");
});
builder.Services.AddHttpClient("MachineService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:MachineService"] ?? "http://localhost:5003");
});
builder.Services.AddHostedService<SessionBillingBackgroundService>();
var app = builder.Build();
app.UseSwagger(); app.UseSwaggerUI();
app.UseAuthentication(); app.UseAuthorization();
app.UseCors();
app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
    db.Database.Migrate();
}
app.Run();
