using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using TextToVideo.Api.Options;
using TextToVideo.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AzureSpeechOptions>(
    builder.Configuration.GetSection("AzureSpeech"));

builder.Services.Configure<FfmpegOptions>(
    builder.Configuration.GetSection("Ffmpeg"));

builder.Services.Configure<LicenseOptions>(
    builder.Configuration.GetSection("License"));

builder.Services.Configure<RuntimeOptions>(
    builder.Configuration.GetSection("Runtime"));

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AudiusDesktop", policy =>
    {
        policy
            .WithOrigins(
                "tauri://localhost",
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:5055",
                "http://127.0.0.1:5055"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddScoped<ITextFileService, TextFileService>();
builder.Services.AddScoped<ISpeechService, SpeechService>();
builder.Services.AddScoped<IVideoRenderService, VideoRenderService>();
builder.Services.AddScoped<IFfmpegService, FfmpegService>();
builder.Services.AddScoped<IVideoGenerationService, VideoGenerationService>();

builder.Services.AddSingleton<IMachineIdService, MachineIdService>();
builder.Services.AddHttpClient<ILicenseService, LicenseService>();

var app = builder.Build();

var outputsDirectory = Path.Combine(
    app.Environment.ContentRootPath,
    "outputs");

Directory.CreateDirectory(outputsDirectory);

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".mp4"] = "video/mp4";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(outputsDirectory),
    RequestPath = "/outputs",
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = true
});

app.UseCors("AudiusDesktop");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(outputsDirectory),
    RequestPath = "/outputs",
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = true
});
app.MapControllers();

app.Run();