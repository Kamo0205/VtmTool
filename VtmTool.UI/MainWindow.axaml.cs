using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VtmTool.Core;
using VtmTool.Core.Enums;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class MainWindow : Window
{
    private readonly List<Character> _characters = new();
    private readonly List<List<Discipline>> _disciplines = new();
    int _selectedIdx = -1;
    readonly Random _rng = new();

    public MainWindow()
    {
        InitializeComponent();
        Db.Init();

        var chars = Db.LoadAll();
        var discMap = Db.LoadAllDisciplines();

        foreach (var c in chars)
        {
            _characters.Add(c);
            _disciplines.Add(discMap.TryGetValue(c.Id, out var d) ? d : new List<Discipline>());
        }

        RebuildList();
    }

    // Sidebase list
    //Rebuild the character button list from _characters
    private void RebuildList()
    {
        CharacterList.Children.Clear();
        for (int i = 0; i < _characters.Count; i++)
        {
            int captured = i; // capture for lambda
            var c = _characters[i];

            var btn = new Button
            {
                Classes = { "charItem" },
                Content = BuildListItemContent(c),
                Tag = i,
            };
            if (i == _selectedIdx)
                btn.Classes.Add("selected");

            btn.Click += (_, _) => SelectCharacter(captured);
            CharacterList.Children.Add(btn);
        }

        if (_characters.Count == 0)
            SelectCharacter(-1);
    }

    static StackPanel BuildListItemContent(in Character c)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = c.Name,
            Foreground = Brushes.White,
            FontSize = 13,
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{ClanName(c.Clan)}  ·  {c.Generation}th",
            Foreground = new SolidColorBrush(Color.Parse("#888")),
            FontSize = 11,
        });
        return panel;
    }

    // Sheet display

    void SelectCharacter(int idx)
    {
        _selectedIdx = idx;

        // Update selected highlight on all buttons
        for (int i = 0; i < CharacterList.Children.Count; i++)
        {
            if (CharacterList.Children[i] is Button btn)
            {
                btn.Classes.Remove("selected");
                if (i == idx) btn.Classes.Add("selected");
            }
        }

        bool hasChar = idx >= 0 && idx < _characters.Count;
        EmptyState.IsVisible = !hasChar;
        SheetPanel.IsVisible = hasChar;

        if (hasChar)
            RefreshSheet(_characters[idx], _disciplines[idx]);
    }

    // Redraw every named TextBlock from the current Character value.
    // This is the immediate-mode render: data in → display out, no retained state.
    void RefreshSheet(in Character c, List<Discipline> disciplines)
    {
        // Identity
        SheetName.Text = c.Name;
        SheetSubtitle.Text = $"{ClanName(c.Clan)}  ·  {c.Generation}th Generation  ·  Humanity {Dots(c.Humanity, 10)}";
        SheetBloodPotency.Text = $"Blood Potency {Dots(c.BloodPotency)}  (max {Rules.MaxBloodPotency(c.Generation)})";

        // Attributes
        AttrStrength.Text = Dots(c.Strength);
        AttrDexterity.Text = Dots(c.Dexterity);
        AttrStamina.Text = Dots(c.Stamina);
        AttrCharisma.Text = Dots(c.Charisma);
        AttrManipulation.Text = Dots(c.Manipulation);
        AttrComposure.Text = Dots(c.Composure);
        AttrIntelligence.Text = Dots(c.Intelligence);
        AttrWits.Text = Dots(c.Wits);
        AttrResolve.Text = Dots(c.Resolve);

        // Skills
        SkillAthletics.Text = Dots(c.Athletics);
        SkillBrawl.Text = Dots(c.Brawl);
        SkillCraft.Text = Dots(c.Craft);
        SkillDrive.Text = Dots(c.Drive);
        SkillFirearms.Text = Dots(c.Firearms);
        SkillLarceny.Text = Dots(c.Larceny);
        SkillMelee.Text = Dots(c.Melee);
        SkillStealth.Text = Dots(c.Stealth);
        SkillSurvival.Text = Dots(c.Survival);
        SkillAnimalKen.Text = Dots(c.AnimalKen);
        SkillEtiquette.Text = Dots(c.Etiquette);
        SkillInsight.Text = Dots(c.Insight);
        SkillIntimidation.Text = Dots(c.Intimidation);
        SkillLeadership.Text = Dots(c.Leadership);
        SkillPerformance.Text = Dots(c.Performance);
        SkillPersuasion.Text = Dots(c.Persuasion);
        SkillStreetwise.Text = Dots(c.Streetwise);
        SkillSubterfuge.Text = Dots(c.Subterfuge);
        SkillAcademics.Text = Dots(c.Academics);
        SkillAwareness.Text = Dots(c.Awareness);
        SkillFinance.Text = Dots(c.Finance);
        SkillInvestigation.Text = Dots(c.Investigation);
        SkillMedicine.Text = Dots(c.Medicine);
        SkillOccult.Text = Dots(c.Occult);
        SkillPolitics.Text = Dots(c.Politics);
        SkillScience.Text = Dots(c.Science);
        SkillTechnology.Text = Dots(c.Technology);

        // Health & Willpower
        TrackHealth.Text = DamageTrack(c.AggravatedHealth, c.SuperficialHealth, c.HealthMax);
        TrackWillpower.Text = DamageTrack(c.AggravatedWillpower, c.SuperficialWillpower, c.WillpowerMax);

        int wp = Rules.WoundPenalty(c);
        WoundPenalty.Text = wp < 0 ? $"  wound {wp}" : "";

        // Hunger
        TrackHunger.Text = HungerTrack(c.Hunger) + $"  {c.Hunger}/5";

        // Disciplines — panel is cleared and rebuilt every render.
        // This is the same immediate-mode principle as the rest of the sheet:
        // the panel has no memory of what it showed last frame.
        DisciplinePanel.Children.Clear();
        if (disciplines.Count == 0)
        {
            DisciplinePanel.Children.Add(new TextBlock
            {
                Text = "None",
                Foreground = new SolidColorBrush(Color.Parse("#555")),
                FontSize = 12,
            });
        }
        else
        {
            // Lay out disciplines in a three-column grid regardless of count.
            // At creation there are always 1–3; "Edit Disciplines" can produce more
            // if we ever allow out-of-clan disciplines in a future version.
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            int rowCount = (disciplines.Count + 2) / 3;
            for (int r = 0; r < rowCount; r++)
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (int i = 0; i < disciplines.Count; i++)
            {
                var d = disciplines[i];
                var cell = new StackPanel
                {
                    Spacing = 2,
                    Margin = new Avalonia.Thickness(0, 0, 16, 8),
                };
                cell.Children.Add(new TextBlock
                {
                    Text = ClanDisciplines.DisplayName(d.Name),
                    Foreground = new SolidColorBrush(Color.Parse("#aaa")),
                    FontSize = 12,
                });
                cell.Children.Add(new TextBlock
                {
                    Text = Dots(d.Rating),
                    Foreground = new SolidColorBrush(Color.Parse("#cc2200")),
                    FontSize = 13,
                });

                Grid.SetColumn(cell, i % 3);
                Grid.SetRow(cell, i / 3);
                grid.Children.Add(cell);
            }

            DisciplinePanel.Children.Add(grid);
        }
    }

    // Mutation helpers

    // Write mutated character back to list and DB, then redraw.
    void Commit(Character updated)
    {
        _characters[_selectedIdx] = updated;
        Db.SaveCharacter(updated);

        // Rebuild list button text in case name changed
        if (CharacterList.Children[_selectedIdx] is Button btn)
            btn.Content = BuildListItemContent(updated);

        RefreshSheet(updated, _disciplines[_selectedIdx]);
    }

    void SetStatus(string msg) => StatusMsg.Text = msg;

    // Button handlers

    async void OnNewCharacter(object? sender, RoutedEventArgs e)
    {
        // Creation wizard is a separate dialog — opens synchronously for now
        // and returns the created character.
        
        var wizard = new CreationWizard(0);
        await wizard.ShowDialog(this);
        if (wizard.Result is Character created)
        {
            var saved = Db.SaveCharacter(created);
            var disciplines = Db.ReplaceAllDisciplines(saved.Id, wizard.PendingDisciplines);

            _characters.Add(saved);
            _disciplines.Add(disciplines);
            RebuildList();
            SelectCharacter(_characters.Count - 1);
            SetStatus($"'{saved.Name}' created.");
        }
    }

    void OnRouse(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var c = _characters[_selectedIdx];
        int roll = _rng.Next(1, 11);
        bool success = roll >= 6;

        byte prev = c.Hunger;
        c.Hunger = Rules.RouseCheck(c, roll);
        Commit(c);

        string result = success
            ? $"Rouse — rolled {roll}: Success. Hunger unchanged ({c.Hunger})."
            : $"Rouse — rolled {roll}: Failure. Hunger {prev} → {c.Hunger}.";

        if (c.Hunger == 5)
            result += "  ⚠ Hunger 5 — Frenzy check required!";

        SetStatus(result);
    }

    async void OnDamage(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var dialog = new DamageDialog(mode: DamageDialogMode.Apply);
        await dialog.ShowDialog(this);
        if (dialog.Confirmed)
            ApplyDamageResult(dialog.Track, dialog.DamageType, dialog.Amount);
    }

    async void OnHeal(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var dialog = new DamageDialog(mode: DamageDialogMode.Heal);
        await dialog.ShowDialog(this);
        if (dialog.Confirmed)
            ApplyDamageResult(dialog.Track, dialog.DamageType, dialog.Amount);
    }

    void ApplyDamageResult(string track, string type, int amount)
    {
        var c = _characters[_selectedIdx];
        c = (track, type) switch
        {
            ("health", "superficial") => Rules.ApplySuperficialHealth(c, amount),
            ("health", "aggravated") => Rules.ApplyAggravatedHealth(c, amount),
            ("willpower", "superficial") => Rules.ApplySuperficialWillpower(c, amount),
            ("willpower", "aggravated") => Rules.ApplyAggravatedWillpower(c, amount),
            ("health", "heal-sup") => Rules.HealSuperficialHealth(c, amount),
            ("health", "heal-agg") => Rules.HealAggravatedHealth(c, amount),
            ("willpower", "heal-sup") => Rules.HealSuperficialWillpower(c, amount),
            ("willpower", "heal-agg") => Rules.HealAggravatedWillpower(c, amount),
            _ => c
        };
        Commit(c);
        SetStatus($"Damage updated.");
    }

    async void OnEditAttr(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var wizard = new CreationWizard(startStep: 1);
        await wizard.ShowDialog(this);
        if (wizard.Result is Character updated) { Commit(updated); SetStatus("Attributes updated."); }
    }

    async void OnEditSkills(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var wizard = new CreationWizard(startStep: 3);
        await wizard.ShowDialog(this);
        if (wizard.Result is Character updated) { Commit(updated); SetStatus("Skills updated."); }
    }

    async void OnEditDisciplines(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var wizard = new CreationWizard(startStep: 5);
        wizard.SeedClan(_characters[_selectedIdx].Clan);
        await wizard.ShowDialog(this);

        // wizard.Result will be null if the wizard was cancelled before Finish().
        // For the discipline-only edit we only care about PendingDisciplines.
        if (wizard.PendingDisciplines.Count > 0)
        {
            var updated = _characters[_selectedIdx];
            var disciplines = Db.ReplaceAllDisciplines(updated.Id, wizard.PendingDisciplines);
            _disciplines[_selectedIdx] = disciplines;
            RefreshSheet(updated, disciplines);
            SetStatus("Disciplines updated.");
        }
    }

    async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var c = _characters[_selectedIdx];
        
        var confirm = new ConfirmDialog($"Delete '{c.Name}'?");
        await confirm.ShowDialog(this);
        if (!confirm.Confirmed) return;
        
        Db.DeleteCharacter(c.Id);
        _characters.RemoveAt(_selectedIdx);
        _disciplines.RemoveAt(_selectedIdx);
        _selectedIdx = -1;
        RebuildList();
        SelectCharacter(-1);
        SetStatus($"'{c.Name}' deleted.");
    }

    // Rendering helpers — same logic as Print.cs, now returning strings

    static string Dots(byte value, byte max = 5) =>
        new string('\u25CF', Math.Min(value, max)) +
        new string('\u25CB', Math.Max(max - value, 0));

    static string DamageTrack(byte agg, byte sup, byte max)
    {
        var sb = new System.Text.StringBuilder(max * 3);
        for (int i = 0; i < max; i++)
        {
            if (i < agg) sb.Append("[X]");
            else if (i < agg + sup) sb.Append("[/]");
            else sb.Append("[ ]");
        }
        return sb.ToString();
    }

    static string HungerTrack(byte hunger)
    {
        var sb = new System.Text.StringBuilder(15);
        for (int i = 0; i < 5; i++)
            sb.Append(i < hunger ? "[H]" : "[ ]");
        return sb.ToString();
    }

    static string ClanName(Clan c) => c switch
    {
        Clan.Banu_Haqim => "Banu Haqim",
        _ => c.ToString()
    };
}