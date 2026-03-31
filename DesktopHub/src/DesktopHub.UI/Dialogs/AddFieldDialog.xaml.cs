using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI.Dialogs;

public partial class AddFieldDialog : Window
{
    public MasterFieldDefinition? ResultField { get; private set; }
    public bool IsMasterScope { get; private set; } = true;

    public AddFieldDialog(List<string> existingCategories, string preSelectedCategory, bool hasProject)
    {
        InitializeComponent();

        // Populate category dropdown
        foreach (var cat in existingCategories)
            CategoryCombo.Items.Add(cat);
        CategoryCombo.Text = preSelectedCategory;

        // Disable "This Project Only" if no project is loaded
        if (!hasProject)
        {
            ProjectScopeRadio.IsEnabled = false;
            ProjectScopeRadio.ToolTip = "Load a project first to add project-specific fields";
        }

        // Setup transparency
        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            WindowHelper.UpdateRootClip(RootBorder, 12, "AddFieldDialog");
            this.Background = null;
        };
        SizeChanged += (s, e) => WindowHelper.UpdateRootClip(RootBorder, 12, "AddFieldDialog");
        Loaded += (s, e) => DisplayNameBox.Focus();
    }

    private void DisplayNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Auto-generate key from display name
        var name = DisplayNameBox.Text?.Trim() ?? "";
        KeyBox.Text = GenerateKey(name);
    }

    private static string GenerateKey(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "";
        // Convert to snake_case: "My Field Name" -> "my_field_name"
        var key = displayName.ToLowerInvariant().Trim();
        key = Regex.Replace(key, @"[^a-z0-9]+", "_");
        key = key.Trim('_');
        return key;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var displayName = DisplayNameBox.Text?.Trim();
        var key = KeyBox.Text?.Trim();
        var category = CategoryCombo.Text?.Trim();

        if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(key))
        {
            System.Windows.MessageBox.Show("Display Name and Key are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var inputMode = (InputModeCombo.SelectedIndex) switch
        {
            0 => TagInputMode.Dropdown,
            1 => TagInputMode.FreeText,
            2 => TagInputMode.MultiSelect,
            _ => TagInputMode.Dropdown
        };

        var suggestedValues = Array.Empty<string>();
        var svText = SuggestedValuesBox.Text?.Trim();
        if (!string.IsNullOrEmpty(svText))
        {
            suggestedValues = svText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        ResultField = new MasterFieldDefinition
        {
            Key = key,
            DisplayName = displayName,
            Category = string.IsNullOrEmpty(category) ? "Other" : category,
            InputMode = inputMode,
            SuggestedValues = suggestedValues,
            Aliases = Array.Empty<string>(),
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
