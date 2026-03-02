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
                RebuildDamageSummaryColumns(ContentCategorySummaryGrid, newVm, "AepDamages", "Content Total AEP Sum");
                RebuildDamageSummaryColumns(OtherCategorySummaryGrid, newVm, "AepDamages", "Other Total AEP Sum");
                RebuildDamageSummaryColumns(VehicleCategorySummaryGrid, newVm, "AepDamages", "Vehicle Total AEP Sum");
            }
        }

        private void OnAepHeadersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                RebuildCategorySummaryColumns(ViewModel);
                RebuildDamageSummaryColumns(ContentCategorySummaryGrid, ViewModel, "AepDamages", "Content Total AEP Sum");
                RebuildDamageSummaryColumns(OtherCategorySummaryGrid, ViewModel, "AepDamages", "Other Total AEP Sum");
                RebuildDamageSummaryColumns(VehicleCategorySummaryGrid, ViewModel, "AepDamages", "Vehicle Total AEP Sum");
            }
        }

        private void RebuildCategorySummaryColumns(StageDamageOrganizerViewModel viewModel)
        {
            RebuildDamageSummaryColumns(CategorySummaryGrid, viewModel, "AepDamages", "Total AEP Sum");
        }

        private static void RebuildDamageSummaryColumns(DataGrid grid, StageDamageOrganizerViewModel viewModel, string damageBindingPath, string totalHeader)
        {
            grid.Columns.Clear();

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Summary Name",
                Binding = new Binding("SummaryName"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Damage Category",
                Binding = new Binding("DamageCategory"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Structures",
                Binding = new Binding("StructureCount"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            for (int i = 0; i < viewModel.AepHeaders.Count; i++)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = viewModel.AepHeaders[i],
                    Binding = new Binding($"{damageBindingPath}[{i}]") { StringFormat = "C0" },
                    Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
                });
            }

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = totalHeader,
                Binding = new Binding("FrequentSumDamage") { StringFormat = "C0" },
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });
        }
    }
}
