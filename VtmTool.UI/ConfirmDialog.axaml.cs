using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VtmTool.UI;

public partial class ConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    void OnConfirm(object? sender, RoutedEventArgs e) { Confirmed = true; Close(); }
    void OnCancel(object? sender, RoutedEventArgs e) => Close();
}