using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EconToolbox.Desktop.ViewModels
{
    public class MindMapViewModel : BaseViewModel
    {
        private readonly RelayCommand _addChildCommand;
        private readonly RelayCommand _addSiblingCommand;
        private readonly RelayCommand _removeNodeCommand;
        private readonly List<MindMapNodeViewModel> _pathSubscriptions = new();
        private bool _suppressSelectionSync;
        private int _nodeCounter = 1;
        private const double MinZoomLevel = 0.4;
        private const double MaxZoomLevel = 1.6;
        private double _zoomLevel = 1.0;

        public ObservableCollection<MindMapNodeViewModel> Nodes { get; } = new();
        public ObservableCollection<MindMapNodeViewModel> CanvasNodes { get; } = new();
        public ObservableCollection<MindMapConnectionViewModel> Connections { get; } = new();

        public double CanvasWidth { get; } = 2200;
        public double CanvasHeight { get; } = 1400;

        private MindMapNodeViewModel? _selectedNode;
        public MindMapNodeViewModel? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode == value)
                    return;

                var previous = _selectedNode;
                _selectedNode = value;

                _suppressSelectionSync = true;
                if (previous != null)
                    previous.IsSelected = false;
                if (_selectedNode != null)
                    _selectedNode.IsSelected = true;
                _suppressSelectionSync = false;

                UpdatePathSubscriptions();

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedPath));

                UpdateCommandStates();
            }
        }

        public bool HasSelection => SelectedNode != null;

        public string SelectedPath => SelectedNode == null
            ? string.Empty
            : string.Join("  ›  ", SelectedNode.GetPath().Select(n => n.Title));

        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                var clamped = Math.Clamp(value, MinZoomLevel, MaxZoomLevel);
                if (Math.Abs(_zoomLevel - clamped) > 0.001)
                {
                    _zoomLevel = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public double ZoomLevelMinimum => MinZoomLevel;
        public double ZoomLevelMaximum => MaxZoomLevel;

        public ICommand AddRootNodeCommand { get; }
        public ICommand AddChildNodeCommand => _addChildCommand;
        public ICommand AddSiblingNodeCommand => _addSiblingCommand;
        public ICommand RemoveNodeCommand => _removeNodeCommand;

        public MindMapViewModel()
        {
            AddRootNodeCommand = new RelayCommand(() => AddRootAt(null));
            _addChildCommand = new RelayCommand(() => AddChildAt(SelectedNode, null), () => SelectedNode != null);
            _addSiblingCommand = new RelayCommand(() => AddSiblingAt(SelectedNode, null), () => SelectedNode != null);
            _removeNodeCommand = new RelayCommand(RemoveNode, () => SelectedNode != null);

            var coreIdea = CreateNode("Central Idea");
            coreIdea.Notes = "Capture the main question, opportunity, or challenge you are exploring.";
            Nodes.Add(coreIdea);

            var themes = CreateNode("Key Themes", coreIdea);
            themes.Notes = "Break the central idea into major themes, components, or workstreams.";
            coreIdea.Children.Add(themes);
            AddConnection(coreIdea, themes);

            var actions = CreateNode("Next Actions", coreIdea);
            actions.Notes = "Record immediate tasks, owners, and follow-ups.";
            coreIdea.Children.Add(actions);
            AddConnection(coreIdea, actions);

            PositionNode(coreIdea, null, new Point(360, CanvasHeight / 2));
            PositionNode(themes, coreIdea, new Point(coreIdea.X + 260, coreIdea.Y - 160));
            PositionNode(actions, coreIdea, new Point(coreIdea.X + 260, coreIdea.Y + 160));

            coreIdea.IsExpanded = true;
            themes.IsExpanded = true;
            actions.IsExpanded = true;

            SelectedNode = coreIdea;
        }

        private string GetDefaultTitle() => $"New Idea {_nodeCounter++}";

        private MindMapNodeViewModel CreateNode(string title, MindMapNodeViewModel? parent = null)
        {
            var node = new MindMapNodeViewModel(title)
            {
                Parent = parent
            };
            AttachNode(node);
            return node;
        }

        public MindMapNodeViewModel AddRootAt(Point? position)
        {
            var node = CreateNode(GetDefaultTitle());
            node.IsExpanded = true;
            Nodes.Add(node);
            PositionNode(node, null, position);
            SelectedNode = node;
            return node;
        }

        public MindMapNodeViewModel? AddChildAt(MindMapNodeViewModel? parent, Point? position)
        {
            if (parent == null)
                return null;

            var child = CreateNode(GetDefaultTitle(), parent);
            parent.Children.Add(child);
            parent.IsExpanded = true;
            AddConnection(parent, child);
            PositionNode(child, parent, position);
            SelectedNode = child;
            return child;
        }

        public MindMapNodeViewModel? AddSiblingAt(MindMapNodeViewModel? node, Point? position)
        {
            if (node == null)
                return null;

            if (node.Parent == null)
            {
                return AddRootAt(position);
            }

            var parent = node.Parent;
            var sibling = CreateNode(GetDefaultTitle(), parent);
            parent.Children.Add(sibling);
            AddConnection(parent, sibling);
            PositionNode(sibling, parent, position);
            SelectedNode = sibling;
            return sibling;
        }

        private void RemoveNode()
        {
            if (SelectedNode == null)
                return;

            var current = SelectedNode;
            var parent = current.Parent;

            if (parent == null)
            {
                int index = Nodes.IndexOf(current);
                Nodes.Remove(current);
                DetachNode(current);
                current.Parent = null;

                if (Nodes.Count == 0)
                {
                    SelectedNode = null;
                }
                else
                {
                    if (index >= Nodes.Count)
                        index = Nodes.Count - 1;
                    SelectedNode = Nodes[index];
                }
            }
            else
            {
                int index = parent.Children.IndexOf(current);
                parent.Children.Remove(current);
                DetachNode(current);
                current.Parent = null;

                if (parent.Children.Count == 0)
                {
                    SelectedNode = parent;
                }
                else
                {
                    if (index >= parent.Children.Count)
                        index = parent.Children.Count - 1;
                    SelectedNode = parent.Children[index];
                }
            }
        }

        private void AttachNode(MindMapNodeViewModel node)
        {
            node.SelectionChanged += NodeOnSelectionChanged;
            CanvasNodes.Add(node);
        }

        private void DetachNode(MindMapNodeViewModel node)
        {
            node.SelectionChanged -= NodeOnSelectionChanged;
            node.PropertyChanged -= PathNodeOnPropertyChanged;
            RemoveConnectionsFor(node);
            CanvasNodes.Remove(node);
            foreach (var child in node.Children.ToList())
            {
                DetachNode(child);
            }
        }

        private void NodeOnSelectionChanged(object? sender, bool isSelected)
        {
            if (!isSelected || _suppressSelectionSync)
                return;

            if (sender is MindMapNodeViewModel node && node != SelectedNode)
                SelectedNode = node;
        }

        private void UpdateCommandStates()
        {
            _addChildCommand.NotifyCanExecuteChanged();
            _addSiblingCommand.NotifyCanExecuteChanged();
            _removeNodeCommand.NotifyCanExecuteChanged();
        }

        private void UpdatePathSubscriptions()
        {
            foreach (var node in _pathSubscriptions)
            {
                node.PropertyChanged -= PathNodeOnPropertyChanged;
            }
            _pathSubscriptions.Clear();

            if (SelectedNode == null)
                return;

            foreach (var node in SelectedNode.GetPath())
            {
                node.PropertyChanged += PathNodeOnPropertyChanged;
                _pathSubscriptions.Add(node);
            }
        }

        private void PathNodeOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MindMapNodeViewModel.Title))
                OnPropertyChanged(nameof(SelectedPath));
        }

        private void PositionNode(MindMapNodeViewModel node, MindMapNodeViewModel? parent, Point? explicitPosition)
        {
            if (explicitPosition.HasValue)
            {
                node.X = Math.Max(0, explicitPosition.Value.X);
                node.Y = Math.Max(0, explicitPosition.Value.Y);
                return;
            }

            if (parent == null)
            {
                var index = Nodes.IndexOf(node);
                var spacingX = 240;
                var startX = 280;
                node.X = startX + index * spacingX;
                node.Y = CanvasHeight / 2;
                return;
            }

            var childIndex = parent.Children.IndexOf(node);
            var verticalSpacing = 140;
            var centerOffset = (parent.Children.Count - 1) / 2.0;
            node.X = parent.X + Math.Max(parent.VisualWidth, 180) + 120;
            node.Y = parent.Y + (childIndex - centerOffset) * verticalSpacing;
        }

        private void AddConnection(MindMapNodeViewModel source, MindMapNodeViewModel target)
        {
            var connection = new MindMapConnectionViewModel(source, target);
            Connections.Add(connection);
        }

        private void RemoveConnectionsFor(MindMapNodeViewModel node)
        {
            var matches = Connections
                .Where(c => c.Source == node || c.Target == node)
                .ToList();
            foreach (var connection in matches)
            {
                connection.Dispose();
                Connections.Remove(connection);
            }
        }

        public IEnumerable<MindMapNodeViewModel> Flatten()
        {
            foreach (var node in Nodes)
            {
                foreach (var descendant in Flatten(node))
                    yield return descendant;
            }
        }

        private IEnumerable<MindMapNodeViewModel> Flatten(MindMapNodeViewModel node)
        {
            yield return node;

            foreach (var child in node.Children)
            {
                foreach (var descendant in Flatten(child))
                    yield return descendant;
            }
        }
    }

    public class MindMapNodeViewModel : BaseViewModel
    {
        private string _title;
        private string _notes = string.Empty;
        private bool _isExpanded;
        private bool _isSelected;
        private double _x;
        private double _y;
        private double _visualWidth = 180;
        private double _visualHeight = 120;

        public MindMapNodeViewModel(string title)
        {
            _title = title;
        }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Notes
        {
            get => _notes;
            set
            {
                if (_notes != value)
                {
                    _notes = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<MindMapNodeViewModel> Children { get; } = new();

        public MindMapNodeViewModel? Parent { get; internal set; }

        public double X
        {
            get => _x;
            set
            {
                if (Math.Abs(_x - value) > 0.1)
                {
                    _x = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                if (Math.Abs(_y - value) > 0.1)
                {
                    _y = value;
                    OnPropertyChanged();
                }
            }
        }

        public double VisualWidth
        {
            get => _visualWidth;
            set
            {
                if (Math.Abs(_visualWidth - value) > 0.1)
                {
                    _visualWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public double VisualHeight
        {
            get => _visualHeight;
            set
            {
                if (Math.Abs(_visualHeight - value) > 0.1)
                {
                    _visualHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    SelectionChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<bool>? SelectionChanged;

        public IEnumerable<MindMapNodeViewModel> GetPath()
        {
            var stack = new Stack<MindMapNodeViewModel>();
            var current = this;
            while (current != null)
            {
                stack.Push(current);
                current = current.Parent;
            }
            return stack;
        }
    }
}
