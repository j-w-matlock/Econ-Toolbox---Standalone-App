using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using EconToolbox.Desktop.Helpers;
using EconToolbox.Desktop.Models;
using Microsoft.Win32;

namespace EconToolbox.Desktop.ViewModels
{
    public class ConceptualModelViewModel : BaseViewModel
    {
        private readonly RelayCommand _addNodeCommand;
        private readonly RelayCommand _removeNodeCommand;
        private readonly RelayCommand _addLinkCommand;
        private readonly RelayCommand _removeLinkCommand;
        private readonly RelayCommand _addVertexCommand;
        private readonly RelayCommand _removeVertexCommand;
        private readonly RelayCommand _attachImageCommand;
        private readonly RelayCommand _clearImageCommand;

        private ConceptualNode? _selectedNode;
        private ConceptualLink? _selectedLink;
        private ConceptualVertex? _selectedVertex;
        private ConceptualNode? _linkStartSelection;
        private ConceptualNode? _linkEndSelection;

        public ConceptualModelViewModel()
        {
            Nodes = new ObservableCollection<ConceptualNode>();
            Links = new ObservableCollection<ConceptualLink>();

            NodeShapes = new ObservableCollection<ConceptualNodeShape>(
                Enum.GetValues(typeof(ConceptualNodeShape)).Cast<ConceptualNodeShape>());

            NodeFillOptions = new ObservableCollection<StyleOption>
            {
                new("Steel Blue", new SolidColorBrush(Color.FromRgb(75, 116, 181))),
                new("Soft Blue", new SolidColorBrush(Color.FromRgb(210, 230, 246))),
                new("Olive", new SolidColorBrush(Color.FromRgb(182, 205, 125))),
                new("Sand", new SolidColorBrush(Color.FromRgb(241, 228, 185))),
                new("Slate", new SolidColorBrush(Color.FromRgb(204, 213, 219)))
            };

            NodeStrokeOptions = new ObservableCollection<StyleOption>
            {
                new("Deep Blue", new SolidColorBrush(Color.FromRgb(54, 95, 160))),
                new("Green", new SolidColorBrush(Color.FromRgb(67, 146, 100))),
                new("Rust", new SolidColorBrush(Color.FromRgb(173, 93, 58))),
                new("Charcoal", new SolidColorBrush(Color.FromRgb(70, 70, 70)))
            };

            LineStrokeOptions = new ObservableCollection<StyleOption>
            {
                new("Emerald", new SolidColorBrush(Color.FromRgb(54, 142, 92))),
                new("Cobalt", new SolidColorBrush(Color.FromRgb(74, 115, 170))),
                new("Amber", new SolidColorBrush(Color.FromRgb(186, 142, 45))),
                new("Slate", new SolidColorBrush(Color.FromRgb(90, 96, 108)))
            };

            LineStyleOptions = new ObservableCollection<LineStyleOption>
            {
                new("Solid", new DoubleCollection()),
                new("Dashed", new DoubleCollection { 6, 4 }),
                new("Dotted", new DoubleCollection { 2, 4 })
            };

            _addNodeCommand = new RelayCommand(AddNode);
            _removeNodeCommand = new RelayCommand(RemoveSelectedNode, () => SelectedNode != null);
            _addLinkCommand = new RelayCommand(AddLink, CanAddLink);
            _removeLinkCommand = new RelayCommand(RemoveSelectedLink, () => SelectedLink != null);
            _addVertexCommand = new RelayCommand(AddVertex, () => SelectedLink != null);
            _removeVertexCommand = new RelayCommand(RemoveSelectedVertex, () => SelectedVertex != null);
            _attachImageCommand = new RelayCommand(AttachImage, () => SelectedNode != null);
            _clearImageCommand = new RelayCommand(ClearImage, () => SelectedNode?.HasImage == true);

            SeedDefaults();
        }

        public ObservableCollection<ConceptualNode> Nodes { get; }
        public ObservableCollection<ConceptualLink> Links { get; }

        public ObservableCollection<ConceptualNodeShape> NodeShapes { get; }
        public ObservableCollection<StyleOption> NodeFillOptions { get; }
        public ObservableCollection<StyleOption> NodeStrokeOptions { get; }
        public ObservableCollection<StyleOption> LineStrokeOptions { get; }
        public ObservableCollection<LineStyleOption> LineStyleOptions { get; }

        public ConceptualNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (ReferenceEquals(_selectedNode, value))
                {
                    return;
                }

