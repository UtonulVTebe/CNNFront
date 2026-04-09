using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Windows;
using System.Windows.Threading;
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

        ShutdownMode = ShutdownMode.OnLastWindowClose;
        base.OnStartup(e);

        Dispatcher.BeginInvoke(DispatcherPriority.Background, async () =>
        {
            try
            {
                var navigator = Services.GetRequiredService<IAppNavigator>();
                await navigator.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить приложение: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        });
    }

    public static string GetOpenApiSpecFullPath() =>
        Path.Combine(AppContext.BaseDirectory, OpenApiSpecRelativePath);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddSingleton<ITokenStore, FileTokenStore>();
        services.AddHttpClient<IApiClient, ApiClient>()
            .ConfigurePrimaryHttpMessageHandler(CreateApiHttpMessageHandler)
            .ConfigureHttpClient((provider, client) =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                var baseUrl = configuration["Api:BaseUrl"] ?? "https://localhost:7128";
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });

        services.AddSingleton<BlankTemplateService>();
        services.AddSingleton<BlankTemplateSyncService>();
        services.AddSingleton<IAppNavigator, StudentAppNavigator>();
        services.AddSingleton<ExamSessionViewModel>();
        services.AddSingleton<StudentOrdersViewModel>();
        services.AddSingleton<StudentMainViewModel>();
        services.AddTransient<StudentAuthViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
    }

    private static HttpMessageHandler CreateApiHttpMessageHandler(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var handler = new HttpClientHandler();

        if (configuration.GetValue("Api:SkipCertificateValidation", false))
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        else
        {
            handler.ServerCertificateCustomValidationCallback = static (request, _, _, errors) =>
            {
                var host = request.RequestUri?.Host;
                if (host is not null && IsLocalDevHost(host))
                    return true;
                return errors == SslPolicyErrors.None;
            };
        }

        return handler;
    }

    private static bool IsLocalDevHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}
