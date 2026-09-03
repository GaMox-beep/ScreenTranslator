using System.Windows;
using ScreenTranslator.ViewModels;

namespace ScreenTranslator;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        TxtApiKey.Password = ViewModel.ApiKey;
    }

    private void TxtApiKey_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = TxtApiKey.Password;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = TxtApiKey.Password;
        ViewModel.SaveSettings();
    }

    private void BtnTestCapture_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetStatus("Chức năng quét đang hoàn thiện...", isError: false);
    }
}