using LocalizationManager.ViewModels;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace LocalizationManager.Views {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow(MainViewModel viewModel) {
            DataContext = viewModel;
            InitializeComponent();
        }

        private void TextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = false;
            fbd.ShowDialog();

            var folderPath = fbd.SelectedPath;
            DirectoryInfo dir = new DirectoryInfo(folderPath);
            if (!dir.Exists)
                throw new DirectoryNotFoundException("Not found: " + dir.FullName);

            var subDirs = dir.GetDirectories("Resources");
            if (subDirs.Length == 0)
                throw new DirectoryNotFoundException("Resources directory not found in project folder!");

            (DataContext as MainViewModel).ProjectDirectory = dir;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            (DataContext as MainViewModel).PersistCache();
        }

        private void DataGrid_RowEditEnding(object sender, System.Windows.Controls.DataGridRowEditEndingEventArgs e) {
            (DataContext as MainViewModel).Callback_OnRowEditEnding(sender, e);
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e) {
            (DataContext as MainViewModel).SaveChanges(((DataView)dataGrid1.ItemsSource).ToTable());
        }
    }
}
