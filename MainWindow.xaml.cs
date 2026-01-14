using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop
{
    public partial class MainWindow : Window
    {
        private const string LightThemePath = "Themes/Design.xaml";
        private const string DarkThemePath = "Themes/Design.Dark.xaml";

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            ApplyTheme(true);
        }

        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyTheme(false);
        }

        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            if (ShouldHandleInNestedScrollViewer(e.OriginalSource as DependencyObject, scrollViewer, e.Delta))
            {
                return;
            }

            var targetOffset = scrollViewer.VerticalOffset - e.Delta;
            targetOffset = Math.Clamp(targetOffset, 0, scrollViewer.ScrollableHeight);
            scrollViewer.ScrollToVerticalOffset(targetOffset);
            e.Handled = true;
        }

        private static bool ShouldHandleInNestedScrollViewer(DependencyObject? source, ScrollViewer root, double delta)
        {
            var current = source;
            while (current != null && current != root)
            {
                if (current is ScrollViewer nested)
                {
                    if (ReferenceEquals(nested, root))
                    {
                        break;
                    }

                    var templatedParent = nested.TemplatedParent;
                    if (templatedParent is TextBoxBase || templatedParent is PasswordBox)
                    {
                        current = GetParent(nested);
                        continue;
                    }

                    bool hasScrollableContent = nested.ScrollableHeight > 0 || nested.ComputedVerticalScrollBarVisibility == Visibility.Visible;
                    if (!hasScrollableContent)
                    {
                        current = GetParent(nested);
                        continue;
                    }

                    bool scrollingUp = delta > 0;
                    bool canScrollUp = nested.VerticalOffset > 0;
                    bool canScrollDown = nested.VerticalOffset < nested.ScrollableHeight;

                    if ((scrollingUp && canScrollUp) || (!scrollingUp && canScrollDown))
                    {
                        return true;
                    }

                    current = GetParent(nested);
                    continue;
                }

                current = GetParent(current);
            }

            return false;
        }

        private static DependencyObject? GetParent(DependencyObject? child)
        {
            if (child == null)
            {
                return null;
            }

            if (child is Visual || child is Visual3D)
            {
                var parent = VisualTreeHelper.GetParent(child);
                if (parent != null)
                {
                    return parent;
                }
            }

            return LogicalTreeHelper.GetParent(child);
        }

        private static void ApplyTheme(bool isDark)
        {
            var uri = new Uri(isDark ? DarkThemePath : LightThemePath, UriKind.Relative);
            var themeDictionary = new ResourceDictionary { Source = uri };
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            var existingTheme = mergedDictionaries.FirstOrDefault(dictionary =>
                dictionary.Source != null &&
                (dictionary.Source.OriginalString.EndsWith("Design.xaml", StringComparison.OrdinalIgnoreCase) ||
                 dictionary.Source.OriginalString.EndsWith("Design.Dark.xaml", StringComparison.OrdinalIgnoreCase)));

            if (existingTheme != null)
            {
                var themeIndex = mergedDictionaries.IndexOf(existingTheme);
                mergedDictionaries[themeIndex] = themeDictionary;
            }
            else
            {
                mergedDictionaries.Insert(0, themeDictionary);
            }
        }
    }
}
