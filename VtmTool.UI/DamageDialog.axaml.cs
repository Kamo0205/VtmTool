using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VtmTool.UI;

public enum DamageDialogMode { Apply, Heal }

public partial class DamageDialog : Window
{
    public bool Confirmed { get; private set; }
    public string Track { get; private set; } = "health";
    public string DamageType { get; private set; } = "superficial";
    public int Amount { get; private set; } = 1;

    readonly DamageDialogMode _mode;

    public DamageDialog() : this(DamageDialogMode.Apply)
    {
        InitializeComponent();
    }

    public DamageDialog(DamageDialogMode mode)
    {
        _mode = mode;
        InitializeComponent();

        DialogTitle.Text = mode == DamageDialogMode.Apply ? "Apply Damage" : "Heal Damage";

        // Populate type options based on mode
        TypeBox.Items.Clear();
        if (mode == DamageDialogMode.Apply)
        {
            TypeBox.Items.Add(new ComboBoxItem { Content = "Superficial", Tag = "superficial" });
            TypeBox.Items.Add(new ComboBoxItem { Content = "Aggravated", Tag = "aggravated" });
        }
        else
        {
            TypeBox.Items.Add(new ComboBoxItem { Content = "Superficial", Tag = "heal-sup" });
            TypeBox.Items.Add(new ComboBoxItem { Content = "Aggravated", Tag = "heal-agg" });
        }

        TrackBox.SelectedIndex = 0;
        TypeBox.SelectedIndex = 0;
    }

    void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Track = (TrackBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "health";
        DamageType = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "superficial";
        Amount = (int)(AmountBox.Value ?? 1);
        Confirmed = true;
        Close();
    }

    void OnCancel(object? sender, RoutedEventArgs e) => Close();
}