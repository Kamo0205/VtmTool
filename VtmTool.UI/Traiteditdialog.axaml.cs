using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class TraitEditDialog : Window
{
    public bool Confirmed { get; private set; }
    public CharacterTrait Result { get; private set; }

    readonly TraitCategory _category;
    int _rating = 0;

    // =========================================================================
    // Constructors
    // =========================================================================
    public TraitEditDialog() {
        InitializeComponent();
    }   // XAML loader

    // New trait
    public TraitEditDialog(TraitCategory category, int characterId)
    {
        _category = category;
        InitializeComponent();
        Title = $"Add {category}";
        PopulateNames(category);
        BuildRatingPicker(0);
        Result = new CharacterTrait
        {
            CharacterId = characterId,
            Category = category,
        };
    }

    // Edit existing trait
    public TraitEditDialog(CharacterTrait existing)
    {
        _category = existing.Category;
        InitializeComponent();
        Title = $"Edit {existing.Category}";
        PopulateNames(existing.Category);
        NameBox.Text = existing.Name;
        DetailBox.Text = existing.Detail;
        BuildRatingPicker(existing.Rating);
        Result = existing;
    }

    // =========================================================================
    // Setup
    // =========================================================================
    void PopulateNames(TraitCategory cat)
    {
        string[] names = cat switch
        {
            TraitCategory.Background => TraitDefinitions.Backgrounds,
            TraitCategory.Merit => TraitDefinitions.Merits,
            TraitCategory.Flaw => TraitDefinitions.Flaws,
            _ => Array.Empty<string>(),
        };

        foreach (string n in names)
            NameBox.Items.Add(new ComboBoxItem { Content = n });
    }

    void BuildRatingPicker(int current)
    {
        _rating = current;
        RatingPicker.Children.Clear();

        // 0 button (unrated / clear)
        var clearBtn = new Button
        {
            Content = "○",
            Width = 28,
            Height = 28,
            Padding = new Avalonia.Thickness(0),
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse(_rating == 0 ? "#cc2200" : "#444")),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        clearBtn.Click += (_, _) => SetRating(0);
        RatingPicker.Children.Add(clearBtn);

        // Dots 1–5
        for (int i = 1; i <= 5; i++)
        {
            int dot = i;
            var btn = new Button
            {
                Content = dot <= _rating ? "●" : "○",
                Width = 28,
                Height = 28,
                Padding = new Avalonia.Thickness(0),
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse(dot <= _rating ? "#cc2200" : "#444")),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            btn.Click += (_, _) => SetRating(_rating == dot ? dot - 1 : dot);
            RatingPicker.Children.Add(btn);
        }
    }

    void SetRating(int r)
    {
        _rating = Math.Clamp(r, 0, 5);
        BuildRatingPicker(_rating);
    }

    // =========================================================================
    // Save / Cancel
    // =========================================================================
    void OnSave(object? sender, RoutedEventArgs e)
    {
        string name = (NameBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            NameBox.BorderBrush = new SolidColorBrush(Color.Parse("#cc0000"));
            return;
        }

        Result = Result with
        {
            Category = _category,
            Name = name,
            Rating = (byte)_rating,
            Detail = (DetailBox.Text ?? "").Trim(),
            IsCustom = !IsInFixedList(name, _category),
        };

        Confirmed = true;
        Close();
    }

    void OnCancel(object? sender, RoutedEventArgs e) => Close();

    static bool IsInFixedList(string name, TraitCategory cat)
    {
        string[] list = cat switch
        {
            TraitCategory.Background => TraitDefinitions.Backgrounds,
            TraitCategory.Merit => TraitDefinitions.Merits,
            TraitCategory.Flaw => TraitDefinitions.Flaws,
            _ => Array.Empty<string>(),
        };
        foreach (string s in list)
            if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}