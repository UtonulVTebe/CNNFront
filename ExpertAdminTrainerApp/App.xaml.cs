using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;

namespace ExpertAdminTrainerApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IConfiguration Configuration { get; private set; } = null!;

        /// <summary>Базовый URL API из appsettings.json (секция Api:BaseUrl).</summary>
        public static string ApiBaseUrl => Configuration["Api:BaseUrl"] ?? string.Empty;

        /// <summary>Относительный путь к OpenAPI-файлу в выходной папке (секция Api:OpenApiSpec).</summary>
        public static string OpenApiSpecRelativePath =>
            Configuration["Api:OpenApiSpec"] ?? "Swagger/swagger.json";

        protected override void OnStartup(StartupEventArgs e)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            Configuration = builder.Build();

            base.OnStartup(e);
        }

        /// <summary>Полный путь к swagger.json рядом с exe (для чтения файла).</summary>
        public static string GetOpenApiSpecFullPath() =>
            Path.Combine(AppContext.BaseDirectory, OpenApiSpecRelativePath);
    }
}
