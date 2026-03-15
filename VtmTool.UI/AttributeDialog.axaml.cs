using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class AttributeDialog : CreationWizard
{
    public bool Confirmed => Result.HasValue;
    public Character Result2 => Result ?? default;

    public AttributeDialog(Character existing) : base()
    {
        // NOTE: This is a temporary bridge.The right fix is a
        // dedicated AttributeEditor UserControl.
    }

    // Re-expose as the shape MainWindow expects
    public new Character? Result => base.Result ?? default;
}