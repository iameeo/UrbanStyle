using Microsoft.Extensions.DependencyInjection;
using urban_style_auto_regist;

namespace urban_style_auto_regist
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var services = new ServiceCollection();
            ConfigureServices(services);  // 서비스 등록
            var serviceProvider = services.BuildServiceProvider();

            var mainForm = serviceProvider.GetRequiredService<Form1>();
            Application.Run(mainForm);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // DbContext 등록 (연결 문자열을 AppDbContext에서 설정하므로 여기서 제외)
            services.AddDbContext<AppDbContext>();

            // Form1을 transient로 등록
            services.AddTransient<Form1>();
        }
    }
}
