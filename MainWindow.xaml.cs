using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenTranslator.Models;
using ScreenTranslator.Services.Storage;

namespace ScreenTranslator;

public partial class MainWindow : Window
{
    private readonly AppSettings _currentSettings;

    public MainWindow()
    {
        InitializeComponent();
        _currentSettings = SettingsManager.Load();
        LoadSettingsToUi();
    }

    private void LoadSettingsToUi()
    {
        TxtApiKey.Password = _currentSettings.ApiKey;
        CmbModel.Text = _currentSettings.Model;

        SetComboValue(CmbSourceLang, _currentSettings.SourceLanguage);
        SetComboValue(CmbTargetLang, _currentSettings.TargetLanguage);
        SetComboValue(CmbModifier, _currentSettings.HotkeyModifier);
        SetComboValue(CmbKey, _currentSettings.HotkeyKey);
    }

    private void SetComboValue(ComboBox comboBox, string value)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (item.Content?.ToString()?.Equals(value, StringComparison.OrdinalIgnoreCase) == true)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
        comboBox.Text = value;
    }

    private string GetComboValue(ComboBox comboBox)
    {
        if (comboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Content?.ToString() ?? comboBox.Text;
        }
        return comboBox.Text;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _currentSettings.ApiKey = TxtApiKey.Password.Trim();
        _currentSettings.Model = string.IsNullOrWhiteSpace(CmbModel.Text) ? "google/gemini-2.0-flash-001" : CmbModel.Text.Trim();
        _currentSettings.SourceLanguage = GetComboValue(CmbSourceLang);
        _currentSettings.TargetLanguage = GetComboValue(CmbTargetLang);
        _currentSettings.HotkeyModifier = GetComboValue(CmbModifier);
        _currentSettings.HotkeyKey = GetComboValue(CmbKey);

        SettingsManager.Save(_currentSettings);

        TxtStatus.Text = "Đã lưu cài đặt!";
        TxtStatus.Foreground = Brushes.Green;
    }

    private void BtnTestCapture_Click(object sender, RoutedEventArgs e)
    {
        TxtStatus.Text = "Chức năng quét đang hoàn thiện...";
        TxtStatus.Foreground = Brushes.DodgerBlue;
    }
}