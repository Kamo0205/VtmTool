using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using VtmTool.Core;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class DiceRollerPanel : UserControl
{
    // State
    Character _character;
    bool _hasCharacter = false;
    int _modifier = 0;
    int _difficulty = 1;
    readonly Random _rng = new();

    // History — most recent first, max 5
    const int MaxHistory = 5;
    readonly List<RollResult> _history = new(MaxHistory);

    // =========================================================================
    // Attribute and skill entries — (label, getValue delegate)
    // =========================================================================
    record struct StatEntry(string Label, Func<Character, byte> Get);

    static readonly StatEntry[] Attributes =
    {
        new("Strength",     c => c.Strength),
        new("Dexterity",    c => c.Dexterity),
        new("Stamina",      c => c.Stamina),
        new("Charisma",     c => c.Charisma),
        new("Manipulation", c => c.Manipulation),
        new("Composure",    c => c.Composure),
        new("Intelligence", c => c.Intelligence),
        new("Wits",         c => c.Wits),
        new("Resolve",      c => c.Resolve),
    };

    static readonly StatEntry[] Skills =
    {
        new("— (no skill)", _ => 0),
        new("Athletics",    c => c.Athletics),    new("Brawl",         c => c.Brawl),
        new("Craft",        c => c.Craft),        new("Drive",         c => c.Drive),
        new("Firearms",     c => c.Firearms),     new("Larceny",       c => c.Larceny),
        new("Melee",        c => c.Melee),        new("Stealth",       c => c.Stealth),
        new("Survival",     c => c.Survival),
        new("Animal Ken",   c => c.AnimalKen),    new("Etiquette",     c => c.Etiquette),
        new("Insight",      c => c.Insight),      new("Intimidation",  c => c.Intimidation),
        new("Leadership",   c => c.Leadership),   new("Performance",   c => c.Performance),
        new("Persuasion",   c => c.Persuasion),   new("Streetwise",    c => c.Streetwise),
        new("Subterfuge",   c => c.Subterfuge),
        new("Academics",    c => c.Academics),    new("Awareness",     c => c.Awareness),
        new("Finance",      c => c.Finance),      new("Investigation", c => c.Investigation),
        new("Medicine",     c => c.Medicine),     new("Occult",        c => c.Occult),
        new("Politics",     c => c.Politics),     new("Science",       c => c.Science),
        new("Technology",   c => c.Technology),
    };

    public DiceRollerPanel()
    {
        InitializeComponent();
        PopulateDropdowns();
        RefreshPool();
    }

    void PopulateDropdowns()
    {
        foreach (var a in Attributes)
            AttrBox.Items.Add(new ComboBoxItem { Content = a.Label });
        AttrBox.SelectedIndex = 0;

        foreach (var s in Skills)
            SkillBox.Items.Add(new ComboBoxItem { Content = s.Label });
        SkillBox.SelectedIndex = 0;
    }

    // =========================================================================
    // Public API — called by MainWindow when the selected character changes
    // =========================================================================
    public void SetCharacter(Character c)
    {
        _character = c;
        _hasCharacter = true;
        _modifier = 0;
        ModLabel.Text = "0";
        RefreshPool();
    }

    public void ClearCharacter()
    {
        _hasCharacter = false;
        RefreshPool();
    }

    // Also called after damage is applied so wound penalty updates live
    public void RefreshCharacter(Character c)
    {
        _character = c;
        if (_hasCharacter) RefreshPool();
    }

    // =========================================================================
    // Pool calculation
    // =========================================================================
    void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => RefreshPool();
    void OnCheckedChanged(object? sender, RoutedEventArgs e) => RefreshPool();

    void RefreshPool()
    {
        if (!_hasCharacter)
        {
            PoolTotalLabel.Text = "—";
            PoolBreakdownLabel.Text = "No character loaded";
            WoundNote.Text = "";
            RollBtn.IsEnabled = false;
            return;
        }

        int attrIdx = AttrBox.SelectedIndex;
        int skillIdx = SkillBox.SelectedIndex;
        if (attrIdx < 0) attrIdx = 0;
        if (skillIdx < 0) skillIdx = 0;

        int attrVal = Attributes[attrIdx].Get(_character);
        int skillVal = Skills[skillIdx].Get(_character);
        int wp = WillpowerCheck.IsChecked == true ? 3 : 0;
        int wound = Rules.WoundPenalty(_character);  // negative or 0

        int pool = Math.Max(0, attrVal + skillVal + _modifier + wp + wound);

        // Breakdown label
        string attrPart = $"{Attributes[attrIdx].Label} {attrVal}";
        string skillPart = skillVal > 0 ? $" + {Skills[skillIdx].Label} {skillVal}" : "";
        string modPart = _modifier != 0 ? $" {(_modifier > 0 ? "+" : "−")} {Math.Abs(_modifier)}" : "";
        string wpPart = wp > 0 ? " + WP 3" : "";
        string woundPart = wound < 0 ? $" − {Math.Abs(wound)}" : "";

        PoolTotalLabel.Text = pool.ToString();
        PoolBreakdownLabel.Text = $"{attrPart}{skillPart}{modPart}{wpPart}{woundPart} = {pool}";
        WoundNote.Text = wound < 0 ? $"Wound penalty {wound}" : "";
        RollBtn.IsEnabled = pool > 0;
    }

    // =========================================================================
    // Controls
    // =========================================================================
    void OnModUp(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _modifier++;
        ModLabel.Text = _modifier >= 0 ? $"+{_modifier}" : _modifier.ToString();
        RefreshPool();
    }

    void OnModDown(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _modifier--;
        ModLabel.Text = _modifier >= 0 ? $"+{_modifier}" : _modifier.ToString();
        RefreshPool();
    }

    void OnDiffUp(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _difficulty = Math.Min(_difficulty + 1, 20);
        DiffLabel.Text = $"Difficulty: {_difficulty}";
    }

    void OnDiffDown(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _difficulty = Math.Max(_difficulty - 1, 1);
        DiffLabel.Text = $"Difficulty: {_difficulty}";
    }

    // =========================================================================
    // Roll
    // =========================================================================
    void OnRoll(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_hasCharacter) return;

        int attrIdx = Math.Max(0, AttrBox.SelectedIndex);
        int skillIdx = Math.Max(0, SkillBox.SelectedIndex);

        int attrVal = Attributes[attrIdx].Get(_character);
        int skillVal = Skills[skillIdx].Get(_character);
        int wp = WillpowerCheck.IsChecked == true ? 3 : 0;
        int wound = Rules.WoundPenalty(_character);
        int pool = Math.Max(0, attrVal + skillVal + _modifier + wp + wound);

        string skillPart = skillVal > 0 ? $" + {Skills[skillIdx].Label} {skillVal}" : "";
        string modPart = _modifier != 0 ? $" {(_modifier > 0 ? "+" : "−")} {Math.Abs(_modifier)}" : "";
        string wpPart = wp > 0 ? " +WP" : "";
        string poolLabel = $"{Attributes[attrIdx].Label} {attrVal}{skillPart}{modPart}{wpPart}";

        var result = Rules.Roll(
            poolSize: pool,
            hungerCount: Math.Min(_character.Hunger, pool),
            difficulty: _difficulty,
            poolLabel: poolLabel,
            rng: _rng);

        // Prepend to history, cap at MaxHistory
        _history.Insert(0, result);
        if (_history.Count > MaxHistory) _history.RemoveAt(_history.Count - 1);

        // If willpower was used, apply the cost
        if (WillpowerCheck.IsChecked == true)
        {
            WillpowerCheck.IsChecked = false;
            // Caller (MainWindow) needs to apply 1 superficial willpower damage.
            // We raise an event so MainWindow can handle persistence.
            WillpowerSpent?.Invoke(this, EventArgs.Empty);
        }

        RebuildHistory();
    }

    // Raised when the player checks Willpower and rolls — MainWindow listens
    // and applies 1 superficial willpower damage + calls Commit.
    public event EventHandler? WillpowerSpent;

    // =========================================================================
    // History rendering
    // =========================================================================
    void RebuildHistory()
    {
        HistoryPanel.Children.Clear();
        foreach (var result in _history)
            HistoryPanel.Children.Add(BuildResultCard(result));
    }

    Border BuildResultCard(RollResult r)
    {
        // Choose card accent colour by outcome
        string accentHex = (r.IsBestialFailure, r.IsMessyCritical, r.IsCriticalWin, r.IsSuccess) switch
        {
            (true, _, _, _) => "#660000",   // dark red — bestial
            (_, true, _, _) => "#7a3800",   // dark orange — messy
            (_, _, true, _) => "#005500",   // dark green — critical
            (_, _, _, true) => "#003355",   // dark blue — success
            _ => "#2a2a2a",   // grey — failure
        };

        string verdictHex = (r.IsBestialFailure, r.IsMessyCritical, r.IsCriticalWin, r.IsSuccess) switch
        {
            (true, _, _, _) => "#ff4444",
            (_, true, _, _) => "#ffaa44",
            (_, _, true, _) => "#44ff88",
            (_, _, _, true) => "#44aaff",
            _ => "#888888",
        };

        var card = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1a1a1a")),
            BorderBrush = new SolidColorBrush(Color.Parse(accentHex)),
            BorderThickness = new Thickness(0, 0, 0, 2),
            Padding = new Thickness(10, 8),
        };

        var stack = new StackPanel { Spacing = 6 };

        // Pool label
        stack.Children.Add(new TextBlock
        {
            Text = r.PoolLabel,
            Foreground = new SolidColorBrush(Color.Parse("#666")),
            FontSize = 10,
        });

        // Dice display — normal dice then hunger dice
        var diceRow = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (int d in r.NormalDice)
            diceRow.Children.Add(BuildDie(d, isHunger: false));

        foreach (int d in r.HungerDice)
            diceRow.Children.Add(BuildDie(d, isHunger: true));

        stack.Children.Add(diceRow);

        // Successes count
        stack.Children.Add(new TextBlock
        {
            Text = $"{r.Successes} success{(r.Successes == 1 ? "" : "es")}  ·  difficulty {r.Difficulty}",
            Foreground = new SolidColorBrush(Color.Parse("#888")),
            FontSize = 11,
        });

        // Verdict
        stack.Children.Add(new TextBlock
        {
            Text = r.Verdict,
            Foreground = new SolidColorBrush(Color.Parse(verdictHex)),
            FontSize = 12,
            FontWeight = FontWeight.Bold,
        });

        card.Child = stack;
        return card;
    }

    // One die: a coloured square with the number inside.
    // Colour coding:
    //   Normal die  6–9  : dark green fill
    //   Normal die  10   : bright green fill (critical)
    //   Normal die  1–5  : dark grey fill
    //   Hunger die  6–9  : dark orange fill
    //   Hunger die  10   : bright orange fill (messy)
    //   Hunger die  1    : bright red fill (bestial)
    //   Hunger die  2–5  : dark red fill
    static Border BuildDie(int value, bool isHunger)
    {
        string fillHex = isHunger
            ? value switch
            {
                10 => "#cc5500",   // messy critical — bright orange
                1 => "#cc0000",   // bestial — bright red
                >= 6 => "#7a3800",   // hunger success — dark orange
                _ => "#3d1a00",   // hunger fail — very dark red
            }
            : value switch
            {
                10 => "#007700",   // critical — bright green
                >= 6 => "#004400",   // success — dark green
                _ => "#2a2a2a",   // fail — dark grey
            };

        string textHex = (value >= 6 || value == 1) ? "#ffffff" : "#888888";

        // Hunger dice get a subtle border to distinguish them
        string borderHex = isHunger ? "#cc2200" : "#333333";

        return new Border
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(2),
            Background = new SolidColorBrush(Color.Parse(fillHex)),
            BorderBrush = new SolidColorBrush(Color.Parse(borderHex)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Child = new TextBlock
            {
                Text = value.ToString(),
                Foreground = new SolidColorBrush(Color.Parse(textHex)),
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }
}