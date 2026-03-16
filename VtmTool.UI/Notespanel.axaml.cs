using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class NotesPanel : UserControl
{
    int _characterId = -1;
    bool _isDirty = false;
    public event EventHandler<CharacterNotes>? NotesSaved;

    public NotesPanel()
    {
        InitializeComponent();
        WireChangeTracking();
    }

    void WireChangeTracking()
    {
        // Mark dirty whenever any text box changes
        BackgroundBox.TextChanged += OnTextChanged;
        GoalsBox.TextChanged += OnTextChanged;
        CoterieBox.TextChanged += OnTextChanged;
    }

    public void SetCharacter(int characterId, CharacterNotes notes)
    {
        _characterId = characterId;

        // Temporarily unwire change tracking so loading doesn't mark dirty
        BackgroundBox.TextChanged -= OnTextChanged;
        GoalsBox.TextChanged -= OnTextChanged;
        CoterieBox.TextChanged -= OnTextChanged;

        BackgroundBox.Text = notes.Background ?? "";
        GoalsBox.Text = notes.Goals ?? "";
        CoterieBox.Text = notes.Coterie ?? "";

        BackgroundBox.TextChanged += OnTextChanged;
        GoalsBox.TextChanged += OnTextChanged;
        CoterieBox.TextChanged += OnTextChanged;

        SetDirty(false);
        SaveStatus.Text = "";
    }

    public void ClearCharacter()
    {
        _characterId = -1;
        BackgroundBox.Text = "";
        GoalsBox.Text = "";
        CoterieBox.Text = "";
        SetDirty(false);
        SaveStatus.Text = "";
    }

    void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_characterId < 0) return;
        SetDirty(true);
    }

    void SetDirty(bool dirty)
    {
        _isDirty = dirty;
        SaveBtn.IsEnabled = dirty && _characterId >= 0;
        if (dirty) SaveStatus.Text = "Unsaved changes";
    }

    void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_characterId < 0) return;

        var notes = new CharacterNotes
        {
            Background = BackgroundBox.Text ?? "",
            Goals = GoalsBox.Text ?? "",
            Coterie = CoterieBox.Text ?? "",
        };

        NotesSaved?.Invoke(this, notes);
        SetDirty(false);
        SaveStatus.Text = $"Saved {DateTime.Now:HH:mm}";
    }
}