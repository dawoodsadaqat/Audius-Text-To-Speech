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

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendLocalhost", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:3001",
                "http://127.0.0.1:3001",
                "http://185.182.187.248:3001"
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

app.UseCors("FrontendLocalhost");

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(outputsDirectory),
    RequestPath = "/outputs"
});

app.MapControllers();

app.Run();