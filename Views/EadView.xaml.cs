using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class EadView : UserControl
    {
        public EadView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is EadViewModel oldVm)
            {
                oldVm.DamageColumns.CollectionChanged -= DamageColumns_CollectionChanged;
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }
            if (e.NewValue is EadViewModel vm)
            {
                vm.DamageColumns.CollectionChanged += DamageColumns_CollectionChanged;
                vm.PropertyChanged += Vm_PropertyChanged;
                RebuildColumns(vm);
            }
        }

        private void DamageColumns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is EadViewModel vm)
                RebuildColumns(vm);
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EadViewModel.UseStage) && DataContext is EadViewModel vm)
                RebuildColumns(vm);
        }

        private void RebuildColumns(EadViewModel vm)
        {
            EadDataGrid.Columns.Clear();
            EadDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Frequency",
                Binding = new Binding("Frequency")
            });
            if (vm.UseStage)
            {
                EadDataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Stage",
                    Binding = new Binding("Stage")
                });
            }
            for (int i = 0; i < vm.DamageColumns.Count; i++)
            {
                EadDataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = vm.DamageColumns[i],
                    Binding = new Binding($"Damages[{i}]")
                });
            }
        }
    }
}
