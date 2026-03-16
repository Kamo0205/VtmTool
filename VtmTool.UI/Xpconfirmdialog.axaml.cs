using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;

namespace VtmTool.UI;

public partial class XpConfirmDialog : Window
{
    public bool Confirmed { get; private set; }
    public XpConfirmDialog()
    {
        InitializeComponent();
    }

    public XpConfirmDialog(string traitDescription, int cost, int available)
    {
        InitializeComponent();

        int after = available - cost;
        bool canAfford = available >= cost;

        TraitLabel.Text = traitDescription;
        CostLabel.Text = $"{cost} XP";
        AvailableLabel.Text = $"{available} XP";
        AfterLabel.Text = $"{after} XP";

        AvailableLabel.Foreground = canAfford
            ? new SolidColorBrush(Color.Parse("#44ff88"))
            : new SolidColorBrush(Color.Parse("#cc4400"));

        AfterLabel.Foreground = after >= 0
            ? new SolidColorBrush(Color.Parse("#aaa"))
            : new SolidColorBrush(Color.Parse("#cc0000"));

        WarningLabel.Text = canAfford
            ? ""
            : $"⚠ Insufficient XP — this will leave {Math.Abs(after)} XP in debt.";

        ConfirmBtn.Content = canAfford ? "Spend XP" : "Spend Anyway";
    }

    void OnConfirm(object? sender, RoutedEventArgs e) { Confirmed = true; Close(); }
    void OnCancel(object? sender, RoutedEventArgs e) => Close();
}