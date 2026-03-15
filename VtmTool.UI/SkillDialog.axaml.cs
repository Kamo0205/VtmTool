using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VtmTool.Core.Models;

namespace VtmTool.UI;

public partial class SkillDialog : CreationWizard
{
    public bool Confirmed => Result.HasValue;

    public SkillDialog(Character existing) : base()
    {
        // Same pattern as AttributeDialog.
    }

    public new Character? Result => base.Result ?? default;
}