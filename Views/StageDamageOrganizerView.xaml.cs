using System;
using System.Collections.Specialized;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class StageDamageOrganizerView : UserControl
    {
        private bool _isMapPanning;
        private Point _mapPanStart;
        private double _mapPanStartHorizontalOffset;
        private double _mapPanStartVerticalOffset;

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
                RebuildStructureCountByAepColumns(newVm);
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
                RebuildStructureCountByAepColumns(ViewModel);
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

        private void RebuildStructureCountByAepColumns(StageDamageOrganizerViewModel viewModel)
        {
            StructureCountByAepSummaryGrid.Columns.Clear();

            StructureCountByAepSummaryGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Summary Name",
                Binding = new Binding("SummaryName"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            StructureCountByAepSummaryGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Impact Area",
                Binding = new Binding("ImpactArea"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            StructureCountByAepSummaryGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Structures",
                Binding = new Binding("StructureCount"),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });

            for (int i = 0; i < viewModel.AepHeaders.Count; i++)
            {
                StructureCountByAepSummaryGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = viewModel.AepHeaders[i],
                    Binding = new Binding($"StructureCountsByAep[{i}]"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
                });
            }
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
                Header = "Impact Area",
                Binding = new Binding("ImpactArea"),
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

        private void MapScrollViewer_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            _isMapPanning = true;
            _mapPanStart = e.GetPosition(scrollViewer);
            _mapPanStartHorizontalOffset = scrollViewer.HorizontalOffset;
            _mapPanStartVerticalOffset = scrollViewer.VerticalOffset;
            scrollViewer.CaptureMouse();
            Mouse.OverrideCursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void MapScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMapPanning || sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            var current = e.GetPosition(scrollViewer);
            var delta = current - _mapPanStart;

            scrollViewer.ScrollToHorizontalOffset(_mapPanStartHorizontalOffset - delta.X);
            scrollViewer.ScrollToVerticalOffset(_mapPanStartVerticalOffset - delta.Y);
        }

        private void MapScrollViewer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            EndMapPan(scrollViewer);
        }


        private void MapScrollViewer_OnPreviewMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            EndMapPan(scrollViewer);
        }

        private void MapScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel is null || sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            var pointer = e.GetPosition(scrollViewer);
            double previousZoom = ViewModel.MapZoom;
            double zoomStep = e.Delta > 0 ? 0.5d : -0.5d;

            ViewModel.MapZoom += zoomStep;
            double nextZoom = ViewModel.MapZoom;

            if (Math.Abs(nextZoom - previousZoom) < 0.0001d)
            {
                e.Handled = true;
                return;
            }

            double zoomFactor = nextZoom / previousZoom;
            double newHorizontalOffset = ((scrollViewer.HorizontalOffset + pointer.X) * zoomFactor) - pointer.X;
            double newVerticalOffset = ((scrollViewer.VerticalOffset + pointer.Y) * zoomFactor) - pointer.Y;

            scrollViewer.ScrollToHorizontalOffset(newHorizontalOffset);
            scrollViewer.ScrollToVerticalOffset(newVerticalOffset);
            e.Handled = true;
        }

        private void EndMapPan(ScrollViewer scrollViewer)
        {
            if (!_isMapPanning)
            {
                return;
            }

            _isMapPanning = false;
            scrollViewer.ReleaseMouseCapture();
            Mouse.OverrideCursor = null;
        }
    }
}
