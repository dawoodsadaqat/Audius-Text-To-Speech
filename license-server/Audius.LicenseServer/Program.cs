using Audius.LicenseServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IFileLicenseService, FileLicenseService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AudiusLicenseCors", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AudiusLicenseCors");

app.MapControllers();

app.Run();