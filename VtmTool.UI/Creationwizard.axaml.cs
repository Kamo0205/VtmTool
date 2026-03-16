using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    public string[] PendingSpecialties { get; private set; } = Array.Empty<string>();
    public PredatorType PendingPredatorType { get; private set; } = PredatorType.None;

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

    // Step 0 — Identity
    TextBox? _nameBox;
    ComboBox? _clanBox;

    // Step 1 — Predator Type
    PredatorType _selectedPredatorType = PredatorType.None;
    DisciplineName? _predDiscChoice = null;
    string? _predSkillBChoice = null;
    Border? _selectedPredCard = null;
    DisciplineName? _pendingPredDisc = null;

    // Step 2 — Attribute priority
    readonly ComboBox[] _attrPriority = new ComboBox[3];
    readonly Func<int>[,] _attrGet = new Func<int>[3, 3];
    readonly TextBlock[] _attrBudgetLabel = new TextBlock[3];

    // Step 4 — Skill priority
    readonly ComboBox[] _skillPriority = new ComboBox[3];
    readonly Func<int>[,] _skillGet = new Func<int>[3, 9];
    readonly TextBlock[] _skillBudgetLabel = new TextBlock[3];

    // Step 6 — Disciplines
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
        new[] { "Athletics","Brawl",    "Craft",  "Drive",         "Firearms",
                "Larceny",  "Melee",    "Stealth","Survival"                   },
        new[] { "Animal Ken","Etiquette","Insight","Intimidation",  "Leadership",
                "Performance","Persuasion","Streetwise","Subterfuge"           },
        new[] { "Academics","Awareness","Finance","Investigation",  "Medicine",
                "Occult",   "Politics", "Science","Technology"                 },
    };

    static readonly string[] CatLabels = { "Physical", "Social", "Mental" };
    static readonly int[] AttrBudgets = { 5, 4, 3 };
    static readonly int[] SkillBudgets = { 8, 6, 4 };

    // =========================================================================
    // Constructors
    // =========================================================================
    public CreationWizard() : this(0) { }

    public CreationWizard(int startStep = 0)
    {
        InitializeComponent();
        ShowStep(startStep);
    }

    public void SeedClan(Clan clan) => _c.Clan = clan;

    // =========================================================================
    // Step routing  (0–7, total 8 steps)
    // =========================================================================
    void ShowStep(int step)
    {
        _step = step;
        StepContent.Children.Clear();

        switch (step)
        {
            case 0: BuildIdentityStep(); break;
            case 1: BuildPredatorTypeStep(); break;
            case 2: BuildAttrPrioStep(); break;
            case 3: BuildAttrDotsStep(); break;
            case 4: BuildSkillPrioStep(); break;
            case 5: BuildSkillDotsStep(); break;
            case 6: BuildDisciplineStep(); break;
            case 7: BuildReviewStep(); break;
        }

        BackBtn.IsEnabled = step > 0;
        NextBtn.Content = step == 7 ? "Create" : "Next";
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
    // Step 1 — Predator Type
    // =========================================================================
    void BuildPredatorTypeStep()
    {
        StepTitle.Text = "Step 2 — Predator Type";
        StepHint.Text = "How does your vampire hunt? Highlighted types match your clan's disciplines.";

        var inClan = new HashSet<DisciplineName>(ClanDisciplines.For(_c.Clan));

        // Card list lives in a ScrollViewer so the choice area can appear below it
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 340,
        };
        var list = new StackPanel { Spacing = 5 };

        foreach (var pt in PredatorTypes.All)
        {
            bool compatible = false;
            foreach (var d in pt.DisciplineChoice)
                if (inClan.Contains(d)) { compatible = true; break; }

            list.Children.Add(BuildPredatorCard(pt, compatible));
        }

        scroll.Content = list;
        StepContent.Children.Add(scroll);
    }

    Border BuildPredatorCard(PredatorTypeData pt, bool compatible)
    {
        string borderHex = compatible ? "#5a0000" : "#2a2a2a";
        string nameHex = compatible ? "#cc2200" : "#888";

        var card = new Border
        {
            Background = Brush("#1a1a1a"),
            BorderBrush = Brush(borderHex),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(12, 8),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(new TextBlock
        {
            Text = PredatorTypes.DisplayName(pt.Type),
            Foreground = Brush(nameHex),
            FontSize = 13,
            FontWeight = FontWeight.Bold,
        });
        stack.Children.Add(new TextBlock
        {
            Text = pt.Description,
            Foreground = Brush("#666"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        });

        string skillB = pt.SkillBChoice != null
            ? $"{pt.SkillBChoice[0]} or {pt.SkillBChoice[1]}"
            : pt.SkillBonuses[1];
        string discList = string.Join(" or ",
            Array.ConvertAll(pt.DisciplineChoice, d => ClanDisciplines.DisplayName(d)));

        stack.Children.Add(new TextBlock
        {
            Text = $"+1 {pt.SkillBonuses[0]}  ·  +1 {skillB}  ·  +1 {discList}  ·  {pt.Specialty}",
            Foreground = Brush("#aaa"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        });

        if (pt.ReducesHumanity)
            stack.Children.Add(new TextBlock
            {
                Text = "⚠ Reduces starting Humanity by 1",
                Foreground = Brush("#cc4400"),
                FontSize = 11,
            });

        card.Child = stack;
        card.PointerPressed += (_, _) => SelectPredatorCard(card, pt);

        if (_selectedPredatorType == pt.Type)
            SelectPredatorCard(card, pt);

        return card;
    }

    void SelectPredatorCard(Border card, PredatorTypeData pt)
    {
        if (_selectedPredCard != null)
        {
            _selectedPredCard.Background = Brush("#1a1a1a");
            _selectedPredCard.BorderThickness = new Thickness(1);
        }

        _selectedPredCard = card;
        _selectedPredatorType = pt.Type;
        card.Background = Brush("#2e0000");
        card.BorderThickness = new Thickness(2);

        // Remove any choice UI added by a previous selection
        while (StepContent.Children.Count > 1)
            StepContent.Children.RemoveAt(StepContent.Children.Count - 1);

        // Discipline choice
        if (pt.DisciplineChoice.Length > 1)
        {
            StepContent.Children.Add(MakeSectionLabel("DISCIPLINE BONUS — Choose one:"));
            var panel = new StackPanel { Spacing = 6 };
            bool first = true;
            foreach (var d in pt.DisciplineChoice)
            {
                var dn = d; // capture
                var rb = new RadioButton
                {
                    Content = ClanDisciplines.DisplayName(d),
                    Tag = d,
                    Foreground = Brush("#e0e0e0"),
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 12,
                    GroupName = "DiscChoice",
                };
                if (first) { first = false; rb.IsChecked = true; _predDiscChoice = d; }
                rb.IsCheckedChanged += (s, _) =>
                {
                    if (s is RadioButton r && r.IsChecked == true && r.Tag is DisciplineName x)
                        _predDiscChoice = x;
                };
                panel.Children.Add(rb);
            }
            StepContent.Children.Add(panel);
        }
        else
        {
            _predDiscChoice = pt.DisciplineChoice[0];
        }

        // Skill B choice (Alleycat only)
        if (pt.SkillBChoice != null)
        {
            StepContent.Children.Add(MakeSectionLabel("SKILL BONUS — Choose one:"));
            var panel = new StackPanel { Spacing = 6 };
            bool first = true;
            foreach (var s in pt.SkillBChoice)
            {
                var sk = s; // capture
                var rb = new RadioButton
                {
                    Content = s,
                    Tag = s,
                    Foreground = Brush("#e0e0e0"),
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 12,
                    GroupName = "SkillBChoice",
                };
                if (first) { first = false; rb.IsChecked = true; _predSkillBChoice = s; }
                rb.IsCheckedChanged += (sender, _) =>
                {
                    if (sender is RadioButton r && r.IsChecked == true && r.Tag is string x)
                        _predSkillBChoice = x;
                };
                panel.Children.Add(rb);
            }
            StepContent.Children.Add(panel);
        }
        else
        {
            _predSkillBChoice = null;
        }
    }

    bool CommitPredatorType()
    {
        if (_selectedPredatorType == PredatorType.None)
        { ShowError("Choose a Predator Type to continue."); return false; }

        var pt = PredatorTypes.Get(_selectedPredatorType)!.Value;

        // Apply skill bonuses to working character
        string skillB = _predSkillBChoice ?? pt.SkillBonuses[1];
        ApplySkillBonus(ref _c, pt.SkillBonuses[0], 1);
        ApplySkillBonus(ref _c, skillB, 1);

        if (pt.ReducesHumanity)
            _c.Humanity = (byte)Math.Max(_c.Humanity - 1, 0);

        PendingPredatorType = _selectedPredatorType;
        PendingSpecialties = new[] { pt.Specialty };
        _pendingPredDisc = _predDiscChoice;
        return true;
    }

    public static void ApplySkillBonus(ref Character c, string skill, int amount)
    {
        switch (skill)
        {
            case "Athletics": c.Athletics = Cap(c.Athletics, amount); break;
            case "Brawl": c.Brawl = Cap(c.Brawl, amount); break;
            case "Craft": c.Craft = Cap(c.Craft, amount); break;
            case "Drive": c.Drive = Cap(c.Drive, amount); break;
            case "Firearms": c.Firearms = Cap(c.Firearms, amount); break;
            case "Larceny": c.Larceny = Cap(c.Larceny, amount); break;
            case "Melee": c.Melee = Cap(c.Melee, amount); break;
            case "Stealth": c.Stealth = Cap(c.Stealth, amount); break;
            case "Survival": c.Survival = Cap(c.Survival, amount); break;
            case "Animal Ken": c.AnimalKen = Cap(c.AnimalKen, amount); break;
            case "Etiquette": c.Etiquette = Cap(c.Etiquette, amount); break;
            case "Insight": c.Insight = Cap(c.Insight, amount); break;
            case "Intimidation": c.Intimidation = Cap(c.Intimidation, amount); break;
            case "Leadership": c.Leadership = Cap(c.Leadership, amount); break;
            case "Performance": c.Performance = Cap(c.Performance, amount); break;
            case "Persuasion": c.Persuasion = Cap(c.Persuasion, amount); break;
            case "Streetwise": c.Streetwise = Cap(c.Streetwise, amount); break;
            case "Subterfuge": c.Subterfuge = Cap(c.Subterfuge, amount); break;
            case "Academics": c.Academics = Cap(c.Academics, amount); break;
            case "Awareness": c.Awareness = Cap(c.Awareness, amount); break;
            case "Finance": c.Finance = Cap(c.Finance, amount); break;
            case "Investigation": c.Investigation = Cap(c.Investigation, amount); break;
            case "Medicine": c.Medicine = Cap(c.Medicine, amount); break;
            case "Occult": c.Occult = Cap(c.Occult, amount); break;
            case "Politics": c.Politics = Cap(c.Politics, amount); break;
            case "Science": c.Science = Cap(c.Science, amount); break;
            case "Technology": c.Technology = Cap(c.Technology, amount); break;
        }
        static byte Cap(byte current, int add) => (byte)Math.Min(current + add, 5);
    }

    // =========================================================================
    // Step 2 — Attribute priority
    // =========================================================================
    void BuildAttrPrioStep()
    {
        StepTitle.Text = "Step 3 — Attribute Priority";
        StepHint.Text = "Primary gets 5 total dots · Secondary 4 · Tertiary 3";

        string[] labels = { "Primary (total 5)", "Secondary (total 4)", "Tertiary (total 3)" };
        for (int i = 0; i < 3; i++)
        {
            StepContent.Children.Add(MakeLabel(labels[i]));
            var box = new ComboBox { Width = 300, Background = Brush("#222"), Foreground = Brush("#e0e0e0") };
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
    // Step 3 — Attribute dots
    // =========================================================================
    void BuildAttrDotsStep()
    {
        StepTitle.Text = "Step 4 — Assign Attribute Dots";
        StepHint.Text = "Click dots to set · Minimum 1 per attribute";

        for (int p = 0; p < 3; p++)
        {
            int catIdx = _attrPriority[p].SelectedIndex;
            int budget = AttrBudgets[p];
            int pCopy = p;

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            header.Children.Add(MakeSectionLabel(CatLabels[catIdx]));
            var lbl = new TextBlock { Text = $"0 / {budget}", FontSize = 11, Foreground = Brush("#666"), VerticalAlignment = VerticalAlignment.Center };
            _attrBudgetLabel[p] = lbl;
            header.Children.Add(lbl);
            StepContent.Children.Add(header);

            for (int a = 0; a < 3; a++)
            {
                var row = MakeRow();
                row.Children.Add(new TextBlock { Text = AttrNames[catIdx][a], Width = 130, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#aaa") });
                row.Children.Add(MakeDotPicker(min: 1, max: 5, initial: 1, onChange: _ => RefreshAttrBudget(pCopy), getValue: out _attrGet[catIdx, a]));
                StepContent.Children.Add(row);
            }
            RefreshAttrBudget(p);
        }
    }

    void RefreshAttrBudget(int p) => SetBudgetLabel(_attrBudgetLabel[p], SumGet(_attrGet, _attrPriority[p].SelectedIndex, 3), AttrBudgets[p]);

    bool CommitAttrDots()
    {
        for (int p = 0; p < 3; p++)
        {
            int catIdx = _attrPriority[p].SelectedIndex;
            int sum = SumGet(_attrGet, catIdx, 3);
            if (sum != AttrBudgets[p]) { ShowError($"{CatLabels[catIdx]} attributes must total {AttrBudgets[p]} (currently {sum})."); return false; }
        }
        _c.Strength = G(_attrGet[0, 0]); _c.Dexterity = G(_attrGet[0, 1]); _c.Stamina = G(_attrGet[0, 2]);
        _c.Charisma = G(_attrGet[1, 0]); _c.Manipulation = G(_attrGet[1, 1]); _c.Composure = G(_attrGet[1, 2]);
        _c.Intelligence = G(_attrGet[2, 0]); _c.Wits = G(_attrGet[2, 1]); _c.Resolve = G(_attrGet[2, 2]);
        return true;
    }

    // =========================================================================
    // Step 4 — Skill priority
    // =========================================================================
    void BuildSkillPrioStep()
    {
        StepTitle.Text = "Step 5 — Skill Priority";
        StepHint.Text = "Primary 8 dots · Secondary 6 · Tertiary 4 · Max 3 per skill";

        string[] labels = { "Primary (8 dots)", "Secondary (6 dots)", "Tertiary (4 dots)" };
        for (int i = 0; i < 3; i++)
        {
            StepContent.Children.Add(MakeLabel(labels[i]));
            var box = new ComboBox { Width = 300, Background = Brush("#222"), Foreground = Brush("#e0e0e0") };
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
            if (idx < 0 || !chosen.Add(idx)) { ShowError("Each category must be chosen exactly once."); return false; }
        }
        return true;
    }

    // =========================================================================
    // Step 5 — Skill dots
    // =========================================================================
    void BuildSkillDotsStep()
    {
        StepTitle.Text = "Step 6 — Assign Skill Dots";
        StepHint.Text = "Click dots · Max 3 per skill · Predator Type bonuses applied on top";

        for (int p = 0; p < 3; p++)
        {
            int catIdx = _skillPriority[p].SelectedIndex;
            int budget = SkillBudgets[p];
            int pCopy = p;

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            header.Children.Add(MakeSectionLabel(CatLabels[catIdx]));
            var lbl = new TextBlock { Text = $"0 / {budget}", FontSize = 11, Foreground = Brush("#666"), VerticalAlignment = VerticalAlignment.Center };
            _skillBudgetLabel[p] = lbl;
            header.Children.Add(lbl);
            StepContent.Children.Add(header);

            for (int s = 0; s < 9; s++)
            {
                var row = MakeRow();
                row.Children.Add(new TextBlock { Text = SkillNames[catIdx][s], Width = 130, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#aaa") });
                row.Children.Add(MakeDotPicker(min: 0, max: 3, initial: 0, onChange: _ => RefreshSkillBudget(pCopy), getValue: out _skillGet[catIdx, s]));
                StepContent.Children.Add(row);
            }
            RefreshSkillBudget(p);
        }
    }

    void RefreshSkillBudget(int p) => SetBudgetLabel(_skillBudgetLabel[p], SumGet(_skillGet, _skillPriority[p].SelectedIndex, 9), SkillBudgets[p]);

    bool CommitSkillDots()
    {
        for (int p = 0; p < 3; p++)
        {
            int catIdx = _skillPriority[p].SelectedIndex;
            int sum = SumGet(_skillGet, catIdx, 9);
            if (sum != SkillBudgets[p]) { ShowError($"{CatLabels[catIdx]} skills must total {SkillBudgets[p]} (currently {sum})."); return false; }
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
    // Step 6 — Disciplines
    // =========================================================================
    void BuildDisciplineStep()
    {
        StepTitle.Text = "Step 7 — Disciplines";
        StepHint.Text = "Distribute 3 dots · Predator Type adds +1 to your chosen discipline on top.";

        var inClan = ClanDisciplines.For(_c.Clan);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        header.Children.Add(MakeSectionLabel("IN-CLAN DISCIPLINES"));
        _discBudgetLabel = new TextBlock { Text = "0 / 3", FontSize = 11, Foreground = Brush("#666"), VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(_discBudgetLabel);
        StepContent.Children.Add(header);

        for (int i = 0; i < 3; i++)
        {
            bool isPredBonus = _pendingPredDisc == inClan[i];
            var row = MakeRow();
            row.Children.Add(new TextBlock { Text = ClanDisciplines.DisplayName(inClan[i]), Width = 160, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#aaa") });
            row.Children.Add(MakeDotPicker(min: 0, max: 5, initial: 0, onChange: _ => RefreshDiscBudget(), getValue: out _discGet[i]));
            if (isPredBonus)
                row.Children.Add(new TextBlock { Text = "+1 Predator bonus", Foreground = Brush("#cc4400"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
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
        if (sum != 3) { ShowError($"Disciplines must total 3 dots (currently {sum})."); return false; }

        var inClan = ClanDisciplines.For(_c.Clan);
        PendingDisciplines.Clear();
        for (int i = 0; i < 3; i++)
        {
            byte rating = (byte)(_discGet[i]?.Invoke() ?? 0);
            if (_pendingPredDisc == inClan[i])
                rating = (byte)Math.Min(rating + 1, 5);
            if (rating == 0) continue;
            PendingDisciplines.Add(new Discipline { CharacterId = 0, Name = inClan[i], Rating = rating });
        }
        return true;
    }

    // =========================================================================
    // Step 7 — Review
    // =========================================================================
    void BuildReviewStep()
    {
        StepTitle.Text = "Step 8 — Review";
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
        Row("Predator Type", PredatorTypes.DisplayName(PendingPredatorType));
        Row("Generation", $"{_c.Generation}th");
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
                Row(ClanDisciplines.DisplayName(d.Name), new string('●', d.Rating) + new string('○', 5 - d.Rating));
        }
        if (PendingSpecialties.Length > 0)
        {
            Divider();
            foreach (var s in PendingSpecialties)
                Row("Specialty", s);
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
            1 => CommitPredatorType(),
            2 => CommitAttrPriority(),
            3 => CommitAttrDots(),
            4 => CommitSkillPriority(),
            5 => CommitSkillDots(),
            6 => CommitDisciplines(),
            7 => Finish(),
            _ => true
        };
        if (ok && _step < 7) ShowStep(_step + 1);
    }

    void OnBack(object? sender, RoutedEventArgs e) { if (_step > 0) ShowStep(_step - 1); }
    void OnCancel(object? sender, RoutedEventArgs e) => Close();
    bool Finish() { Result = _c; Close(); return true; }

    // =========================================================================
    // Dot picker
    // =========================================================================
    static StackPanel MakeDotPicker(int min, int max, int initial, Action<int> onChange, out Func<int> getValue)
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
                    i < current ? global::Avalonia.Media.Color.Parse("#cc2200")
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
                Redraw(); onChange(current);
            };
            buttons[i] = btn;
            panel.Children.Add(btn);
        }
        Redraw();
        getValue = () => current;
        return panel;
    }

    // =========================================================================
    // UI helpers
    // =========================================================================
    static StackPanel MakeRow() => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 2) };
    static TextBlock MakeLabel(string t) => new TextBlock { Text = t, Foreground = Brush("#aaa"), FontSize = 12, Margin = new Thickness(0, 8, 0, 2) };
    static TextBlock MakeSectionLabel(string t) => new TextBlock { Text = t, Foreground = Brush("#b00000"), FontSize = 11, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 12, 0, 4), LetterSpacing = 1 };
    static void SetBudgetLabel(TextBlock? lbl, int sum, int budget) { if (lbl == null) return; lbl.Text = $"{sum} / {budget}"; lbl.Foreground = sum == budget ? Brush("#00cc66") : Brush("#666"); }
    void Divider() => StepContent.Children.Add(new Separator { Height = 1, Background = Brush("#333"), Margin = new Thickness(0, 8) });
    void ShowError(string msg)
    {
        if (StepContent.Children.Count > 0 && StepContent.Children[^1] is TextBlock { Tag: "error" } prev)
            StepContent.Children.Remove(prev);
        StepContent.Children.Add(new TextBlock { Text = msg, Foreground = Brush("#cc0000"), FontSize = 12, Margin = new Thickness(0, 8, 0, 0), Tag = "error" });
    }
    static ISolidColorBrush Brush(string hex) => new SolidColorBrush(global::Avalonia.Media.Color.Parse(hex));
    static byte G(Func<int>? f) => (byte)(f?.Invoke() ?? 0);
    static int SumGet(Func<int>[,] arr, int row, int count) { int s = 0; for (int i = 0; i < count; i++) s += arr[row, i]?.Invoke() ?? 0; return s; }
    static string ClanDisplayName(Clan c) => c == Clan.Banu_Haqim ? "Banu Haqim" : c.ToString();
}