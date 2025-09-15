using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

        public ObservableCollection<MindMapNodeViewModel> Nodes { get; } = new();

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

        public ICommand AddRootNodeCommand { get; }
        public ICommand AddChildNodeCommand => _addChildCommand;
        public ICommand AddSiblingNodeCommand => _addSiblingCommand;
        public ICommand RemoveNodeCommand => _removeNodeCommand;

        public MindMapViewModel()
        {
            AddRootNodeCommand = new RelayCommand(AddRootNode);
            _addChildCommand = new RelayCommand(AddChildNode, () => SelectedNode != null);
            _addSiblingCommand = new RelayCommand(AddSiblingNode, () => SelectedNode != null);
            _removeNodeCommand = new RelayCommand(RemoveNode, () => SelectedNode != null);

            var coreIdea = CreateNode("Central Idea");
            coreIdea.Notes = "Capture the main question, opportunity, or challenge you are exploring.";
            Nodes.Add(coreIdea);

            var themes = CreateNode("Key Themes", coreIdea);
            themes.Notes = "Break the central idea into major themes, components, or workstreams.";
            coreIdea.Children.Add(themes);

            var actions = CreateNode("Next Actions", coreIdea);
            actions.Notes = "Record immediate tasks, owners, and follow-ups.";
            coreIdea.Children.Add(actions);

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

        private void AddRootNode()
        {
            var node = CreateNode(GetDefaultTitle());
            node.IsExpanded = true;
            Nodes.Add(node);
            SelectedNode = node;
        }

        private void AddChildNode()
        {
            if (SelectedNode == null)
                return;

            var child = CreateNode(GetDefaultTitle(), SelectedNode);
            SelectedNode.Children.Add(child);
            SelectedNode.IsExpanded = true;
            SelectedNode = child;
        }

        private void AddSiblingNode()
        {
            if (SelectedNode == null)
                return;

            if (SelectedNode.Parent == null)
            {
                var node = CreateNode(GetDefaultTitle());
                node.IsExpanded = true;
                Nodes.Add(node);
                SelectedNode = node;
            }
            else
            {
                var parent = SelectedNode.Parent;
                var sibling = CreateNode(GetDefaultTitle(), parent);
                parent.Children.Add(sibling);
                SelectedNode = sibling;
            }
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
        }

        private void DetachNode(MindMapNodeViewModel node)
        {
            node.SelectionChanged -= NodeOnSelectionChanged;
            node.PropertyChanged -= PathNodeOnPropertyChanged;
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
            _addChildCommand.RaiseCanExecuteChanged();
            _addSiblingCommand.RaiseCanExecuteChanged();
            _removeNodeCommand.RaiseCanExecuteChanged();
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