                _selectedNode = value;
                OnPropertyChanged();
                _removeNodeCommand.NotifyCanExecuteChanged();
                _attachImageCommand.NotifyCanExecuteChanged();
                _clearImageCommand.NotifyCanExecuteChanged();
            }
        }

        public ConceptualLink? SelectedLink
        {
            get => _selectedLink;
            set
            {
                if (ReferenceEquals(_selectedLink, value))
                {
                    return;
                }

                _selectedLink = value;
                OnPropertyChanged();
                SelectedVertex = null;
                _removeLinkCommand.NotifyCanExecuteChanged();
                _addVertexCommand.NotifyCanExecuteChanged();
            }
        }

        public ConceptualVertex? SelectedVertex
        {
            get => _selectedVertex;
            set
            {
                if (ReferenceEquals(_selectedVertex, value))
                {
                    return;
                }

                _selectedVertex = value;
                OnPropertyChanged();
                _removeVertexCommand.NotifyCanExecuteChanged();
            }
        }

        public ConceptualNode? LinkStartSelection
        {
            get => _linkStartSelection;
            set
            {
                if (ReferenceEquals(_linkStartSelection, value))
                {
                    return;
                }

                _linkStartSelection = value;
                OnPropertyChanged();
                _addLinkCommand.NotifyCanExecuteChanged();
            }
        }

        public ConceptualNode? LinkEndSelection
        {
            get => _linkEndSelection;
            set
            {
                if (ReferenceEquals(_linkEndSelection, value))
                {
                    return;
                }

                _linkEndSelection = value;
                OnPropertyChanged();
                _addLinkCommand.NotifyCanExecuteChanged();
            }
        }

        public RelayCommand AddNodeCommand => _addNodeCommand;
        public RelayCommand RemoveNodeCommand => _removeNodeCommand;
        public RelayCommand AddLinkCommand => _addLinkCommand;
        public RelayCommand RemoveLinkCommand => _removeLinkCommand;
        public RelayCommand AddVertexCommand => _addVertexCommand;
        public RelayCommand RemoveVertexCommand => _removeVertexCommand;
        public RelayCommand AttachImageCommand => _attachImageCommand;
        public RelayCommand ClearImageCommand => _clearImageCommand;

        public void AddVertexAt(ConceptualLink? link, Point position)
        {
            if (link == null)
            {
                return;
            }

            var insertIndex = CalculateVertexInsertIndex(link, position);
            var vertex = new ConceptualVertex { X = position.X, Y = position.Y };
            link.Vertices.Insert(insertIndex, vertex);
            SelectedLink = link;
            SelectedVertex = vertex;
        }

        private void SeedDefaults()
        {
            var source = new ConceptualNode
            {
                Name = "Community Forum",
                X = 80,
                Y = 80,
                Fill = NodeFillOptions[1].Brush,
                Stroke = NodeStrokeOptions[0].Brush,
                Width = 110,
                Height = 110,
                Shape = ConceptualNodeShape.Circle
            };
            var channel = new ConceptualNode
            {
                Name = "Amplification Hub",
                X = 340,
                Y = 120,
                Fill = NodeFillOptions[2].Brush,
                Stroke = NodeStrokeOptions[1].Brush,
                Width = 120,
                Height = 120,
                Shape = ConceptualNodeShape.Circle
            };
            var influencer = new ConceptualNode
            {
                Name = "Influencer",
                X = 600,
                Y = 80,
                Fill = NodeFillOptions[3].Brush,
                Stroke = NodeStrokeOptions[2].Brush,
                Width = 120,
                Height = 120,
                Shape = ConceptualNodeShape.Circle
            };
            var funding = new ConceptualNode
            {
                Name = "Funding Stream",
                X = 420,
                Y = 340,
                Fill = NodeFillOptions[4].Brush,
                Stroke = NodeStrokeOptions[3].Brush,
                Width = 130,
                Height = 90,
                Shape = ConceptualNodeShape.RoundedRectangle
            };

            Nodes.Add(source);
            Nodes.Add(channel);
            Nodes.Add(influencer);
            Nodes.Add(funding);

            var link1 = new ConceptualLink(source, channel);
            link1.Vertices.Add(new ConceptualVertex { X = 250, Y = 110 });
            var link2 = new ConceptualLink(channel, influencer)
            {
                Stroke = LineStrokeOptions[0].Brush,
                DashArray = LineStyleOptions[0].DashArray
            };
            link2.Vertices.Add(new ConceptualVertex { X = 510, Y = 135 });
            var link3 = new ConceptualLink(funding, channel)
            {
                Stroke = LineStrokeOptions[2].Brush,
                DashArray = LineStyleOptions[1].DashArray
            };
            link3.Vertices.Add(new ConceptualVertex { X = 390, Y = 240 });
            link3.Vertices.Add(new ConceptualVertex { X = 360, Y = 200 });

            Links.Add(link1);
            Links.Add(link2);
            Links.Add(link3);

            SelectedNode = channel;
            SelectedLink = link1;
            LinkStartSelection = source;
            LinkEndSelection = channel;
        }

        private void AddNode()
        {
            var offset = Nodes.Count * 25;
            var node = new ConceptualNode
            {
                Name = $"Node {Nodes.Count + 1}",
                X = 120 + offset,
                Y = 220 + offset,
                Fill = NodeFillOptions[1].Brush,
                Stroke = NodeStrokeOptions[0].Brush
            };

            Nodes.Add(node);
            SelectedNode = node;
        }

        private void RemoveSelectedNode()
        {
            if (SelectedNode == null)
            {
                return;
            }

            var node = SelectedNode;
            var linksToRemove = Links.Where(link => link.StartNode == node || link.EndNode == node).ToList();
            foreach (var link in linksToRemove)
            {
                Links.Remove(link);
            }

            Nodes.Remove(node);
            SelectedNode = Nodes.LastOrDefault();
            if (ReferenceEquals(LinkStartSelection, node))
            {
                LinkStartSelection = null;
            }

            if (ReferenceEquals(LinkEndSelection, node))
            {
                LinkEndSelection = null;
            }
        }

        private bool CanAddLink()
        {
            return LinkStartSelection != null && LinkEndSelection != null && !ReferenceEquals(LinkStartSelection, LinkEndSelection);
        }

        private void AddLink()
        {
            if (!CanAddLink())
            {
                return;
            }

            var link = new ConceptualLink(LinkStartSelection, LinkEndSelection)
            {
                Stroke = LineStrokeOptions[0].Brush,
                DashArray = LineStyleOptions[0].DashArray
            };
            Links.Add(link);
            SelectedLink = link;
        }

        private void RemoveSelectedLink()
        {
            if (SelectedLink == null)
            {
                return;
            }

            var link = SelectedLink;
            Links.Remove(link);
            SelectedLink = Links.LastOrDefault();
        }

        private void AddVertex()
        {
            if (SelectedLink?.StartNode == null || SelectedLink.EndNode == null)
            {
                return;
            }

            var start = SelectedLink.StartNode;
            var end = SelectedLink.EndNode;
            var vertex = new ConceptualVertex
            {
                X = (start.X + end.X + start.Width + end.Width) / 2,
                Y = (start.Y + end.Y + start.Height + end.Height) / 2
            };

            SelectedLink.Vertices.Add(vertex);
            SelectedVertex = vertex;
        }

        private void RemoveSelectedVertex()
        {
            if (SelectedLink == null || SelectedVertex == null)
            {
                return;
            }

            var vertex = SelectedVertex;
            SelectedLink.Vertices.Remove(vertex);
            SelectedVertex = SelectedLink.Vertices.LastOrDefault();
        }

        private void AttachImage()
        {
            if (SelectedNode == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Select an image for this node"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedNode.ImagePath = dialog.FileName;
            }

            _clearImageCommand.NotifyCanExecuteChanged();
        }

        private void ClearImage()
        {
            if (SelectedNode == null)
            {
                return;
            }

            SelectedNode.ImagePath = null;
            _clearImageCommand.NotifyCanExecuteChanged();
        }

        private static int CalculateVertexInsertIndex(ConceptualLink link, Point position)
        {
            var points = link.Points;
            if (points == null || points.Count < 2)
            {
                return link.Vertices.Count;
            }

            var bestDistance = double.MaxValue;
            var bestSegmentIndex = 0;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var distance = DistanceToSegment(position, points[i], points[i + 1]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSegmentIndex = i;
                }
            }

            return Math.Min(bestSegmentIndex, link.Vertices.Count);
        }

        private static double DistanceToSegment(Point point, Point segmentStart, Point segmentEnd)
        {
            var dx = segmentEnd.X - segmentStart.X;
            var dy = segmentEnd.Y - segmentStart.Y;
            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
            {
                return (point - segmentStart).Length;
            }

            var t = ((point.X - segmentStart.X) * dx + (point.Y - segmentStart.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));
            var projection = new Point(segmentStart.X + t * dx, segmentStart.Y + t * dy);
            return (point - projection).Length;
        }

        public sealed class StyleOption
        {
            public StyleOption(string name, Brush brush)
            {
                Name = name;
                Brush = brush;
            }

            public string Name { get; }
            public Brush Brush { get; }
        }

        public sealed class LineStyleOption
        {
            public LineStyleOption(string name, DoubleCollection dashArray)
            {
                Name = name;
                DashArray = dashArray;
            }

            public string Name { get; }
            public DoubleCollection DashArray { get; }
        }
    }
}
