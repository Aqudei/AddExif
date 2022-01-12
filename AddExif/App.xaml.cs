using AddExif.Views;
using MahApps.Metro.Controls.Dialogs;
using Prism.Ioc;
using Prism.Unity;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AddExif
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance(DialogCoordinator.Instance);
        }

        protected override Window CreateShell()
        {
            var shell = Container.Resolve<Shell>();
            return (Window)shell;
        }
    }
}
