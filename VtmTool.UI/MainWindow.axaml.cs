using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    // =========================================================================
    // State — _disciplines indexed in parallel with _characters
    // =========================================================================
    readonly List<Character> _characters = new();
    readonly List<List<Discipline>> _disciplines = new();
    readonly List<CharacterNotes> _notes = new();
    readonly List<string[]> _specialties = new();
    readonly List<PredatorType> _predatorTypes = new();
    readonly List<int> _xpEarned = new();
    readonly List<int> _xpSpent = new();
    readonly List<List<CharacterTrait>> _traits = new();
    int _selectedIdx = -1;
    readonly Random _rng = new();

    DiceRollerPanel _roller = null!;
    NotesPanel _notes_panel = null!;
    XpPanel _xpPanel = null!;
    TraitsPanel _traitsPanel = null!;

    // =========================================================================
    // Init
    // =========================================================================
    public MainWindow()
    {
        InitializeComponent();
        _roller = this.FindControl<DiceRollerPanel>("Roller")!;
        Db.Init();

        var chars = Db.LoadAll();
        var discMap = Db.LoadAllDisciplines();
        var notesMap = Db.LoadAllNotes();

        foreach (var c in chars)
        {
            _characters.Add(c);
            _disciplines.Add(discMap.TryGetValue(c.Id, out var d) ? d : new List<Discipline>());
            _notes.Add(notesMap.TryGetValue(c.Id, out var n) ? n : default);
        }

        // Wire the willpower-spent event from the roller panel
        Roller.WillpowerSpent += OnRollerWillpowerSpent;
        _roller.WillpowerSpent += OnRollerWillpowerSpent;
        _notes_panel = this.FindControl<NotesPanel>("NotesPanel")!;
        _notes_panel.NotesSaved += OnNotesSaved;

        _xpPanel = this.FindControl<XpPanel>("XpPanel")!;
        _xpPanel.AwardAdded += OnXpAwardAdded;

        var xpTotals = Db.LoadAllXpTotals();
        foreach (var c in chars)
        {
            (int Earned, int Spent) totals = xpTotals.TryGetValue(c.Id, out var t) ? t : (0, 0);
            _xpEarned.Add(totals.Earned);
            _xpSpent.Add(totals.Spent);
        }

        _traitsPanel = this.FindControl<TraitsPanel>("TraitsPanel")!;
        _traitsPanel.RaiseRequested += OnTraitRaiseRequested;

        var traitsMap = Db.LoadAllTraits();
        foreach (var c in chars)
            _traits.Add(traitsMap.TryGetValue(c.Id, out var t) ? t : new List<CharacterTrait>());

        var specialtiesMap = Db.LoadAllSpecialties();
        //var predatorTypesMap = Db.LoadAllPredatorTypes();   // see helper below

        foreach (var c in chars)
        {
            _specialties.Add(specialtiesMap.TryGetValue(c.Id, out var sp) ? sp : Array.Empty<string>());
            _predatorTypes.Add((PredatorType)c.PredatorType);
        }

        RebuildList();
    }

    // =========================================================================
    // Sidebar list
    // =========================================================================
    void RebuildList()
    {
        CharacterList.Children.Clear();
        for (int i = 0; i < _characters.Count; i++)
        {
            int captured = i;
            var btn = new Button
            {
                Classes = { "charItem" },
                Content = BuildListItemContent(_characters[i]),
            };
            if (i == _selectedIdx) btn.Classes.Add("selected");
            btn.Click += (_, _) => SelectCharacter(captured);
            CharacterList.Children.Add(btn);
        }

        if (_characters.Count == 0) SelectCharacter(-1);
    }

    static StackPanel BuildListItemContent(in Character c)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = c.Name, Foreground = Brushes.White, FontSize = 13 });
        panel.Children.Add(new TextBlock
        {
            Text = $"{ClanName(c.Clan)}  ·  {c.Generation}th",
            Foreground = new SolidColorBrush(Color.Parse("#888")),
            FontSize = 11,
        });
        return panel;
    }

    // =========================================================================
    // Sheet display
    // =========================================================================
    void SelectCharacter(int idx)
    {
        _selectedIdx = idx;

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
        {
            _notes_panel.SetCharacter(_characters[idx].Id, _notes[idx]);
            var awards = Db.LoadAwards(_characters[idx].Id);
            var purchases = Db.LoadPurchases(_characters[idx].Id);
            _xpPanel.SetCharacter(_characters[idx].Id,
                                  _xpEarned[idx], _xpSpent[idx],
                                  awards, purchases);
            _traitsPanel.SetCharacter(_characters[idx].Id, _traits[idx]);
            RefreshSheet(_characters[idx], _disciplines[idx],
                         _predatorTypes[idx], _specialties[idx]);
            _roller.SetCharacter(_characters[idx]);
        }
        else
        {
            _notes_panel.ClearCharacter();
            _xpPanel.ClearCharacter();
            _roller.ClearCharacter();
        }
    }

    void RefreshSheet(in Character c, List<Discipline> disciplines, PredatorType predatorType, string[] specialties)
    {
        // ── MakeStatRow ───────────────────────────────────────────────────────
        // Builds a horizontal row: label | dots | optional ▲ raise button.
        // The raise button only appears when value < max.
        StackPanel MakeStatRow(string label, byte value, byte max,
                               string traitName, XpTraitType traitType,
                               bool isInClan = false)
        {
            var row = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
            };

            row.Children.Add(new TextBlock { Text = label, Classes = { "statLabel" } });
            row.Children.Add(new TextBlock { Text = Dots(value, max), Classes = { "dots" } });

            if (value < max)
            {
                var raiseBtn = new Button
                {
                    Content = "▲",
                    Padding = new Thickness(4, 0),
                    FontSize = 10,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.Parse("#444")),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                };
                raiseBtn.Click += async (_, _) =>
                    await OnRaiseTrait(traitName, traitType, value, isInClan);
                raiseBtn.PointerEntered += (_, _) =>
                    raiseBtn.Foreground = new SolidColorBrush(Color.Parse("#cc2200"));
                raiseBtn.PointerExited += (_, _) =>
                    raiseBtn.Foreground = new SolidColorBrush(Color.Parse("#444"));
                row.Children.Add(raiseBtn);
            }

            return row;
        }

        // ── Header ────────────────────────────────────────────────────────────
        SheetName.Text = c.Name;
        SheetSubtitle.Text = $"{ClanName(c.Clan)}  ·  {c.Generation}th Generation  ·  Humanity {Dots(c.Humanity, 10)}";
        SheetBloodPotency.Text = $"Blood Potency {Dots(c.BloodPotency)}  (max {Rules.MaxBloodPotency(c.Generation)})";
        SheetPredatorType.Text = predatorType == PredatorType.None
            ? ""
            : $"Predator: {PredatorTypes.DisplayName(predatorType)}";

        SpecialtiesPanel.Children.Clear();
        if (specialties.Length == 0)
        {
            SpecialtiesPanel.Children.Add(new TextBlock
            {
                Text = "None",
                Foreground = new SolidColorBrush(Color.Parse("#555")),
                FontSize = 12,
            });
        }
        else
        {
            foreach (string s in specialties)
            {
                SpecialtiesPanel.Children.Add(new TextBlock
                {
                    Text = s,
                    Foreground = new SolidColorBrush(Color.Parse("#aaa")),
                    FontSize = 12,
                });
            }
        }

        // ── Attributes ────────────────────────────────────────────────────────
        var attrPhysical = this.FindControl<StackPanel>("AttrPhysicalPanel")!;
        var attrSocial = this.FindControl<StackPanel>("AttrSocialPanel")!;
        var attrMental = this.FindControl<StackPanel>("AttrMentalPanel")!;

        attrPhysical.Children.Clear();
        attrSocial.Children.Clear();
        attrMental.Children.Clear();

        attrPhysical.Children.Add(MakeStatRow("Strength", c.Strength, 5, "Strength", XpTraitType.Attribute));
        attrPhysical.Children.Add(MakeStatRow("Dexterity", c.Dexterity, 5, "Dexterity", XpTraitType.Attribute));
        attrPhysical.Children.Add(MakeStatRow("Stamina", c.Stamina, 5, "Stamina", XpTraitType.Attribute));

        attrSocial.Children.Add(MakeStatRow("Charisma", c.Charisma, 5, "Charisma", XpTraitType.Attribute));
        attrSocial.Children.Add(MakeStatRow("Manipulation", c.Manipulation, 5, "Manipulation", XpTraitType.Attribute));
        attrSocial.Children.Add(MakeStatRow("Composure", c.Composure, 5, "Composure", XpTraitType.Attribute));

        attrMental.Children.Add(MakeStatRow("Intelligence", c.Intelligence, 5, "Intelligence", XpTraitType.Attribute));
        attrMental.Children.Add(MakeStatRow("Wits", c.Wits, 5, "Wits", XpTraitType.Attribute));
        attrMental.Children.Add(MakeStatRow("Resolve", c.Resolve, 5, "Resolve", XpTraitType.Attribute));

        // ── Skills ────────────────────────────────────────────────────────────
        var skillPhysical = this.FindControl<StackPanel>("SkillPhysicalPanel")!;
        var skillSocial = this.FindControl<StackPanel>("SkillSocialPanel")!;
        var skillMental = this.FindControl<StackPanel>("SkillMentalPanel")!;

        skillPhysical.Children.Clear();
        skillSocial.Children.Clear();
        skillMental.Children.Clear();

        skillPhysical.Children.Add(MakeStatRow("Athletics", c.Athletics, 5, "Athletics", XpTraitType.Skill));
        skillPhysical.Children.Add(MakeStatRow("Brawl", c.Brawl, 5, "Brawl", XpTraitType.Skill));
        skillPhysical.Children.Add(MakeStatRow("Craft", c.Craft, 5, "Craft", XpTraitType.Skill));
        skillPhysical.Children.Add(MakeStatRow("Drive", c.Drive, 5, "Drive", XpTraitType.Skill));
        skillPhysical.Children.Add(MakeStatRow("Firearms", c.Firearms, 5, "Firearms", XpTraitType.Skill));
        skillPhysical.Children.Add(MakeStatRow("Larceny", c.Larceny, 5, "Larceny", XpTraitType.Skill));
        skillPhysical.Children.Add(MakeStatRow("Melee", c.Melee, 5, "Melee", XpTraitType.Skill));
        skillPhysical.Children.Add(MakeStatRow("Stealth", c.Stealth, 5, "Stealth", XpTraitType.Skill));
        skillPhysical.Children.Add(MakeStatRow("Survival", c.Survival, 5, "Survival", XpTraitType.Skill));

        skillSocial.Children.Add(MakeStatRow("Animal Ken", c.AnimalKen, 5, "Animal Ken", XpTraitType.Skill));
        skillSocial.Children.Add(MakeStatRow("Etiquette", c.Etiquette, 5, "Etiquette", XpTraitType.Skill));
        skillSocial.Children.Add(MakeStatRow("Insight", c.Insight, 5, "Insight", XpTraitType.Skill));
        skillSocial.Children.Add(MakeStatRow("Intimidation", c.Intimidation, 5, "Intimidation", XpTraitType.Skill));
        skillSocial.Children.Add(MakeStatRow("Leadership", c.Leadership, 5, "Leadership", XpTraitType.Skill));
        skillSocial.Children.Add(MakeStatRow("Performance", c.Performance, 5, "Performance", XpTraitType.Skill));
        skillSocial.Children.Add(MakeStatRow("Persuasion", c.Persuasion, 5, "Persuasion", XpTraitType.Skill));
        skillSocial.Children.Add(MakeStatRow("Streetwise", c.Streetwise, 5, "Streetwise", XpTraitType.Skill));
        skillSocial.Children.Add(MakeStatRow("Subterfuge", c.Subterfuge, 5, "Subterfuge", XpTraitType.Skill));

        skillMental.Children.Add(MakeStatRow("Academics", c.Academics, 5, "Academics", XpTraitType.Skill));
        skillMental.Children.Add(MakeStatRow("Awareness", c.Awareness, 5, "Awareness", XpTraitType.Skill));
        skillMental.Children.Add(MakeStatRow("Finance", c.Finance, 5, "Finance", XpTraitType.Skill));
        skillMental.Children.Add(MakeStatRow("Investigation", c.Investigation, 5, "Investigation", XpTraitType.Skill));
        skillMental.Children.Add(MakeStatRow("Medicine", c.Medicine, 5, "Medicine", XpTraitType.Skill));
        skillMental.Children.Add(MakeStatRow("Occult", c.Occult, 5, "Occult", XpTraitType.Skill));
        skillMental.Children.Add(MakeStatRow("Politics", c.Politics, 5, "Politics", XpTraitType.Skill));
        skillMental.Children.Add(MakeStatRow("Science", c.Science, 5, "Science", XpTraitType.Skill));
        skillMental.Children.Add(MakeStatRow("Technology", c.Technology, 5, "Technology", XpTraitType.Skill));

        // ── Tracks ────────────────────────────────────────────────────────────
        TrackHealth.Text = DamageTrack(c.AggravatedHealth, c.SuperficialHealth, c.HealthMax);
        TrackWillpower.Text = DamageTrack(c.AggravatedWillpower, c.SuperficialWillpower, c.WillpowerMax);
        int wp = Rules.WoundPenalty(c);
        WoundPenalty.Text = wp < 0 ? $"  wound {wp}" : "";
        TrackHunger.Text = HungerTrack(c.Hunger) + $"  {c.Hunger}/5";

        // ── Disciplines ───────────────────────────────────────────────────────
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
            var inClanSet = new HashSet<DisciplineName>(ClanDisciplines.For(c.Clan));

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
                bool inClan = inClanSet.Contains(d.Name);

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

                // Dots + raise button on one horizontal row
                var dotsRow = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 6,
                };
                dotsRow.Children.Add(new TextBlock
                {
                    Text = Dots(d.Rating),
                    Foreground = new SolidColorBrush(Color.Parse("#cc2200")),
                    FontSize = 13,
                });

                if (d.Rating < 5)
                {
                    byte capturedRating = d.Rating;
                    string capturedName = ClanDisciplines.DisplayName(d.Name);
                    var raiseBtn = new Button
                    {
                        Content = "▲",
                        Padding = new Thickness(4, 0),
                        FontSize = 10,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = new SolidColorBrush(Color.Parse("#444")),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    };
                    raiseBtn.Click += async (_, _) =>
                        await OnRaiseTrait(capturedName,
                                           XpTraitType.InClanDiscipline,
                                           capturedRating,
                                           isInClan: inClan);
                    raiseBtn.PointerEntered += (_, _) =>
                        raiseBtn.Foreground = new SolidColorBrush(Color.Parse("#cc2200"));
                    raiseBtn.PointerExited += (_, _) =>
                        raiseBtn.Foreground = new SolidColorBrush(Color.Parse("#444"));
                    dotsRow.Children.Add(raiseBtn);
                }

                cell.Children.Add(dotsRow);
                Grid.SetColumn(cell, i % 3);
                Grid.SetRow(cell, i / 3);
                grid.Children.Add(cell);
            }

            DisciplinePanel.Children.Add(grid);
        }
    }

    // =========================================================================
    // Mutation helpers
    // =========================================================================
    void Commit(Character updated)
    {
        _characters[_selectedIdx] = updated;
        Db.SaveCharacter(updated);
        if (CharacterList.Children[_selectedIdx] is Button btn)
            btn.Content = BuildListItemContent(updated);
        RefreshSheet(updated, _disciplines[_selectedIdx],
                     _predatorTypes[_selectedIdx], _specialties[_selectedIdx]);

        // Keep roller in sync — wound penalty may have changed
        _roller.RefreshCharacter(updated);
    }

    void SetStatus(string msg) => StatusMsg.Text = msg;

    // =========================================================================
    // Willpower spent event from roller
    // Applies 1 superficial willpower damage and persists.
    // =========================================================================
    void OnRollerWillpowerSpent(object? sender, EventArgs e)
    {
        if (_selectedIdx < 0) return;
        var c = Rules.ApplySuperficialWillpower(_characters[_selectedIdx], 1);
        Commit(c);
        SetStatus("Willpower spent (1 superficial).");
    }

    // =========================================================================
    // Button handlers
    // =========================================================================
    async void OnNewCharacter(object? sender, RoutedEventArgs e)
    {
        var wizard = new CreationWizard(0);
        await wizard.ShowDialog(this);

        if (wizard.Result is Character created)
        {
            var saved = Db.SaveCharacter(created);
            var disciplines = Db.ReplaceAllDisciplines(saved.Id, wizard.PendingDisciplines);
            Db.SavePredatorType(saved.Id, wizard.PendingPredatorType, wizard.PendingSpecialties);
            _characters.Add(saved);
            _disciplines.Add(disciplines);
            _notes.Add(default);
            _specialties.Add(wizard.PendingSpecialties);
            _predatorTypes.Add(wizard.PendingPredatorType);
            _xpEarned.Add(0);
            _xpSpent.Add(0);
            _traits.Add(new List<CharacterTrait>());
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
        if (c.Hunger == 5) result += "  ⚠ Hunger 5 — Frenzy check required!";
        SetStatus(result);
    }

    async void OnDamage(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var dialog = new DamageDialog(DamageDialogMode.Apply);
        await dialog.ShowDialog(this);
        if (dialog.Confirmed) ApplyDamageResult(dialog.Track, dialog.DamageType, dialog.Amount);
    }

    async void OnHeal(object? sender, RoutedEventArgs e)
    {
        if (_selectedIdx < 0) return;
        var dialog = new DamageDialog(DamageDialogMode.Heal);
        await dialog.ShowDialog(this);
        if (dialog.Confirmed) ApplyDamageResult(dialog.Track, dialog.DamageType, dialog.Amount);
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
        SetStatus("Damage updated.");
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
        if (wizard.PendingDisciplines.Count > 0)
        {
            var updated = _characters[_selectedIdx];
            var disciplines = Db.ReplaceAllDisciplines(updated.Id, wizard.PendingDisciplines);
            _disciplines[_selectedIdx] = disciplines;
            RefreshSheet(updated, disciplines,
                         _predatorTypes[_selectedIdx], _specialties[_selectedIdx]);
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
        _notes.RemoveAt(_selectedIdx);
        _specialties.RemoveAt(_selectedIdx);
        _predatorTypes.RemoveAt(_selectedIdx);
        _xpEarned.RemoveAt(_selectedIdx);
        _xpSpent.RemoveAt(_selectedIdx);
        _traits.RemoveAt(_selectedIdx);
        _selectedIdx = -1;
        RebuildList();
        SelectCharacter(-1);
        SetStatus($"'{c.Name}' deleted.");
    }

    void OnNotesSaved(object? sender, CharacterNotes notes)
    {
        if (_selectedIdx < 0) return;
        _notes[_selectedIdx] = notes;
        Db.SaveNotes(_characters[_selectedIdx].Id, notes);
    }

    // =========================================================================
    // XP award event
    // =========================================================================
    void OnXpAwardAdded(object? sender, XpAward award)
    {
        if (_selectedIdx < 0) return;
        _xpEarned[_selectedIdx] += award.Amount;
    }

    // =========================================================================
    // XP spend — raise a trait
    // =========================================================================
    async Task OnRaiseTrait(string traitName, XpTraitType traitType,
                             byte currentRating, bool isInClan = false)
    {
        if (_selectedIdx < 0) return;
        var c = _characters[_selectedIdx];

        // For disciplines, pick in-clan vs out-of-clan cost
        var effectiveType = traitType == XpTraitType.InClanDiscipline && !isInClan
            ? XpTraitType.OutOfClanDiscipline
            : traitType;

        int toRating = currentRating + 1;
        int cost = Rules.XpCost(effectiveType, toRating);
        int available = _xpEarned[_selectedIdx] - _xpSpent[_selectedIdx];

        string desc = $"Raise {traitName}  {currentRating} → {toRating}";
        var dialog = new XpConfirmDialog(desc, cost, available);
        await dialog.ShowDialog(this);
        if (!dialog.Confirmed) return;

        // ── Apply raise ───────────────────────────────────────────────────────
        if (traitType == XpTraitType.Attribute || traitType == XpTraitType.Skill)
        {
            // Attributes and skills live directly in the Character struct
            CreationWizard.ApplySkillBonus(ref c, traitName, 1);
            Commit(c);
        }
        else if (traitType == XpTraitType.InClanDiscipline)
        {
            // Disciplines live in the parallel _disciplines list
            var discList = _disciplines[_selectedIdx];
            string normalizedName = traitName; // traitName is already the display name
            for (int i = 0; i < discList.Count; i++)
            {
                if (ClanDisciplines.DisplayName(discList[i].Name) == normalizedName)
                {
                    var d = discList[i];
                    d.Rating = (byte)Math.Min(d.Rating + 1, 5);
                    discList[i] = d;
                    Db.SaveDiscipline(d);
                    break;
                }
            }
            RefreshSheet(c, discList, _predatorTypes[_selectedIdx], _specialties[_selectedIdx]);
        }
        else if (traitType == XpTraitType.BloodPotency)
        {
            c.BloodPotency = (byte)Math.Min(c.BloodPotency + 1, Rules.MaxBloodPotency(c.Generation));
            Commit(c);
        }
        else if (traitType == XpTraitType.Humanity)
        {
            c.Humanity = (byte)Math.Min(c.Humanity + 1, 10);
            Commit(c);
        }

        // ── Log the purchase ──────────────────────────────────────────────────
        var purchase = Db.AddPurchase(c.Id, effectiveType, traitName,
                                      currentRating, toRating, cost);
        _xpSpent[_selectedIdx] += cost;
        _xpPanel.AddPurchase(purchase);

        SetStatus($"{traitName} raised to {toRating}. {cost} XP spent.");
    }

    // =========================================================================
    // Rendering helpers
    // =========================================================================
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

    async void OnTraitRaiseRequested(object? sender, CharacterTrait t)
    {
        if (_selectedIdx < 0) return;

        int toRating = t.Rating + 1;
        if (toRating > 5) return;

        int cost = t.Category switch
        {
            TraitCategory.Background => TraitDefinitions.BackgroundXpCost(toRating),
            _ => TraitDefinitions.MeritXpCost(toRating),
        };

        int available = _xpEarned[_selectedIdx] - _xpSpent[_selectedIdx];
        string desc = $"Raise {t.Name}  {t.Rating} → {toRating}";

        var dialog = new XpConfirmDialog(desc, cost, available);
        await dialog.ShowDialog(this);
        if (!dialog.Confirmed) return;

        // Update trait rating
        var updated = t;
        updated.Rating = (byte)toRating;
        var saved = Db.SaveTrait(updated);

        // Update parallel list
        var list = _traits[_selectedIdx];
        for (int i = 0; i < list.Count; i++)
            if (list[i].Id == saved.Id) { list[i] = saved; break; }

        // Log XP purchase
        var purchase = Db.AddPurchase(
            _characters[_selectedIdx].Id,
            XpTraitType.Background,   // or Merit
            t.Name, t.Rating, toRating, cost);

        _xpSpent[_selectedIdx] += cost;
        _xpPanel.AddPurchase(purchase);
        _traitsPanel.RefreshTrait(saved);

        SetStatus($"{t.Name} raised to {toRating}. {cost} XP spent.");
    }
}