using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using EconToolbox.Desktop.ViewModels;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;

namespace EconToolbox.Desktop.Views
{
    public partial class EadView : UserControl
    {
        private GraphViewer? _graphViewer;
        private EadViewModel? _viewModel;

        public EadView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsureGraphViewer();
            UpdateGraph();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            }

            if (e.NewValue is EadViewModel vm)
            {
                _viewModel = vm;
                vm.PropertyChanged += ViewModelOnPropertyChanged;
            }
            else
            {
                _viewModel = null;
            }

            UpdateGraph();
        }

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EadViewModel.Graph))
            {
                Dispatcher.Invoke(UpdateGraph);
            }
        }

        private void EnsureGraphViewer()
        {
            if (_graphViewer != null)
            {
                return;
            }

            _graphViewer = new GraphViewer
            {
                LayoutEditingEnabled = false
            };
            _graphViewer.BindToPanel(GraphHost);
        }

        private void UpdateGraph()
        {
            EnsureGraphViewer();
            if (_graphViewer == null)
            {
                return;
            }

            _graphViewer.Graph = _viewModel?.Graph ?? new Graph("eadGraph");
        }
    }
}
