using KoiPonders.Views;

namespace KoiPonders
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(ReportEditPage), typeof(ReportEditPage));
        }
    }
}
