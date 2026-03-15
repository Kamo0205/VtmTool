using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using VtmTool.Core;
using VtmTool.Core.Enums;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class CreationWizard : Window
{
    // =========================================================================
    // Results
    // =========================================================================
    public Character? Result { get; private set; }
    public List<Discipline> PendingDisciplines { get; } = new(3);

    // =========================================================================
    // Working state
    // =========================================================================
    Character _c = new Character
    {
        Generation = 13,
        BloodPotency = 1,
        Hunger = 1,
        Humanity = 7,
    };

    int _step = 0;

    // Step 0
    TextBox? _nameBox;
    ComboBox? _clanBox;

    // Step 1 — attribute priority dropdowns
    readonly ComboBox[] _attrPriority = new ComboBox[3];

    // Step 2 — getValue delegates from dot pickers, indexed [categoryIndex, attrIndex]
    readonly Func<int>[,] _attrGet = new Func<int>[3, 3];
    readonly TextBlock[] _attrBudgetLabel = new TextBlock[3];

    // Step 3 — skill priority dropdowns
    readonly ComboBox[] _skillPriority = new ComboBox[3];

    // Step 4 — getValue delegates from dot pickers, indexed [categoryIndex, skillIndex]
    readonly Func<int>[,] _skillGet = new Func<int>[3, 9];
    readonly TextBlock[] _skillBudgetLabel = new TextBlock[3];

    // Step 5 — disciplines
    readonly Func<int>[] _discGet = new Func<int>[3];
    TextBlock? _discBudgetLabel;

    // =========================================================================
    // Static data
    // =========================================================================
    static readonly string[][] AttrNames =
    {
        new[] { "Strength",     "Dexterity",    "Stamina"   },
        new[] { "Charisma",     "Manipulation", "Composure" },
        new[] { "Intelligence", "Wits",         "Resolve"   },
    };

    static readonly string[][] SkillNames =
    {
        new[] { "Athletics", "Brawl",      "Craft",  "Drive",          "Firearms",
                "Larceny",   "Melee",      "Stealth","Survival"                     },
        new[] { "Animal Ken","Etiquette",  "Insight","Intimidation",   "Leadership",
                "Performance","Persuasion","Streetwise","Subterfuge"               },
        new[] { "Academics", "Awareness",  "Finance","Investigation",  "Medicine",
                "Occult",    "Politics",   "Science","Technology"                   },
    };

    static readonly string[] CatLabels = { "Physical", "Social", "Mental" };
    static readonly int[] AttrBudgets = { 5, 4, 3 };
    static readonly int[] SkillBudgets = { 8, 6, 4 };

    // =========================================================================
    // Constructors
    // =========================================================================
    public CreationWizard() : this(0) { }    // required by Avalonia XAML loader

    public CreationWizard(int startStep = 0)
    {
        InitializeComponent();
        ShowStep(startStep);
    }

    // Seed the clan before showing the discipline step from an edit context.
    public void SeedClan(Clan clan) => _c.Clan = clan;

    // =========================================================================
    // Step routing
    // =========================================================================
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
            case 5: BuildDisciplineStep(); break;
            case 6: BuildReviewStep(); break;
        }

        BackBtn.IsEnabled = step > 0;
        NextBtn.Content = step == 6 ? "Create" : "Next";
    }

    // =========================================================================
    // Step 0 — Identity
    // =========================================================================
    void BuildIdentityStep()
    {
        StepTitle.Text = "Step 1 — Identity";
        StepHint.Text = "Name your character and choose their clan.";

        StepContent.Children.Add(MakeLabel("Name"));
        _nameBox = new TextBox
        {
            Text = _c.Name ?? "",
            Watermark = "Character name",
            Background = Brush("#222"),
            Foreground = Brush("#e0e0e0"),
            BorderBrush = Brush("#444"),
        };
        StepContent.Children.Add(_nameBox);

        StepContent.Children.Add(MakeLabel("Clan"));
        _clanBox = new ComboBox
        {
            Width = 300,
            Background = Brush("#222"),
            Foreground = Brush("#e0e0e0"),
            BorderBrush = Brush("#444"),
        };
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
    // Step 1 — Attribute priority
    // =========================================================================
    void BuildAttrPrioStep()
    {
        StepTitle.Text = "Step 2 — Attribute Priority";
        StepHint.Text = "Primary gets 5 total dots · Secondary 4 · Tertiary 3";

        string[] labels = { "Primary (total 5)", "Secondary (total 4)", "Tertiary (total 3)" };
        for (int i = 0; i < 3; i++)
        {
            StepContent.Children.Add(MakeLabel(labels[i]));
            var box = new ComboBox
            {
                Width = 300,
                Background = Brush("#222"),
                Foreground = Brush("#e0e0e0"),
                BorderBrush = Brush("#444"),
            };
            foreach (string cat in CatLabels)
                box.Items.Add(new ComboBoxItem { Content = cat });
            box.SelectedIndex = i;
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
    // Step 2 — Attribute dots
    // =========================================================================
    void BuildAttrDotsStep()
    {
        StepTitle.Text = "Step 3 — Assign Attribute Dots";
        StepHint.Text = "Click dots to set · Click active dot to decrement · Min 1";

        for (int p = 0; p < 3; p++)
        {
            int catIdx = _attrPriority[p].SelectedIndex;
            int budget = AttrBudgets[p];
            int pCopy = p;

            // Section header + live budget counter
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            header.Children.Add(MakeSectionLabel(CatLabels[catIdx]));
            var budgetLbl = new TextBlock
            {
                Text = $"0 / {budget}",
                FontSize = 11,
                Foreground = Brush("#666"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _attrBudgetLabel[p] = budgetLbl;
            header.Children.Add(budgetLbl);
            StepContent.Children.Add(header);

            for (int a = 0; a < 3; a++)
            {
                var row = MakeRow();
                row.Children.Add(new TextBlock
                {
                    Text = AttrNames[catIdx][a],
                    Width = 130,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brush("#aaa"),
                });
                row.Children.Add(MakeDotPicker(
                    min: 1, max: 5, initial: 1,
                    onChange: _ => RefreshAttrBudget(pCopy),
                    getValue: out _attrGet[catIdx, a]));
                StepContent.Children.Add(row);
            }

            RefreshAttrBudget(p);
        }
    }

    void RefreshAttrBudget(int p)
    {
        int catIdx = _attrPriority[p].SelectedIndex;
        int budget = AttrBudgets[p];
        int sum = SumGet(_attrGet, catIdx, 3);
        SetBudgetLabel(_attrBudgetLabel[p], sum, budget);
    }

    bool CommitAttrDots()
    {
        for (int p = 0; p < 3; p++)
        {
            int catIdx = _attrPriority[p].SelectedIndex;
            int budget = AttrBudgets[p];
            int sum = SumGet(_attrGet, catIdx, 3);
            if (sum != budget)
            { ShowError($"{CatLabels[catIdx]} attributes must total {budget} (currently {sum})."); return false; }
        }

        _c.Strength = G(_attrGet[0, 0]); _c.Dexterity = G(_attrGet[0, 1]); _c.Stamina = G(_attrGet[0, 2]);
        _c.Charisma = G(_attrGet[1, 0]); _c.Manipulation = G(_attrGet[1, 1]); _c.Composure = G(_attrGet[1, 2]);
        _c.Intelligence = G(_attrGet[2, 0]); _c.Wits = G(_attrGet[2, 1]); _c.Resolve = G(_attrGet[2, 2]);
        return true;
    }

    // =========================================================================
    // Step 3 — Skill priority
    // =========================================================================
    void BuildSkillPrioStep()
    {
        StepTitle.Text = "Step 4 — Skill Priority";
        StepHint.Text = "Primary 8 dots · Secondary 6 · Tertiary 4 · Max 3 per skill";

        string[] labels = { "Primary (8 dots)", "Secondary (6 dots)", "Tertiary (4 dots)" };
        for (int i = 0; i < 3; i++)
        {
            StepContent.Children.Add(MakeLabel(labels[i]));
            var box = new ComboBox
            {
                Width = 300,
                Background = Brush("#222"),
                Foreground = Brush("#e0e0e0"),
                BorderBrush = Brush("#444"),
            };
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
    // Step 4 — Skill dots
    // =========================================================================
    void BuildSkillDotsStep()
    {
        StepTitle.Text = "Step 5 — Assign Skill Dots";
        StepHint.Text = "Click dots to set · Max 3 per skill at creation";

        for (int p = 0; p < 3; p++)
        {
            int catIdx = _skillPriority[p].SelectedIndex;
            int budget = SkillBudgets[p];
            int pCopy = p;

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            header.Children.Add(MakeSectionLabel(CatLabels[catIdx]));
            var budgetLbl = new TextBlock
            {
                Text = $"0 / {budget}",
                FontSize = 11,
                Foreground = Brush("#666"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _skillBudgetLabel[p] = budgetLbl;
            header.Children.Add(budgetLbl);
            StepContent.Children.Add(header);

            for (int s = 0; s < 9; s++)
            {
                var row = MakeRow();
                row.Children.Add(new TextBlock
                {
                    Text = SkillNames[catIdx][s],
                    Width = 130,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brush("#aaa"),
                });
                row.Children.Add(MakeDotPicker(
                    min: 0, max: 3, initial: 0,
                    onChange: _ => RefreshSkillBudget(pCopy),
                    getValue: out _skillGet[catIdx, s]));
                StepContent.Children.Add(row);
            }

            RefreshSkillBudget(p);
        }
    }

    void RefreshSkillBudget(int p)
    {
        int catIdx = _skillPriority[p].SelectedIndex;
        int budget = SkillBudgets[p];
        int sum = SumGet(_skillGet, catIdx, 9);
        SetBudgetLabel(_skillBudgetLabel[p], sum, budget);
    }

    bool CommitSkillDots()
    {
        for (int p = 0; p < 3; p++)
        {
            int catIdx = _skillPriority[p].SelectedIndex;
            int budget = SkillBudgets[p];
            int sum = SumGet(_skillGet, catIdx, 9);
            if (sum != budget)
            { ShowError($"{CatLabels[catIdx]} skills must total {budget} (currently {sum})."); return false; }
        }

        _c.Athletics = G(_skillGet[0, 0]); _c.Brawl = G(_skillGet[0, 1]); _c.Craft = G(_skillGet[0, 2]);
        _c.Drive = G(_skillGet[0, 3]); _c.Firearms = G(_skillGet[0, 4]); _c.Larceny = G(_skillGet[0, 5]);
        _c.Melee = G(_skillGet[0, 6]); _c.Stealth = G(_skillGet[0, 7]); _c.Survival = G(_skillGet[0, 8]);

        _c.AnimalKen = G(_skillGet[1, 0]); _c.Etiquette = G(_skillGet[1, 1]); _c.Insight = G(_skillGet[1, 2]);
        _c.Intimidation = G(_skillGet[1, 3]); _c.Leadership = G(_skillGet[1, 4]); _c.Performance = G(_skillGet[1, 5]);
        _c.Persuasion = G(_skillGet[1, 6]); _c.Streetwise = G(_skillGet[1, 7]); _c.Subterfuge = G(_skillGet[1, 8]);

        _c.Academics = G(_skillGet[2, 0]); _c.Awareness = G(_skillGet[2, 1]); _c.Finance = G(_skillGet[2, 2]);
        _c.Investigation = G(_skillGet[2, 3]); _c.Medicine = G(_skillGet[2, 4]); _c.Occult = G(_skillGet[2, 5]);
        _c.Politics = G(_skillGet[2, 6]); _c.Science = G(_skillGet[2, 7]); _c.Technology = G(_skillGet[2, 8]);
        return true;
    }

    // =========================================================================
    // Step 5 — Disciplines
    // =========================================================================
    void BuildDisciplineStep()
    {
        StepTitle.Text = "Step 6 — Disciplines";
        StepHint.Text = "Distribute 3 dots across your clan's disciplines (V5 p.152).";

        var inClan = ClanDisciplines.For(_c.Clan);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        header.Children.Add(MakeSectionLabel("IN-CLAN DISCIPLINES"));
        _discBudgetLabel = new TextBlock
        {
            Text = "0 / 3",
            FontSize = 11,
            Foreground = Brush("#666"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(_discBudgetLabel);
        StepContent.Children.Add(header);

        for (int i = 0; i < 3; i++)
        {
            var row = MakeRow();
            row.Children.Add(new TextBlock
            {
                Text = ClanDisciplines.DisplayName(inClan[i]),
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#aaa"),
            });
            row.Children.Add(MakeDotPicker(
                min: 0, max: 5, initial: 1,
                onChange: _ => RefreshDiscBudget(),
                getValue: out _discGet[i]));
            StepContent.Children.Add(row);
        }

        RefreshDiscBudget();
    }

    void RefreshDiscBudget()
    {
        int sum = 0;
        for (int i = 0; i < 3; i++) sum += _discGet[i]?.Invoke() ?? 0;
        SetBudgetLabel(_discBudgetLabel, sum, 3);
    }

    bool CommitDisciplines()
    {
        int sum = 0;
        for (int i = 0; i < 3; i++) sum += _discGet[i]?.Invoke() ?? 0;
        if (sum != 3)
        { ShowError($"Disciplines must total 3 dots (currently {sum})."); return false; }

        var inClan = ClanDisciplines.For(_c.Clan);
        PendingDisciplines.Clear();
        for (int i = 0; i < 3; i++)
        {
            byte rating = (byte)(_discGet[i]?.Invoke() ?? 0);
            if (rating == 0) continue;
            PendingDisciplines.Add(new Discipline
            {
                CharacterId = 0,
                Name = inClan[i],
                Rating = rating,
            });
        }
        return true;
    }

    // =========================================================================
    // Step 6 — Review
    // =========================================================================
    void BuildReviewStep()
    {
        StepTitle.Text = "Step 7 — Review";
        StepHint.Text = "Confirm to create the character.";

        void Row(string lbl, string val)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = lbl, Width = 160, Foreground = Brush("#aaa"), FontSize = 12 });
            row.Children.Add(new TextBlock { Text = val, Foreground = Brush("#e0e0e0"), FontSize = 12 });
            StepContent.Children.Add(row);
        }

        Row("Name", _c.Name ?? "");
        Row("Clan", ClanDisplayName(_c.Clan));
        Row("Generation", $"{_c.Generation}th");
        Row("Blood Potency", $"{_c.BloodPotency}");
        Row("Humanity", $"{_c.Humanity}");
        Row("Hunger", $"{_c.Hunger}");
        Divider();
        Row("STR / DEX / STA", $"{_c.Strength} / {_c.Dexterity} / {_c.Stamina}");
        Row("CHA / MAN / COM", $"{_c.Charisma} / {_c.Manipulation} / {_c.Composure}");
        Row("INT / WIT / RES", $"{_c.Intelligence} / {_c.Wits} / {_c.Resolve}");
        Row("Health (max)", $"{_c.HealthMax}");
        Row("Willpower (max)", $"{_c.WillpowerMax}");

        if (PendingDisciplines.Count > 0)
        {
            Divider();
            foreach (var d in PendingDisciplines)
                Row(ClanDisciplines.DisplayName(d.Name),
                    new string('●', d.Rating) + new string('○', 5 - d.Rating));
        }
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
            5 => CommitDisciplines(),
            6 => Finish(),
            _ => true
        };
        if (ok && _step < 6) ShowStep(_step + 1);
    }

    void OnBack(object? sender, RoutedEventArgs e) { if (_step > 0) ShowStep(_step - 1); }
    void OnCancel(object? sender, RoutedEventArgs e) => Close();
    bool Finish() { Result = _c; Close(); return true; }

    // =========================================================================
    // Dot picker
    //
    // Returns a StackPanel of `max` clickable dot buttons.
    // Clicking dot i+1 sets value to i+1.
    // Clicking the active dot decrements by 1 (clamped to min).
    // onChange fires after every change — used to update budget labels.
    // getValue is a delegate the caller stores to read the live value.
    // =========================================================================
    static StackPanel MakeDotPicker(int min, int max, int initial,
                                     Action<int> onChange, out Func<int> getValue)
    {
        int current = Math.Clamp(initial, min, max);
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        var buttons = new Button[max];

        void Redraw()
        {
            for (int i = 0; i < max; i++)
            {
                buttons[i].Content = i < current ? "●" : "○";
                buttons[i].Foreground = new SolidColorBrush(
                    i < current
                        ? global::Avalonia.Media.Color.Parse("#cc2200")
                        : global::Avalonia.Media.Color.Parse("#444"));
            }
        }

        for (int i = 0; i < max; i++)
        {
            int dotRating = i + 1;
            var btn = new Button
            {
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            btn.Click += (_, _) =>
            {
                current = (current == dotRating && dotRating > min) ? dotRating - 1 : dotRating;
                current = Math.Clamp(current, min, max);
                Redraw();
                onChange(current);
            };
            buttons[i] = btn;
            panel.Children.Add(btn);
        }

        Redraw();
        getValue = () => current;
        return panel;
    }

    // =========================================================================
    // Small helpers
    // =========================================================================
    static StackPanel MakeRow() => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 12,
        Margin = new Thickness(0, 2, 0, 2),
    };

    static TextBlock MakeLabel(string text) => new TextBlock
    {
        Text = text,
        Foreground = Brush("#aaa"),
        FontSize = 12,
        Margin = new Thickness(0, 8, 0, 2),
    };

    static TextBlock MakeSectionLabel(string text) => new TextBlock
    {
        Text = text,
        Foreground = Brush("#b00000"),
        FontSize = 11,
        FontWeight = FontWeight.Bold,
        Margin = new Thickness(0, 12, 0, 4),
        LetterSpacing = 1,
    };

    static void SetBudgetLabel(TextBlock? lbl, int sum, int budget)
    {
        if (lbl == null) return;
        lbl.Text = $"{sum} / {budget}";
        lbl.Foreground = sum == budget ? Brush("#00cc66") : Brush("#666");
    }

    void Divider() => StepContent.Children.Add(new Separator
    {
        Height = 1,
        Background = Brush("#333"),
        Margin = new Thickness(0, 8),
    });

    void ShowError(string msg)
    {
        if (StepContent.Children.Count > 0 &&
            StepContent.Children[^1] is TextBlock { Tag: "error" } prev)
            StepContent.Children.Remove(prev);

        StepContent.Children.Add(new TextBlock
        {
            Text = msg,
            Foreground = Brush("#cc0000"),
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
            Tag = "error",
        });
    }

    // Returns a SolidColorBrush from a hex string
    static ISolidColorBrush Brush(string hex) =>
        new SolidColorBrush(global::Avalonia.Media.Color.Parse(hex));

    // Invoke a Func<int> delegate and cast to byte
    static byte G(Func<int>? f) => (byte)(f?.Invoke() ?? 0);

    // Sum a row of Func<int> delegates from a 2D array
    static int SumGet(Func<int>[,] arr, int row, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++) sum += arr[row, i]?.Invoke() ?? 0;
        return sum;
    }

    static string ClanDisplayName(Clan c) => c switch
    {
        Clan.Banu_Haqim => "Banu Haqim",
        _ => c.ToString()
    };
}