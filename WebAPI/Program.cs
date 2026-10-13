using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WebAPI.Data;
using WebAPI.Models;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Knowledge Assistant API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=knowledge_assistant.db"));

builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();

var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "KnowledgeAssistant_SuperSecret_Jwt_Key_2026!#";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "KnowledgeAssistantWebAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "KnowledgeAssistantApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var assistantInternalKey = builder.Configuration["AssistantService:InternalKey"];

builder.Services.AddHttpClient<AssistantServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AssistantService:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = Timeout.InfiniteTimeSpan;
    if (!string.IsNullOrWhiteSpace(assistantInternalKey))
    {
        client.DefaultRequestHeaders.Add("X-Internal-Key", assistantInternalKey);
    }
});
builder.Services.AddHttpClient<AssistantIngestionClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AssistantService:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromMinutes(5);
    if (!string.IsNullOrWhiteSpace(assistantInternalKey))
    {
        client.DefaultRequestHeaders.Add("X-Internal-Key", assistantInternalKey);
    }
});
builder.Services.AddHostedService<DocumentIngestionWorker>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var adminUsername = builder.Configuration["AdminSeed:Username"] ?? "admin";
    var adminPassword = builder.Configuration["AdminSeed:Password"] ?? "AdminPassword123!";

    var adminUser = db.Users.FirstOrDefault(u => u.Username.ToLower() == adminUsername.ToLower());
    if (adminUser is null)
    {
        var (hash, salt) = passwordHasher.HashPassword(adminPassword);
        adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = adminUsername,
            PasswordHash = hash,
            Salt = salt,
            Role = "Admin",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(adminUser);
        db.SaveChanges();
    }

    var unassignedConversations = db.Conversations.Where(c => c.UserId == Guid.Empty).ToList();
    if (unassignedConversations.Count > 0)
    {
        foreach (var conv in unassignedConversations)
        {
            conv.UserId = adminUser.Id;
        }
        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
