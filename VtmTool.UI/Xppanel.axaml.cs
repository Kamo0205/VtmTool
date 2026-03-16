using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using VtmTool.Core;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class XpPanel : UserControl
{
    int _characterId = -1;
    int _earned = 0;
    int _spent = 0;

    readonly List<XpAward> _awards = new();
    readonly List<XpPurchase> _purchases = new();

    // Raised when an award is added — MainWindow updates its parallel totals
    public event EventHandler<XpAward>? AwardAdded;

    public XpPanel()
    {
        InitializeComponent();
    }

    public void SetCharacter(int characterId, int earned, int spent,
                              List<XpAward> awards, List<XpPurchase> purchases)
    {
        _characterId = characterId;
        _earned = earned;
        _spent = spent;

        _awards.Clear(); _awards.AddRange(awards);
        _purchases.Clear(); _purchases.AddRange(purchases);

        RefreshSummary();
        RebuildHistory();
    }

    public void ClearCharacter()
    {
        _characterId = -1;
        _earned = _spent = 0;
        _awards.Clear();
        _purchases.Clear();
        RefreshSummary();
        RebuildHistory();
    }

    // Called by MainWindow after a purchase is committed so the panel stays current
    public void AddPurchase(XpPurchase purchase)
    {
        _purchases.Add(purchase);
        _spent += purchase.Cost;
        RefreshSummary();
        RebuildHistory();
    }

    // =========================================================================
    // Award
    // =========================================================================
    void OnAward(object? sender, RoutedEventArgs e)
    {
        if (_characterId < 0) return;
        if (!int.TryParse(AwardAmountBox.Text?.Trim(), out int amount) || amount <= 0)
        {
            AwardAmountBox.BorderBrush = new SolidColorBrush(Color.Parse("#cc0000"));
            return;
        }

        AwardAmountBox.BorderBrush = new SolidColorBrush(Color.Parse("#444"));
        string note = AwardNoteBox.Text?.Trim() ?? "";

        var award = Db.AddAward(_characterId, amount, note);
        _awards.Add(award);
        _earned += amount;

        AwardAmountBox.Text = "";
        AwardNoteBox.Text = "";

        RefreshSummary();
        RebuildHistory();

        AwardAdded?.Invoke(this, award);
    }

    // =========================================================================
    // Rendering
    // =========================================================================
    void RefreshSummary()
    {
        int available = _earned - _spent;
        AvailableLabel.Text = available.ToString();
        EarnedLabel.Text = _earned.ToString();
        SpentLabel.Text = _spent.ToString();

        AvailableLabel.Foreground = available >= 0
            ? new SolidColorBrush(Color.Parse("#44ff88"))
            : new SolidColorBrush(Color.Parse("#cc0000"));
    }

    void RebuildHistory()
    {
        HistoryList.Children.Clear();

        if (_characterId < 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = "No character loaded.",
                Foreground = new SolidColorBrush(Color.Parse("#444")),
                FontSize = 11,
            });
            return;
        }

        if (_awards.Count == 0 && _purchases.Count == 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = "No XP history yet.",
                Foreground = new SolidColorBrush(Color.Parse("#444")),
                FontSize = 11,
            });
            return;
        }

        // Merge awards and purchases into a single chronological list
        // by comparing their timestamps.
        var entries = new List<(string timestamp, bool isAward, string description, int amount)>();

        foreach (var a in _awards)
            entries.Add((a.AwardedAt, true,
                string.IsNullOrWhiteSpace(a.Note) ? "XP awarded" : a.Note,
                a.Amount));

        foreach (var p in _purchases)
            entries.Add((p.PurchasedAt, false,
                $"{p.TraitName} {p.FromRating} → {p.ToRating}",
                -p.Cost));

        entries.Sort((a, b) => string.Compare(b.timestamp, a.timestamp, StringComparison.Ordinal));

        foreach (var (ts, isAward, desc, amount) in entries)
        {
            // Parse timestamp for display
            string dateStr = ts.Length >= 10 ? ts[..10] : ts;

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,60") };

            row.Children.Add(new TextBlock
            {
                Text = desc,
                Foreground = new SolidColorBrush(Color.Parse(isAward ? "#aaa" : "#888")),
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });

            var dateBlock = new TextBlock
            {
                Text = dateStr,
                Foreground = new SolidColorBrush(Color.Parse("#444")),
                FontSize = 10,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            };
            Grid.SetColumn(dateBlock, 1);
            row.Children.Add(dateBlock);

            var amountBlock = new TextBlock
            {
                Text = amount > 0 ? $"+{amount}" : amount.ToString(),
                Foreground = new SolidColorBrush(
                    Color.Parse(amount > 0 ? "#44ff88" : "#cc2200")),
                FontSize = 11,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            Grid.SetColumn(amountBlock, 2);
            row.Children.Add(amountBlock);

            HistoryList.Children.Add(row);
        }
    }
}