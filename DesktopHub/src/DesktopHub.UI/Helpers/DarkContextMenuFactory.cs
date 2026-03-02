using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Effects;
using WpfColor = System.Windows.Media.Color;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Creates dark-themed WPF context menus with custom chrome, eliminating
/// duplicated CreateDarkContextMenu methods across widgets and overlays.
/// </summary>
public static class DarkContextMenuFactory
{
    private static readonly WpfColor MenuBgColor = WpfColor.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly WpfColor MenuBorderColor = WpfColor.FromArgb(0x40, 0xFF, 0xFF, 0xFF);
    private static readonly WpfColor ItemFgColor = WpfColor.FromRgb(0xE0, 0xE0, 0xE0);
    private static readonly WpfColor HoverBgColor = WpfColor.FromArgb(0x30, 0x4F, 0xC3, 0xF7);
    private static readonly WpfColor SeparatorColor = WpfColor.FromArgb(0x20, 0xFF, 0xFF, 0xFF);

    /// <summary>
    /// Creates a fully-styled dark context menu with custom MenuItem template.
    /// Includes drop shadow, hover highlight, and optional separator styling.
    /// </summary>
    public static ContextMenu Create(bool includeSeparatorStyle = false)
    {
        var menuBg = new System.Windows.Media.SolidColorBrush(MenuBgColor);
        var menuBorder = new System.Windows.Media.SolidColorBrush(MenuBorderColor);
        var itemFg = new System.Windows.Media.SolidColorBrush(ItemFgColor);
        var hoverBg = new System.Windows.Media.SolidColorBrush(HoverBgColor);

        // MenuItem ControlTemplate
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "Bd";
        borderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        itemTemplate.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        itemTemplate.Triggers.Add(hoverTrigger);

        // MenuItem style
        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, itemFg));
        itemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, itemTemplate));
        itemStyle.Setters.Add(new Setter(MenuItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        // ContextMenu template
        var contextMenuTemplate = new ControlTemplate(typeof(ContextMenu));
        var menuBorderFactory = new FrameworkElementFactory(typeof(Border));
        menuBorderFactory.SetValue(Border.BackgroundProperty, menuBg);
        menuBorderFactory.SetValue(Border.BorderBrushProperty, menuBorder);
        menuBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        menuBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        menuBorderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 4, 2, 4));
        menuBorderFactory.SetValue(Border.EffectProperty, new DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 2,
            Opacity = 0.5,
            Color = WpfColor.FromRgb(0, 0, 0)
        });

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        menuBorderFactory.AppendChild(itemsPresenter);
        contextMenuTemplate.VisualTree = menuBorderFactory;

        var menu = new ContextMenu
        {
            Template = contextMenuTemplate,
            HasDropShadow = false
        };
        menu.Resources[typeof(MenuItem)] = itemStyle;

        if (includeSeparatorStyle)
        {
            var sepTemplate = new ControlTemplate(typeof(Separator));
            var sepBorderFactory = new FrameworkElementFactory(typeof(Border));
            sepBorderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(SeparatorColor));
            sepBorderFactory.SetValue(Border.HeightProperty, 1.0);
            sepBorderFactory.SetValue(Border.MarginProperty, new Thickness(8, 4, 8, 4));
            sepTemplate.VisualTree = sepBorderFactory;
            var sepStyle = new Style(typeof(Separator));
            sepStyle.Setters.Add(new Setter(Separator.TemplateProperty, sepTemplate));
            menu.Resources[typeof(Separator)] = sepStyle;
        }

        return menu;
    }

    /// <summary>
    /// Creates a styled submenu MenuItem with arrow indicator and PART_Popup for submenus.
    /// </summary>
    public static MenuItem CreateSubmenuItem(string header)
    {
        var menuBg = new System.Windows.Media.SolidColorBrush(MenuBgColor);
        var menuBorder = new System.Windows.Media.SolidColorBrush(MenuBorderColor);
        var itemFg = new System.Windows.Media.SolidColorBrush(ItemFgColor);
        var hoverBg = new System.Windows.Media.SolidColorBrush(HoverBgColor);

        var template = new ControlTemplate(typeof(MenuItem));
        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        // Visible row
        var bdFactory = new FrameworkElementFactory(typeof(Border));
        bdFactory.Name = "Bd";
        bdFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        bdFactory.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        bdFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        bdFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

        var dockFactory = new FrameworkElementFactory(typeof(DockPanel));

        // Arrow indicator
        var arrowFactory = new FrameworkElementFactory(typeof(TextBlock));
        arrowFactory.SetValue(DockPanel.DockProperty, Dock.Right);
        arrowFactory.SetValue(TextBlock.TextProperty, "\u203A");
        arrowFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
        arrowFactory.SetValue(TextBlock.ForegroundProperty,
            new System.Windows.Media.SolidColorBrush(WpfColor.FromArgb(0x60, 0xE0, 0xE0, 0xE0)));
        arrowFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrowFactory.SetValue(TextBlock.MarginProperty, new Thickness(16, 0, 0, 0));
        dockFactory.AppendChild(arrowFactory);

        // Header content
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        dockFactory.AppendChild(contentFactory);

        bdFactory.AppendChild(dockFactory);
        gridFactory.AppendChild(bdFactory);

        // Popup for sub-items
        var popupFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup));
        popupFactory.Name = "PART_Popup";
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty, false);
        popupFactory.SetBinding(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
            new System.Windows.Data.Binding("IsSubmenuOpen") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty,
            System.Windows.Controls.Primitives.PlacementMode.Right);
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);

        var popupBorder = new FrameworkElementFactory(typeof(Border));
        popupBorder.SetValue(Border.BackgroundProperty, menuBg);
        popupBorder.SetValue(Border.BorderBrushProperty, menuBorder);
        popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        popupBorder.SetValue(Border.PaddingProperty, new Thickness(2, 4, 2, 4));
        popupBorder.SetValue(Border.MarginProperty, new Thickness(2, 0, 0, 0));

        var subItemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        popupBorder.AppendChild(subItemsPresenter);
        popupFactory.AppendChild(popupBorder);
        gridFactory.AppendChild(popupFactory);

        template.VisualTree = gridFactory;

        // Hover trigger
        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        template.Triggers.Add(hoverTrigger);

        // Sub-item style
        var subItemStyle = new Style(typeof(MenuItem));
        subItemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, itemFg));
        subItemStyle.Setters.Add(new Setter(MenuItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        var subItemTemplate = new ControlTemplate(typeof(MenuItem));
        var subBd = new FrameworkElementFactory(typeof(Border));
        subBd.Name = "SubBd";
        subBd.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        subBd.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        subBd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        subBd.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));
        var subContent = new FrameworkElementFactory(typeof(ContentPresenter));
        subContent.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        subContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        subBd.AppendChild(subContent);
        subItemTemplate.VisualTree = subBd;

        var subHover = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        subHover.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "SubBd"));
        subItemTemplate.Triggers.Add(subHover);
        subItemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, subItemTemplate));

        var item = new MenuItem
        {
            Header = header,
            Template = template,
            Foreground = itemFg,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        item.Resources[typeof(MenuItem)] = subItemStyle;

        return item;
    }
}
