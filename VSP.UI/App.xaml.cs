using System.Windows;
using VSP.Infrastructure.Database;

namespace VSP.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var databaseService = new DatabaseService();
        var initializer = new DatabaseInitializer(databaseService);

        initializer.Initialize();
    }
}