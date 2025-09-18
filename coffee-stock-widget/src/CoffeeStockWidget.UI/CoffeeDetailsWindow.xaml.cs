using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace CoffeeStockWidget.UI
{
    public partial class CoffeeDetailsWindow : Window
    {
        public CoffeeDetailsWindow()
        {
            InitializeComponent();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OpenWebsiteBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext != null)
                {
                    var prop = DataContext.GetType().GetProperty("Url", BindingFlags.Public | BindingFlags.Instance);
                    var val = prop?.GetValue(DataContext) as Uri;
                    if (val != null)
                    {
                        Process.Start(new ProcessStartInfo(val.ToString()) { UseShellExecute = true });
                        DialogResult = true;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
