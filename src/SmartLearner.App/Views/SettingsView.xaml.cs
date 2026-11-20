using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SmartLearner.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

#if DEBUG
        Loaded += (_, _) =>
        {
            if (Environment.GetEnvironmentVariable("SL_DEBUG_SETTINGS_BAILIAN") == "1")
            {
                Dispatcher.BeginInvoke(
                    new Action(() => BailianCard.BringIntoView()),
                    DispatcherPriority.ContextIdle);
            }
        };
#endif
    }
}
