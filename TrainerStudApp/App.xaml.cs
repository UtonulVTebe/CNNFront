using System.IO;
using System.Net.Http.Headers;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainerStudApp.Presentation.ViewModels;
using TrainerStudApp.Services;

namespace TrainerStudApp;

public partial class App : Application
{
    public static IConfiguration Configuration { get; private set; } = null!;
    public static IServiceProvider Services { get; private set; } = null!;

    public static string ApiBaseUrl => Configuration["Api:BaseUrl"] ?? string.Empty;

    public static string OpenApiSpecRelativePath =>
        Configuration["Api:OpenApiSpec"] ?? "Swagger/swagger.json";

    protected override void OnStartup(StartupEventArgs e)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        Configuration = builder.Build();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var window = Services.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    public static string GetOpenApiSpecFullPath() =>
        Path.Combine(AppContext.BaseDirectory, OpenApiSpecRelativePath);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddSingleton<ITokenStore, FileTokenStore>();
        services.AddHttpClient<IApiClient, ApiClient>((provider, client) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var baseUrl = configuration["Api:BaseUrl"] ?? "https://localhost:7128";
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<BlankTemplateService>();
        services.AddSingleton<BlankTemplateSyncService>();
        services.AddSingleton<ExamSessionViewModel>();
        services.AddSingleton<StudentMainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
