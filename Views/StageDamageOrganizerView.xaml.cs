using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class StageDamageOrganizerView : UserControl
    {
        public StageDamageOrganizerView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private StageDamageOrganizerViewModel? ViewModel => DataContext as StageDamageOrganizerViewModel;

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is StageDamageOrganizerViewModel oldVm)
            {
                oldVm.AepHeaders.CollectionChanged -= OnAepHeadersChanged;
            }

            if (e.NewValue is StageDamageOrganizerViewModel newVm)
            {
                newVm.AepHeaders.CollectionChanged += OnAepHeadersChanged;
                RebuildCategorySummaryColumns(newVm);
            }
        }

        private void OnAepHeadersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                RebuildCategorySummaryColumns(ViewModel);
            }
        }

        private void RebuildCategorySummaryColumns(StageDamageOrganizerViewModel viewModel)
        {
            CategorySummaryGrid.Columns.Clear();

            CategorySummaryGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Summary Name",
                Binding = new Binding("SummaryName"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            CategorySummaryGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Damage Category",
                Binding = new Binding("DamageCategory"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            CategorySummaryGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Structures",
                Binding = new Binding("StructureCount"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            for (int i = 0; i < viewModel.AepHeaders.Count; i++)
            {
                CategorySummaryGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = viewModel.AepHeaders[i],
                    Binding = new Binding($"AepDamages[{i}]") { StringFormat = "C0" },
                    Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
                });
            }

            CategorySummaryGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Total AEP Sum",
                Binding = new Binding("FrequentSumDamage") { StringFormat = "C0" },
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });
        }
    }
}
