using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;


namespace Awesome_Password_Generator
{
    /// <summary>
    /// Interaction logic for QuickGenInfo.xaml
    /// </summary>
    public partial class QuickGenInfo : Window
    {
        public QuickGenInfo()
        {
            InitializeComponent();

            System.Media.SystemSounds.Asterisk.Play();
        }

        private void cmdOK_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.mainWindow.showQuickGenInfoWindow = !(bool)cbDontShowThisDialogAgain.IsChecked;
        }

        private void hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            // open a webpage
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));

            e.Handled = true;
        }
    }
}
