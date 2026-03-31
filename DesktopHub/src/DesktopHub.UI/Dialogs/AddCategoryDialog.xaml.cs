using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI.Dialogs;

public partial class AddCategoryDialog : Window
{
    public MasterCategoryDefinition? ResultCategory { get; private set; }
    public bool IsMasterScope { get; private set; } = true;

    private Border? _selectedChip;

    private static readonly string[] EmojiOptions =
    {
        "\u26A1",       // lightning (electrical)
        "\u2744\uFE0F", // snowflake (mechanical/HVAC)
        "\U0001F3E0",   // house (building)
        "\U0001F4CD",   // pin (location)
        "\U0001F465",   // people
        "\U0001F4D6",   // book (code)
        "\U0001F4CB",   // clipboard (other)
        "\U0001F527",   // wrench (tools)
        "\U0001F4CA",   // bar chart (data)
        "\U0001F4DD",   // memo (notes)
        "\U0001F50D",   // magnifying glass (search)
        "\U0001F4E6",   // package (materials)
        "\U0001F4B0",   // money bag (budget)
        "\U0001F6E0\uFE0F", // hammer+wrench (maintenance)
        "\U0001F4C5",   // calendar (schedule)
        "\U0001F512",   // lock (security)
        "\U0001F3AF",   // target (goals)
        "\U0001F4A1",   // lightbulb (ideas)
        "\U0001F30D",   // globe (global)
        "\U0001F4E7",   // email (comms)
    };

    public AddCategoryDialog(bool hasProject)
    {
        InitializeComponent();

        var hoverBrush = (System.Windows.Media.Brush)FindResource("HoverBrush");
        var hoverStrongBrush = (System.Windows.Media.Brush)FindResource("HoverStrongBrush");
        var accentBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
        var borderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");

        foreach (var emoji in EmojiOptions)
        {
            var chip = new Border
            {
                Background = hoverBrush,
                CornerRadius = new CornerRadius(6),
                Width = 32, Height = 32,
                Margin = new Thickness(2),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Tag = emoji
            };
            chip.Child = new TextBlock
            {
                Text = emoji,
                FontSize = 15,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            chip.MouseEnter += (s, ev) =>
            {
                if (s is Border b && b != _selectedChip)
                    b.Background = hoverStrongBrush;
            };
            chip.MouseLeave += (s, ev) =>
            {
                if (s is Border b && b != _selectedChip)
                    b.Background = hoverBrush;
            };
            chip.MouseLeftButtonDown += (s, ev) =>
            {
                if (s is Border b && b.Tag is string emojiVal)
                {
                    // Deselect previous
                    if (_selectedChip != null)
                    {
                        _selectedChip.Background = hoverBrush;
                        _selectedChip.BorderBrush = borderBrush;
                    }

                    // Select new
                    b.Background = accentBrush;
                    b.BorderBrush = accentBrush;
                    _selectedChip = b;

                    IconBox.Text = emojiVal;
                    IconPreview.Text = emojiVal;
                    ev.Handled = true;
                }
            };
            EmojiPicker.Children.Add(chip);
        }

        // Pre-select the clipboard icon (default)
        if (EmojiPicker.Children.Count > 6 && EmojiPicker.Children[6] is Border defaultChip)
        {
            defaultChip.Background = accentBrush;
            defaultChip.BorderBrush = accentBrush;
            _selectedChip = defaultChip;
        }

        if (!hasProject)
        {
            ProjectScopeRadio.IsEnabled = false;
            ProjectScopeRadio.ToolTip = "Load a project first to add project-specific categories";
        }

        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            WindowHelper.UpdateRootClip(RootBorder, 12, "AddCategoryDialog");
            this.Background = null;
        };
        SizeChanged += (s, e) => WindowHelper.UpdateRootClip(RootBorder, 12, "AddCategoryDialog");
        Loaded += (s, e) => CategoryNameBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var name = CategoryNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            System.Windows.MessageBox.Show("Category name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var icon = IconBox.Text?.Trim();
        if (string.IsNullOrEmpty(icon))
            icon = "\U0001F4CB";

        ResultCategory = new MasterCategoryDefinition
        {
            Name = name,
            Icon = icon,
            IsBuiltIn = false
        };

        IsMasterScope = MasterScopeRadio.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
