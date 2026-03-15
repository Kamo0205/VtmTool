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
    int _selectedIdx = -1;
    readonly Random _rng = new();

    public MainWindow()
    {
        InitializeComponent();
        Db.Init();
        _characters.AddRange(Db.LoadAll());
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
            RefreshSheet(_characters[idx]);
    }

    // Redraw every named TextBlock from the current Character value.
    // This is the immediate-mode render: data in → display out, no retained state.
    void RefreshSheet(in Character c)
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

        RefreshSheet(updated);
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
            _characters.Add(saved);
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

    async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var c = _characters[_selectedIdx];
        
        var confirm = new ConfirmDialog($"Delete '{c.Name}'?");
        await confirm.ShowDialog(this);
        if (!confirm.Confirmed) return;
        
        Db.DeleteCharacter(c.Id);
        _characters.RemoveAt(_selectedIdx);
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