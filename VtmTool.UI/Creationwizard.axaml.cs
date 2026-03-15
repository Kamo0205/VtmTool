using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using VtmTool.Core.Enums;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class CreationWizard : Window
{
    public Character? Result { get; private set; }

    // Working character — built up step by step
    Character _c = new Character
    {
        Generation = 13,
        BloodPotency = 1,
        Hunger = 1,
        Humanity = 7,
    };

    // Step index
    int _step = 0;

    // Per-step input references (cleared and rebuilt on each step)
    TextBox? _nameBox;
    ComboBox? _clanBox;

    // Attribute NumericUpDowns — indexed [cat][attr] where cat: 0=phys,1=soc,2=ment
    readonly NumericUpDown[,] _attrBoxes = new NumericUpDown[3, 3];
    readonly ComboBox[] _attrPriority = new ComboBox[3];

    // Skill NumericUpDowns — indexed [cat][skill] 0–8
    readonly NumericUpDown[,] _skillBoxes = new NumericUpDown[3, 9];
    readonly ComboBox[] _skillPriority = new ComboBox[3];

    static readonly string[][] AttrNames =
    {
        new[] { "Strength",     "Dexterity",    "Stamina"   },
        new[] { "Charisma",     "Manipulation", "Composure" },
        new[] { "Intelligence", "Wits",         "Resolve"   },
    };

    static readonly string[][] SkillNames =
    {
        new[] { "Athletics", "Brawl",    "Craft",  "Drive",    "Firearms",
                "Larceny",   "Melee",    "Stealth","Survival" },
        new[] { "Animal Ken","Etiquette","Insight","Intimidation","Leadership",
                "Performance","Persuasion","Streetwise","Subterfuge" },
        new[] { "Academics", "Awareness","Finance","Investigation","Medicine",
                "Occult",    "Politics", "Science","Technology" },
    };

    static readonly string[] CatLabels = { "Physical", "Social", "Mental" };
    static readonly int[] AttrBudgets = { 5, 4, 3 };
    static readonly int[] SkillBudgets = { 8, 6, 4 };

    public CreationWizard()
    {
        InitializeComponent();
        ShowStep(0);
    }

    // Step routing
    void ShowStep(int step)
    {
        _step = step;
        StepContent.Children.Clear();

        switch (step)
        {
            case 0: BuildIdentityStep(); break;
            case 1: BuildAttrPrioStep(); break;
            case 2: BuildAttrDotsStep(); break;
            case 3: BuildSkillPrioStep(); break;
            case 4: BuildSkillDotsStep(); break;
            case 5: BuildReviewStep(); break;
        }

        BackBtn.IsEnabled = step > 0;
        NextBtn.Content = step == 5 ? "Create" : "Next";
    }

    // =========================================================================
    // Step 0 — Identity (Name + Clan)
    // =========================================================================
    void BuildIdentityStep()
    {
        StepTitle.Text = "Step 1 — Identity";
        StepHint.Text = "Name your character and choose their clan.";

        StepContent.Children.Add(Label("Name"));
        _nameBox = new TextBox
        {
            Text = _c.Name ?? "",
            Background = Color("#222"),
            Foreground = Color("#e0e0e0"),
            BorderBrush = Color("#444"),
            Watermark = "Character name",
        };
        StepContent.Children.Add(_nameBox);

        StepContent.Children.Add(Label("Clan"));
        _clanBox = new ComboBox { Width = 300, Background = Color("#222"), Foreground = Color("#e0e0e0") };
        foreach (Clan clan in Enum.GetValues<Clan>())
            _clanBox.Items.Add(new ComboBoxItem { Content = ClanDisplayName(clan), Tag = clan });
        _clanBox.SelectedIndex = (int)_c.Clan;
        StepContent.Children.Add(_clanBox);
    }

    bool CommitIdentity()
    {
        string name = _nameBox?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) { ShowError("Name is required."); return false; }
        _c.Name = name;
        if (_clanBox?.SelectedItem is ComboBoxItem item && item.Tag is Clan clan)
            _c.Clan = clan;
        return true;
    }

    // =========================================================================
    // Step 1 — Attribute priority selection
    // =========================================================================
    void BuildAttrPrioStep()
    {
        StepTitle.Text = "Step 2 — Attribute Priority";
        StepHint.Text = "Primary gets 5 total dots · Secondary 4 · Tertiary 3";

        string[] prioLabels = { "Primary (total 5)", "Secondary (total 4)", "Tertiary (total 3)" };
        for (int i = 0; i < 3; i++)
        {
            StepContent.Children.Add(Label(prioLabels[i]));
            var box = new ComboBox { Width = 300, Background = Color("#222"), Foreground = Color("#e0e0e0") };
            foreach (string cat in CatLabels)
                box.Items.Add(new ComboBoxItem { Content = cat });
            box.SelectedIndex = i; // default: Phys/Soc/Ment
            _attrPriority[i] = box;
            StepContent.Children.Add(box);
        }
    }

    bool CommitAttrPriority()
    {
        var chosen = new HashSet<int>();
        for (int i = 0; i < 3; i++)
        {
            int idx = _attrPriority[i].SelectedIndex;
            if (idx < 0 || !chosen.Add(idx))
            { ShowError("Each category must be chosen exactly once."); return false; }
        }
        return true;
    }

    // =========================================================================
    // Step 2 — Attribute dot assignment
    // =========================================================================
    void BuildAttrDotsStep()
    {
        StepTitle.Text = "Step 3 — Assign Attribute Dots";
        StepHint.Text = "Each attribute minimum 1 · Sum must match priority budget";

        for (int p = 0; p < 3; p++)
        {
            int catIdx = _attrPriority[p].SelectedIndex;
            int budget = AttrBudgets[p];
            string cat = CatLabels[catIdx];

            StepContent.Children.Add(SectionLabel($"{cat}  (total = {budget})"));

            for (int a = 0; a < 3; a++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                row.Children.Add(new TextBlock
                {
                    Text = AttrNames[catIdx][a],
                    Width = 130,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Color("#aaa"),
                });
                var spin = new NumericUpDown
                {
                    Minimum = 1,
                    Maximum = 5,
                    Value = 1,
                    Width = 80,
                    Background = Color("#222"),
                    Foreground = Color("#e0e0e0"),
                };
                _attrBoxes[catIdx, a] = spin;
                row.Children.Add(spin);
                StepContent.Children.Add(row);
            }
        }
    }

    bool CommitAttrDots()
    {
        for (int p = 0; p < 3; p++)
        {
            int catIdx = _attrPriority[p].SelectedIndex;
            int budget = AttrBudgets[p];
            int sum = 0;
            for (int a = 0; a < 3; a++)
                sum += (int)(_attrBoxes[catIdx, a].Value ?? 1);
            if (sum != budget)
            {
                ShowError($"{CatLabels[catIdx]} attributes must total {budget} (currently {sum}).");
                return false;
            }
        }

        // Write into character
        _c.Strength = V(_attrBoxes[0, 0]); _c.Dexterity = V(_attrBoxes[0, 1]); _c.Stamina = V(_attrBoxes[0, 2]);
        _c.Charisma = V(_attrBoxes[1, 0]); _c.Manipulation = V(_attrBoxes[1, 1]); _c.Composure = V(_attrBoxes[1, 2]);
        _c.Intelligence = V(_attrBoxes[2, 0]); _c.Wits = V(_attrBoxes[2, 1]); _c.Resolve = V(_attrBoxes[2, 2]);
        return true;
    }

    // =========================================================================
    // Step 3 — Skill priority selection (same pattern as attr priority)
    // =========================================================================
    void BuildSkillPrioStep()
    {
        StepTitle.Text = "Step 4 — Skill Priority";
        StepHint.Text = "Primary 8 dots · Secondary 6 · Tertiary 4 · Max 3 per skill";

        string[] prioLabels = { "Primary (8 dots)", "Secondary (6 dots)", "Tertiary (4 dots)" };
        for (int i = 0; i < 3; i++)
        {
            StepContent.Children.Add(Label(prioLabels[i]));
            var box = new ComboBox { Width = 300, Background = Color("#222"), Foreground = Color("#e0e0e0") };
            foreach (string cat in CatLabels)
                box.Items.Add(new ComboBoxItem { Content = cat });
            box.SelectedIndex = i;
            _skillPriority[i] = box;
            StepContent.Children.Add(box);
        }
    }

    bool CommitSkillPriority()
    {
        var chosen = new HashSet<int>();
        for (int i = 0; i < 3; i++)
        {
            int idx = _skillPriority[i].SelectedIndex;
            if (idx < 0 || !chosen.Add(idx))
            { ShowError("Each category must be chosen exactly once."); return false; }
        }
        return true;
    }

    // =========================================================================
    // Step 4 — Skill dot assignment
    // =========================================================================
    void BuildSkillDotsStep()
    {
        StepTitle.Text = "Step 5 — Assign Skill Dots";
        StepHint.Text = "Skills start at 0 · Max 3 per skill at creation";

        for (int p = 0; p < 3; p++)
        {
            int catIdx = _skillPriority[p].SelectedIndex;
            int budget = SkillBudgets[p];
            string cat = CatLabels[catIdx];

            StepContent.Children.Add(SectionLabel($"{cat}  (total = {budget})"));

            for (int s = 0; s < 9; s++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                row.Children.Add(new TextBlock
                {
                    Text = SkillNames[catIdx][s],
                    Width = 130,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Color("#aaa"),
                });
                var spin = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 3,
                    Value = 0,
                    Width = 80,
                    Background = Color("#222"),
                    Foreground = Color("#e0e0e0"),
                };
                _skillBoxes[catIdx, s] = spin;
                row.Children.Add(spin);
                StepContent.Children.Add(row);
            }
        }
    }

    bool CommitSkillDots()
    {
        for (int p = 0; p < 3; p++)
        {
            int catIdx = _skillPriority[p].SelectedIndex;
            int budget = SkillBudgets[p];
            int sum = 0;
            for (int s = 0; s < 9; s++)
                sum += (int)(_skillBoxes[catIdx, s].Value ?? 0);
            if (sum != budget)
            {
                ShowError($"{CatLabels[catIdx]} skills must total {budget} (currently {sum}).");
                return false;
            }
        }

        _c.Athletics = V(_skillBoxes[0, 0]); _c.Brawl = V(_skillBoxes[0, 1]); _c.Craft = V(_skillBoxes[0, 2]);
        _c.Drive = V(_skillBoxes[0, 3]); _c.Firearms = V(_skillBoxes[0, 4]); _c.Larceny = V(_skillBoxes[0, 5]);
        _c.Melee = V(_skillBoxes[0, 6]); _c.Stealth = V(_skillBoxes[0, 7]); _c.Survival = V(_skillBoxes[0, 8]);

        _c.AnimalKen = V(_skillBoxes[1, 0]); _c.Etiquette = V(_skillBoxes[1, 1]); _c.Insight = V(_skillBoxes[1, 2]);
        _c.Intimidation = V(_skillBoxes[1, 3]); _c.Leadership = V(_skillBoxes[1, 4]); _c.Performance = V(_skillBoxes[1, 5]);
        _c.Persuasion = V(_skillBoxes[1, 6]); _c.Streetwise = V(_skillBoxes[1, 7]); _c.Subterfuge = V(_skillBoxes[1, 8]);

        _c.Academics = V(_skillBoxes[2, 0]); _c.Awareness = V(_skillBoxes[2, 1]); _c.Finance = V(_skillBoxes[2, 2]);
        _c.Investigation = V(_skillBoxes[2, 3]); _c.Medicine = V(_skillBoxes[2, 4]); _c.Occult = V(_skillBoxes[2, 5]);
        _c.Politics = V(_skillBoxes[2, 6]); _c.Science = V(_skillBoxes[2, 7]); _c.Technology = V(_skillBoxes[2, 8]);
        return true;
    }

    // =========================================================================
    // Step 5 — Review summary
    // =========================================================================
    void BuildReviewStep()
    {
        StepTitle.Text = "Step 6 — Review";
        StepHint.Text = "Confirm to create the character.";

        void Row(string label, string value)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = label, Width = 130, Foreground = Color("#aaa"), FontSize = 12 });
            row.Children.Add(new TextBlock { Text = value, Foreground = Color("#e0e0e0"), FontSize = 12 });
            StepContent.Children.Add(row);
        }

        Row("Name", _c.Name ?? "");
        Row("Clan", ClanDisplayName(_c.Clan));
        Row("Generation", $"{_c.Generation}th");
        Row("Blood Potency", $"{_c.BloodPotency}");
        Row("Humanity", $"{_c.Humanity}");
        Row("Hunger", $"{_c.Hunger}");
        StepContent.Children.Add(new Avalonia.Controls.Separator { Height = 1, Background = Color("#333"), Margin = new Avalonia.Thickness(0, 8) });
        Row("STR / DEX / STA", $"{_c.Strength} / {_c.Dexterity} / {_c.Stamina}");
        Row("CHA / MAN / COM", $"{_c.Charisma} / {_c.Manipulation} / {_c.Composure}");
        Row("INT / WIT / RES", $"{_c.Intelligence} / {_c.Wits} / {_c.Resolve}");
        Row("Health (max)", $"{_c.HealthMax}");
        Row("Willpower (max)", $"{_c.WillpowerMax}");
    }

    // =========================================================================
    // Navigation
    // =========================================================================
    void OnNext(object? sender, RoutedEventArgs e)
    {
        bool ok = _step switch
        {
            0 => CommitIdentity(),
            1 => CommitAttrPriority(),
            2 => CommitAttrDots(),
            3 => CommitSkillPriority(),
            4 => CommitSkillDots(),
            5 => Finish(),
            _ => true
        };
        if (ok && _step < 5) ShowStep(_step + 1);
    }

    void OnBack(object? sender, RoutedEventArgs e)
    {
        if (_step > 0) ShowStep(_step - 1);
    }

    void OnCancel(object? sender, RoutedEventArgs e) => Close();

    bool Finish()
    {
        Result = _c;
        Close();
        return true;
    }

    // =========================================================================
    // UI helpers
    // =========================================================================

    static TextBlock Label(string text) => new TextBlock
    {
        Text = text,
        Foreground = new SolidColorBrush(global::Avalonia.Media.Color.Parse("#aaa")),
        FontSize = 12,
        Margin = new Avalonia.Thickness(0, 8, 0, 2),
    };

    static TextBlock SectionLabel(string text) => new TextBlock
    {
        Text = text,
        Foreground = new SolidColorBrush(global::Avalonia.Media.Color.Parse("#b00000")),
        FontSize = 11,
        FontWeight = FontWeight.Bold,
        Margin = new Avalonia.Thickness(0, 12, 0, 4),
        LetterSpacing = 1,
    };

    void ShowError(string msg)
    {
        // Remove previous error if any
        if (StepContent.Children.Count > 0 &&
            StepContent.Children[^1] is TextBlock { Tag: "error" } prev)
            StepContent.Children.Remove(prev);

        StepContent.Children.Add(new TextBlock
        {
            Text = msg,
            Foreground = new SolidColorBrush(global::Avalonia.Media.Color.Parse("#cc0000")),
            FontSize = 12,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
            Tag = "error",
        });
    }

    static ISolidColorBrush Color(string hex) =>
        new SolidColorBrush(global::Avalonia.Media.Color.Parse(hex));

    static byte V(NumericUpDown? box) => (byte)(box?.Value ?? 0);

    static string ClanDisplayName(Clan c) => c switch
    {
        Clan.Banu_Haqim => "Banu Haqim",
        _ => c.ToString()
    };
}