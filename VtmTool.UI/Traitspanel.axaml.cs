using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using VtmTool.Core;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class TraitsPanel : UserControl
{
    // =========================================================================
    // State
    // =========================================================================
    int _characterId = -1;
    readonly List<CharacterTrait> _traits = new();

    // Raised when a trait is added, edited, or removed — MainWindow persists
    public event EventHandler<List<CharacterTrait>>? TraitsChanged;

    // Raised when player clicks ▲ on a Background to spend XP
    public event EventHandler<CharacterTrait>? RaiseRequested;

    // =========================================================================
    // Init
    // =========================================================================
    public TraitsPanel() => InitializeComponent();

    // =========================================================================
    // Public API
    // =========================================================================
    public void SetCharacter(int characterId, List<CharacterTrait> traits)
    {
        _characterId = characterId;
        _traits.Clear();
        _traits.AddRange(traits);
        Rebuild();
    }

    public void ClearCharacter()
    {
        _characterId = -1;
        _traits.Clear();
        Rebuild();
    }

    // Called by MainWindow after an XP raise is committed so the panel refreshes
    public void RefreshTrait(CharacterTrait updated)
    {
        for (int i = 0; i < _traits.Count; i++)
            if (_traits[i].Id == updated.Id) { _traits[i] = updated; break; }
        Rebuild();
    }

    // =========================================================================
    // Rebuild — immediate-mode render of all three lists
    // =========================================================================
    void Rebuild()
    {
        RebuildSection(BackgroundList, TraitCategory.Background);
        RebuildSection(MeritList, TraitCategory.Merit);
        RebuildSection(FlawList, TraitCategory.Flaw);
    }

    void RebuildSection(StackPanel container, TraitCategory cat)
    {
        container.Children.Clear();

        bool any = false;
        foreach (var t in _traits)
        {
            if (t.Category != cat) continue;
            container.Children.Add(BuildTraitRow(t));
            any = true;
        }

        if (!any)
            container.Children.Add(new TextBlock
            {
                Text = "None",
                Foreground = new SolidColorBrush(Color.Parse("#444")),
                FontSize = 11,
            });
    }

    // One row: dots  name  detail  [▲] [edit] [×]
    Grid BuildTraitRow(CharacterTrait t)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto"),
            Margin = new Avalonia.Thickness(0, 1, 0, 1),
        };

        // Dots
        string dots = t.Rating > 0
            ? new string('●', t.Rating) + new string('○', 5 - t.Rating)
            : "—";
        var dotsBlock = new TextBlock
        {
            Text = dots,
            Foreground = new SolidColorBrush(Color.Parse("#cc2200")),
            FontSize = 12,
            Width = 70,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(dotsBlock, 0);
        grid.Children.Add(dotsBlock);

        // Name
        var nameBlock = new TextBlock
        {
            Text = t.Name,
            Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")),
            FontSize = 12,
            Width = 140,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(nameBlock, 1);
        grid.Children.Add(nameBlock);

        // Detail
        var detailBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(t.Detail) ? "" : $"— {t.Detail}",
            Foreground = new SolidColorBrush(Color.Parse("#666")),
            FontSize = 11,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(detailBlock, 2);
        grid.Children.Add(detailBlock);

        // ▲ Raise button (Backgrounds and Merits only, not at max)
        if (t.Category != TraitCategory.Flaw && t.Rating < 5)
        {
            var raiseBtn = MakeIconBtn("▲", "#444", "#cc2200");
            raiseBtn.Click += (_, _) => RaiseRequested?.Invoke(this, t);
            Grid.SetColumn(raiseBtn, 3);
            grid.Children.Add(raiseBtn);
        }

        // Edit button
        var editBtn = MakeIconBtn("✎", "#444", "#aaa");
        editBtn.Click += async (_, _) => await EditTrait(t);
        Grid.SetColumn(editBtn, 4);
        grid.Children.Add(editBtn);

        // Remove button
        var removeBtn = MakeIconBtn("×", "#444", "#cc0000");
        removeBtn.Click += (_, _) => RemoveTrait(t);
        Grid.SetColumn(removeBtn, 5);
        grid.Children.Add(removeBtn);

        return grid;
    }

    static Button MakeIconBtn(string icon, string normalHex, string hoverHex)
    {
        var btn = new Button
        {
            Content = icon,
            Classes = { "traitAction" },
            Foreground = new SolidColorBrush(Color.Parse(normalHex)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        btn.PointerEntered += (_, _) =>
            btn.Foreground = new SolidColorBrush(Color.Parse(hoverHex));
        btn.PointerExited += (_, _) =>
            btn.Foreground = new SolidColorBrush(Color.Parse(normalHex));
        return btn;
    }

    // =========================================================================
    // Add
    // =========================================================================
    async void OnAddTrait(object? sender, RoutedEventArgs e)
    {
        if (_characterId < 0) return;

        // Tag on each button encodes the category (0/1/2)
        byte catByte = sender is Button btn && btn.Tag is string s
            ? byte.TryParse(s, out byte b) ? b : (byte)0
            : (byte)0;
        var cat = (TraitCategory)catByte;

        var dialog = new TraitEditDialog(cat, _characterId);
        await dialog.ShowDialog(GetParentWindow());
        if (!dialog.Confirmed) return;

        var saved = Db.SaveTrait(dialog.Result);
        _traits.Add(saved);
        Rebuild();
        TraitsChanged?.Invoke(this, _traits);
    }

    // =========================================================================
    // Edit
    // =========================================================================
    async System.Threading.Tasks.Task EditTrait(CharacterTrait t)
    {
        var dialog = new TraitEditDialog(t);
        await dialog.ShowDialog(GetParentWindow());
        if (!dialog.Confirmed) return;

        var saved = Db.SaveTrait(dialog.Result);
        for (int i = 0; i < _traits.Count; i++)
            if (_traits[i].Id == saved.Id) { _traits[i] = saved; break; }

        Rebuild();
        TraitsChanged?.Invoke(this, _traits);
    }

    // =========================================================================
    // Remove
    // =========================================================================
    void RemoveTrait(CharacterTrait t)
    {
        Db.DeleteTrait(t.Id);
        for (int i = 0; i < _traits.Count; i++)
            if (_traits[i].Id == t.Id) { _traits.RemoveAt(i); break; }
        Rebuild();
        TraitsChanged?.Invoke(this, _traits);
    }

    // =========================================================================
    // Helper
    // =========================================================================
    Window GetParentWindow()
    {
        var current = this.Parent;
        while (current != null)
        {
            if (current is Window w) return w;
            current = current.Parent;
        }
        throw new InvalidOperationException("TraitsPanel is not attached to a Window.");
    }
}