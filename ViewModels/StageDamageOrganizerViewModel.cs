using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using EconToolbox.Desktop.Themes;
using Microsoft.VisualBasic.FileIO;

namespace EconToolbox.Desktop.ViewModels
{
    public class StageDamageOrganizerViewModel : DiagnosticViewModelBase
    {
        private readonly ObservableCollection<StageDamageRecord> _records = new();
        private readonly ObservableCollection<string> _aepHeaders = new();
        private List<AepColumn> _structureAepColumns = new();
        private List<AepColumn> _contentAepColumns = new();
        private List<AepColumn> _otherAepColumns = new();
        private List<AepColumn> _vehicleAepColumns = new();
        private List<AepColumn> _depthAboveFirstFloorColumns = new();
        private string _statusMessage = "Load one or more Stage Damage Functions_StructureStageDamageDetails.csv files to summarize damages by category.";
        private bool _isBusy;
        private double _mapZoom = 1d;
        private StageDamageMapPoint? _selectedMapPoint;
        private double _mapMinX;
        private double _mapMaxX;
        private double _mapMinY;
        private double _mapMaxY;

        private const double DefaultMapCanvasWidth = 980d;
        private const double DefaultMapCanvasHeight = 620d;
        private const double MapPadding = 24d;

        private static readonly string[] ChartSeriesBrushKeys =
        {
            "App.Chart.Series1",
            "App.Chart.Series2",
            "App.Chart.Series3",
            "App.Chart.Series4",
            "App.Chart.Series5",
            "App.Chart.Series6"
        };

        public ObservableCollection<StageDamageRecord> Records => _records;
        public ObservableCollection<StageDamageCategorySummary> CategorySummaries { get; } = new();
        public ObservableCollection<StageDamageAepStructureCountSummary> StructureCountByAepSummaries { get; } = new();
        public ObservableCollection<StageDamageCategorySummary> ContentCategorySummaries { get; } = new();
        public ObservableCollection<StageDamageCategorySummary> OtherCategorySummaries { get; } = new();
        public ObservableCollection<StageDamageCategorySummary> VehicleCategorySummaries { get; } = new();
        public ObservableCollection<StageDamageHighlight> Highlights { get; } = new();
        public ObservableCollection<StageDamageMapPoint> TopStructureMapPoints { get; } = new();
        public ObservableCollection<ChartSeries> ChartSeries { get; } = new();
        public ObservableCollection<LegendItem> LegendItems { get; } = new();
        public ObservableCollection<string> AepHeaders => _aepHeaders;
        public ObservableCollection<string> ImpactAreaOptions { get; } = new();

        public ObservableCollection<StageDamageSummaryInfo> Summaries { get; } = new();

        private const string AllImpactAreasOption = "All Impact Areas";
        private string _selectedImpactArea = AllImpactAreasOption;

        public string SelectedImpactArea
        {
            get => _selectedImpactArea;
            set
            {
                if (SetProperty(ref _selectedImpactArea, value))
                {
                    ComputeSummaries();
                }
            }
        }

        public string FrequentAepLabelSummary
        {
            get
            {
                if (AepHeaders.Count == 0)
                {
                    return "Bars show the sum of structure damages across all available AEPs for each DamageCategory.";
                }

                var listedAeps = string.Join(", ", AepHeaders);
                return $"Bars show the sum of structure damages across all AEPs ({listedAeps}) for each DamageCategory.";
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool HasRecords => Records.Count > 0;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public double MapZoom
        {
            get => _mapZoom;
            set
            {
                double clamped = Math.Clamp(value, 0.5d, 12d);
                if (SetProperty(ref _mapZoom, clamped))
                {
                    OnPropertyChanged(nameof(MapZoomLabel));
                }
            }
        }

        public string MapZoomLabel => $"Zoom: {MapZoom:0.0}x";

        public StageDamageMapPoint? SelectedMapPoint
        {
            get => _selectedMapPoint;
            set
            {
                if (SetProperty(ref _selectedMapPoint, value))
                {
                    OnPropertyChanged(nameof(SelectedMapPointSummary));
                }
            }
        }

        public string SelectedMapPointSummary
            => SelectedMapPoint is null
                ? "Select a point to inspect coordinates and structure ID."
                : $"Selected: {SelectedMapPoint.SummaryName} | FID {SelectedMapPoint.StructureFid} | X: {SelectedMapPoint.XCoordinate:0.###}, Y: {SelectedMapPoint.YCoordinate:0.###}";

        public double MapCanvasWidth => DefaultMapCanvasWidth;

        public double MapCanvasHeight => DefaultMapCanvasHeight;

        public string MapPointCountSummary => $"{TopStructureMapPoints.Count} mapped top-damage structures";

        public string MapExtentTopLeft => $"NW: X {MapMinX:0.###}, Y {MapMaxY:0.###}";

        public string MapExtentBottomRight => $"SE: X {MapMaxX:0.###}, Y {MapMinY:0.###}";

        public double MapMinX
        {
            get => _mapMinX;
            private set => SetProperty(ref _mapMinX, value);
        }

        public double MapMaxX
        {
            get => _mapMaxX;
            private set => SetProperty(ref _mapMaxX, value);
        }

        public double MapMinY
        {
            get => _mapMinY;
            private set => SetProperty(ref _mapMinY, value);
        }

        public double MapMaxY
        {
            get => _mapMaxY;
            private set => SetProperty(ref _mapMaxY, value);
        }

        public IAsyncRelayCommand ImportCsvCommand { get; }
        public IRelayCommand ClearCommand { get; }
        public IAsyncRelayCommand SaveSummaryCommand { get; }
        public IRelayCommand ZoomInMapCommand { get; }
        public IRelayCommand ZoomOutMapCommand { get; }
        public IRelayCommand ResetMapViewCommand { get; }

        public StageDamageOrganizerViewModel()
        {
            ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync);
            ClearCommand = new RelayCommand(ClearAll, () => Records.Count > 0);
            SaveSummaryCommand = new AsyncRelayCommand(SaveSummaryAsync, () => Records.Count > 0);
            ZoomInMapCommand = new RelayCommand(() => MapZoom += 0.5d);
            ZoomOutMapCommand = new RelayCommand(() => MapZoom -= 0.5d);
            ResetMapViewCommand = new RelayCommand(ResetMapView);

            ImpactAreaOptions.Add(AllImpactAreasOption);

            Records.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasRecords));
                (ClearCommand as RelayCommand)?.NotifyCanExecuteChanged();
                (SaveSummaryCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                RefreshDiagnostics();
            };

            AepHeaders.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(FrequentAepLabelSummary));
                RefreshDiagnostics();
            };
            Summaries.CollectionChanged += OnSummariesChanged;
            RefreshDiagnostics();
        }

        private void ClearAll()
        {
            Records.Clear();
            StructureCountByAepSummaries.Clear();
            CategorySummaries.Clear();
            ContentCategorySummaries.Clear();
            OtherCategorySummaries.Clear();
            VehicleCategorySummaries.Clear();
            Highlights.Clear();
            ChartSeries.Clear();
            TopStructureMapPoints.Clear();
            SelectedMapPoint = null;
            MapZoom = 1d;
            MapMinX = 0d;
            MapMaxX = 0d;
            MapMinY = 0d;
            MapMaxY = 0d;
            LegendItems.Clear();
            AepHeaders.Clear();
            ImpactAreaOptions.Clear();
            ImpactAreaOptions.Add(AllImpactAreasOption);
            SelectedImpactArea = AllImpactAreasOption;
            Summaries.Clear();
            _structureAepColumns.Clear();
            _contentAepColumns.Clear();
            _otherAepColumns.Clear();
            _vehicleAepColumns.Clear();
            _depthAboveFirstFloorColumns.Clear();
            StatusMessage = "Cleared results. Load new CSV files to regenerate summaries.";
            RefreshDiagnostics();
        }

        public override object CaptureState()
        {
            return new StageDamageOrganizerData
            {
                StatusMessage = StatusMessage,
                AepHeaders = AepHeaders.ToList(),
                Summaries = Summaries.Select(summary => new StageDamageSummaryInfoData
                {
                    SourceKey = summary.SourceKey,
                    SourceLabel = summary.SourceLabel,
                    Name = summary.Name
                }).ToList(),
                Records = Records.Select(record => new StageDamageRecordData
                {
                    StructureFid = record.StructureFid,
                    DamageCategory = record.DamageCategory,
                    Description = record.Description,
                    ImpactArea = record.ImpactArea,
                    OccTypeName = record.OccTypeName,
                    SummaryName = record.SummaryName,
                    SourceKey = record.SourceKey,
                    XCoordinate = record.XCoordinate,
                    YCoordinate = record.YCoordinate,
                    AepDamages = record.AepDamages.Select(aep => new StageDamageAepValueData
                    {
                        Label = aep.Label,
                        Value = aep.Value
                    }).ToList(),
                    ContentAepDamages = record.ContentAepDamages.Select(aep => new StageDamageAepValueData
                    {
                        Label = aep.Label,
                        Value = aep.Value
                    }).ToList(),
                    OtherAepDamages = record.OtherAepDamages.Select(aep => new StageDamageAepValueData
                    {
                        Label = aep.Label,
                        Value = aep.Value
                    }).ToList(),
                    VehicleAepDamages = record.VehicleAepDamages.Select(aep => new StageDamageAepValueData
                    {
                        Label = aep.Label,
                        Value = aep.Value
                    }).ToList(),
                    DepthAboveFirstFloorByAep = record.DepthAboveFirstFloorByAep.Select(aep => new StageDamageAepValueData
                    {
                        Label = aep.Label,
                        Value = aep.Value
                    }).ToList()
                }).ToList()
            };
        }

        public override void RestoreState(object state)
        {
            if (state is not StageDamageOrganizerData data)
            {
                return;
            }

            ClearAll();

            _structureAepColumns = data.AepHeaders
                .Select(label => new AepColumn(NormalizeHeader($"Structure Damage At {label}"), label))
                .ToList();

            foreach (var header in data.AepHeaders)
            {
                AepHeaders.Add(header);
            }

            foreach (var summary in data.Summaries)
            {
                var info = new StageDamageSummaryInfo(summary.SourceKey, summary.SourceLabel)
                {
                    Name = summary.Name
                };
                AttachSummaryHandlers(info);
                Summaries.Add(info);
            }

            foreach (var recordData in data.Records)
            {
                Records.Add(new StageDamageRecord
                {
                    StructureFid = recordData.StructureFid,
                    DamageCategory = recordData.DamageCategory,
                    Description = recordData.Description,
                    ImpactArea = recordData.ImpactArea,
                    OccTypeName = recordData.OccTypeName,
                    SummaryName = recordData.SummaryName,
                    SourceKey = recordData.SourceKey,
                    XCoordinate = recordData.XCoordinate,
                    YCoordinate = recordData.YCoordinate,
                    AepDamages = recordData.AepDamages.Select(aep => new StageDamageAepValue(aep.Label, aep.Value)).ToList(),
                    ContentAepDamages = (recordData.ContentAepDamages ?? new List<StageDamageAepValueData>())
                        .Select(aep => new StageDamageAepValue(aep.Label, aep.Value))
                        .ToList(),
                    OtherAepDamages = (recordData.OtherAepDamages ?? new List<StageDamageAepValueData>())
                        .Select(aep => new StageDamageAepValue(aep.Label, aep.Value))
                        .ToList(),
                    VehicleAepDamages = (recordData.VehicleAepDamages ?? new List<StageDamageAepValueData>())
                        .Select(aep => new StageDamageAepValue(aep.Label, aep.Value))
                        .ToList(),
                    DepthAboveFirstFloorByAep = (recordData.DepthAboveFirstFloorByAep ?? new List<StageDamageAepValueData>())
                        .Select(aep => new StageDamageAepValue(aep.Label, aep.Value))
                        .ToList()
                });
            }

            _contentAepColumns = BuildColumnsFromRecords(Records.SelectMany(r => r.ContentAepDamages));
            _otherAepColumns = BuildColumnsFromRecords(Records.SelectMany(r => r.OtherAepDamages));
            _vehicleAepColumns = BuildColumnsFromRecords(Records.SelectMany(r => r.VehicleAepDamages));

            PopulateImpactAreaOptions();
            ComputeSummaries();
            StatusMessage = string.IsNullOrWhiteSpace(data.StatusMessage)
                ? "Project data loaded."
                : data.StatusMessage;
            RefreshDiagnostics();
        }

        private async Task ImportCsvAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Multiselect = true,
                Title = "Select Stage Damage Functions_StructureStageDamageDetails.csv files"
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var imported = await Task.Run(() => LoadRecords(dialog.FileNames));

                Records.Clear();
                AepHeaders.Clear();
                Summaries.Clear();

                foreach (var summary in imported.Summaries)
                {
                    AttachSummaryHandlers(summary);
                    Summaries.Add(summary);
                }

                foreach (var record in imported.Records)
                {
                    Records.Add(record);
                }

                PopulateImpactAreaOptions();

                foreach (var header in _structureAepColumns.Select(c => c.Label))
                {
                    AepHeaders.Add(header);
                }

                ComputeSummaries();
                StatusMessage = $"Loaded {Records.Count} rows from {dialog.FileNames.Length} file(s).";
                RefreshDiagnostics();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load CSV data: {ex.Message}";
                RefreshDiagnostics();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveSummaryAsync()
        {
            if (Records.Count == 0)
            {
                StatusMessage = "No data to export. Load a CSV first.";
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "StageDamageSummaries.xlsx",
                Title = "Save summarized stage damage results"
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return;
            }

            try
            {
                await Task.Run(() => WriteSummaryWorkbook(dialog.FileName));
                StatusMessage = $"Saved summary to \"{Path.GetFileName(dialog.FileName)}\".";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save summary: {ex.Message}";
            }
            RefreshDiagnostics();
        }

        protected override IEnumerable<DiagnosticItem> BuildDiagnostics()
        {
            var diagnostics = new List<DiagnosticItem>();

            if (Records.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Info,
                    "No CSV data loaded",
                    "Import Stage Damage Functions CSV files to generate summaries."));
                return diagnostics;
            }

            if (AepHeaders.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Warning,
                    "Missing AEP headers",
                    "No AEP columns were detected. Confirm the CSV files contain AEP damage columns."));
            }

            if (Summaries.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Warning,
                    "No summaries generated",
                    "Imported records did not produce any summary totals. Check file contents."));
            }

            if (diagnostics.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Info,
                    "Stage damage inputs look good",
                    "Records and AEP columns are ready for review and export."));
            }

            return diagnostics;
        }

        private void WriteSummaryWorkbook(string filePath)
        {
            using var workbook = new XLWorkbook();
            var usedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            WriteDamageSummaryWorksheet(workbook, usedTableNames, "Structure Damage Summary", "Structure", CategorySummaries);
            WriteStructureCountByAepWorksheet(workbook, usedTableNames);
            WriteDamageSummaryWorksheet(workbook, usedTableNames, "Content Damage Summary", "Content", ContentCategorySummaries);
            WriteDamageSummaryWorksheet(workbook, usedTableNames, "Other Damage Summary", "Other", OtherCategorySummaries);
            WriteDamageSummaryWorksheet(workbook, usedTableNames, "Vehicle Damage Summary", "Vehicle", VehicleCategorySummaries);
            WriteHighlightsWorksheet(workbook, usedTableNames);

            workbook.SaveAs(filePath);
        }

        private void WriteDamageSummaryWorksheet(XLWorkbook workbook, HashSet<string> usedTableNames, string worksheetName, string damageLabel, IEnumerable<StageDamageCategorySummary> summaries)
        {
            var worksheet = workbook.Worksheets.Add(ExcelNamingPolicy.CreateWorksheetName(workbook, worksheetName));

            var headerColumns = new List<string> { "Summary Name", "Impact Area", "Damage Category", "Structures" };
            headerColumns.AddRange(AepHeaders.Select(h => $"{damageLabel} Damage @{h}"));
            headerColumns.Add("Total AEP Sum");
            WriteHeaders(worksheet, headerColumns);

            int row = 2;
            foreach (var summary in summaries)
            {
                int col = 1;
                worksheet.Cell(row, col++).Value = summary.SummaryName;
                worksheet.Cell(row, col++).Value = summary.ImpactArea;
                worksheet.Cell(row, col++).Value = summary.DamageCategory;
                worksheet.Cell(row, col++).Value = summary.StructureCount;

                foreach (var value in summary.AepDamages)
                {
                    worksheet.Cell(row, col++).Value = value;
                }

                worksheet.Cell(row, col).Value = summary.FrequentSumDamage;
                row++;
            }

            CreateFormattedTable(worksheet, usedTableNames, $"{damageLabel}DamageSummary", row - 1, headerColumns.Count);
        }

        private void WriteStructureCountByAepWorksheet(XLWorkbook workbook, HashSet<string> usedTableNames)
        {
            var worksheet = workbook.Worksheets.Add(ExcelNamingPolicy.CreateWorksheetName(workbook, "Structure Count by AEP"));
            var headerColumns = new List<string> { "Summary Name", "Impact Area", "Structures" };
            headerColumns.AddRange(AepHeaders.Select(h => $"Damaged Structures @{h}"));
            WriteHeaders(worksheet, headerColumns);

            int row = 2;
            foreach (var summary in StructureCountByAepSummaries)
            {
                int col = 1;
                worksheet.Cell(row, col++).Value = summary.SummaryName;
                worksheet.Cell(row, col++).Value = summary.ImpactArea;
                worksheet.Cell(row, col++).Value = summary.StructureCount;

                foreach (var value in summary.StructureCountsByAep)
                {
                    worksheet.Cell(row, col++).Value = value;
                }

                row++;
            }

            CreateFormattedTable(worksheet, usedTableNames, "StructureCountByAEP", row - 1, headerColumns.Count);
        }

        private void WriteHighlightsWorksheet(XLWorkbook workbook, HashSet<string> usedTableNames)
        {
            var worksheet = workbook.Worksheets.Add(ExcelNamingPolicy.CreateWorksheetName(workbook, "Top Structures"));
            var headers = new[]
            {
                "Summary Name", "Structure FID", "Damage Category", "Impact Area", "Description",
                "Highest AEP", "Depth Above First Floor", "Structure Damage", "X Coordinate", "Y Coordinate"
            };
            WriteHeaders(worksheet, headers);

            int row = 2;
            foreach (var highlight in Highlights)
            {
                worksheet.Cell(row, 1).Value = highlight.SummaryName;
                worksheet.Cell(row, 2).Value = highlight.StructureFid;
                worksheet.Cell(row, 3).Value = highlight.DamageCategory;
                worksheet.Cell(row, 4).Value = highlight.ImpactArea;
                worksheet.Cell(row, 5).Value = highlight.Description;
                worksheet.Cell(row, 6).Value = highlight.HighestAepLabel;
                worksheet.Cell(row, 7).Value = highlight.DepthAboveFirstFloorAtHighestAep;
                worksheet.Cell(row, 8).Value = highlight.HighestStructureDamage;
                worksheet.Cell(row, 9).Value = highlight.XCoordinate;
                worksheet.Cell(row, 10).Value = highlight.YCoordinate;
                row++;
            }

            CreateFormattedTable(worksheet, usedTableNames, "TopStructuresByDamage", row - 1, headers.Length);
        }

        private static void WriteHeaders(IXLWorksheet worksheet, IReadOnlyList<string> headers)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }
        }

        private static void CreateFormattedTable(IXLWorksheet worksheet, HashSet<string> usedTableNames, string baseTableName, int lastRow, int columnCount)
        {
            int effectiveLastRow = Math.Max(lastRow, 2);
            var tableRange = worksheet.Range(1, 1, effectiveLastRow, columnCount);
            var tableName = ExcelNamingPolicy.CreateUniqueName(
                baseTableName,
                usedTableNames,
                ExcelNamingPolicy.ExcelMaxTableNameLength,
                "Tbl",
                requireLetterStart: true);

            var table = tableRange.CreateTable(tableName);
            table.Theme = XLTableTheme.TableStyleLight11;
            worksheet.Columns().AdjustToContents();
        }

        private void ComputeSummaries()
        {
            CategorySummaries.Clear();
            StructureCountByAepSummaries.Clear();
            ContentCategorySummaries.Clear();
            OtherCategorySummaries.Clear();
            VehicleCategorySummaries.Clear();
            Highlights.Clear();
            ChartSeries.Clear();
            TopStructureMapPoints.Clear();
            LegendItems.Clear();

            if (Records.Count == 0 || _structureAepColumns.Count == 0)
            {
                StatusMessage = "No rows available. Load CSV files to generate results.";
                return;
            }

            var filteredRecords = GetFilteredRecords().ToList();
            if (filteredRecords.Count == 0)
            {
                StatusMessage = "No rows match the selected impact area filter.";
                return;
            }

            var groupedBySummary = filteredRecords
                .GroupBy(r => string.IsNullOrWhiteSpace(r.SummaryName) ? "StageDamageSummary" : r.SummaryName.Trim())
                .ToList();

            var categoryTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var summaryBundles = new List<(string SummaryName, List<StageDamageCategorySummary> Categories)>();
            int paletteIndex = 0;

            foreach (var summaryGroup in groupedBySummary)
            {
                var structureSummaries = BuildCategorySummaries(summaryGroup, _structureAepColumns, r => r.AepDamages, summaryGroup.Key);
                var structureCountByAepSummaries = BuildStructureCountByAepSummaries(summaryGroup, _structureAepColumns, summaryGroup.Key);
                var contentSummaries = BuildCategorySummaries(summaryGroup, _contentAepColumns, r => r.ContentAepDamages, summaryGroup.Key);
                var otherSummaries = BuildCategorySummaries(summaryGroup, _otherAepColumns, r => r.OtherAepDamages, summaryGroup.Key);
                var vehicleSummaries = BuildCategorySummaries(summaryGroup, _vehicleAepColumns, r => r.VehicleAepDamages, summaryGroup.Key);

                summaryBundles.Add((summaryGroup.Key, structureSummaries));

                foreach (var summary in structureCountByAepSummaries)
                {
                    StructureCountByAepSummaries.Add(summary);
                }

                foreach (var summary in structureSummaries)
                {
                    CategorySummaries.Add(summary);
                    categoryTotals[summary.DamageCategory] = categoryTotals.TryGetValue(summary.DamageCategory, out var current)
                        ? current + summary.FrequentSumDamage
                        : summary.FrequentSumDamage;
                }

                foreach (var summary in contentSummaries)
                {
                    ContentCategorySummaries.Add(summary);
                }

                foreach (var summary in otherSummaries)
                {
                    OtherCategorySummaries.Add(summary);
                }

                foreach (var summary in vehicleSummaries)
                {
                    VehicleCategorySummaries.Add(summary);
                }

                var topStructures = summaryGroup
                    .OrderByDescending(r => r.FrequentPeakDamage)
                    .ThenBy(r => r.StructureFid)
                    .Take(10)
                    .Select(r => new StageDamageHighlight
                    {
                        SummaryName = summaryGroup.Key,
                        StructureFid = r.StructureFid,
                        DamageCategory = r.DamageCategory,
                        Description = r.Description,
                        ImpactArea = r.ImpactArea,
                        HighestAepLabel = r.FrequentPeakAepLabel,
                        DepthAboveFirstFloorAtHighestAep = r.FrequentPeakDepthAboveFirstFloor,
                        HighestStructureDamage = r.FrequentPeakDamage,
                        XCoordinate = r.XCoordinate,
                        YCoordinate = r.YCoordinate
                    });

                foreach (var highlight in topStructures)
                {
                    Highlights.Add(highlight);
                }
            }

            BuildTopStructureMapPoints();

            var categoryOrder = categoryTotals
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var bundle in summaryBundles)
            {
                string brushKey = ChartSeriesBrushKeys[paletteIndex % ChartSeriesBrushKeys.Length];
                var points = categoryOrder
                    .Select((category, index) =>
                    {
                        var match = bundle.Categories.FirstOrDefault(c => c.DamageCategory.Equals(category, StringComparison.OrdinalIgnoreCase));
                        return new ChartDataPoint
                        {
                            X = index,
                            Y = match?.FrequentSumDamage ?? 0d,
                            Label = category
                        };
                    })
                    .ToList();

                var chartSeries = new ChartSeries
                {
                    Name = bundle.SummaryName,
                    Stroke = ThemeResourceHelper.GetBrush(brushKey, System.Windows.Media.Brushes.SteelBlue),
                    Points = points
                };

                ChartSeries.Add(chartSeries);
                LegendItems.Add(new LegendItem
                {
                    Name = chartSeries.Name,
                    Color = chartSeries.Stroke ?? System.Windows.Media.Brushes.SteelBlue
                });
                paletteIndex++;
            }
        }


        private void BuildTopStructureMapPoints()
        {
            TopStructureMapPoints.Clear();
            SelectedMapPoint = null;
            MapZoom = 1d;
            var points = Highlights
                .Where(h => !(double.IsNaN(h.XCoordinate) || double.IsInfinity(h.XCoordinate) || double.IsNaN(h.YCoordinate) || double.IsInfinity(h.YCoordinate)))
                .ToList();

            if (points.Count == 0)
            {
                OnPropertyChanged(nameof(MapPointCountSummary));
                OnPropertyChanged(nameof(MapExtentTopLeft));
                OnPropertyChanged(nameof(MapExtentBottomRight));
                return;
            }

            MapMinX = points.Min(p => p.XCoordinate);
            MapMaxX = points.Max(p => p.XCoordinate);
            MapMinY = points.Min(p => p.YCoordinate);
            MapMaxY = points.Max(p => p.YCoordinate);

            double xRange = Math.Max(MapMaxX - MapMinX, 1d);
            double yRange = Math.Max(MapMaxY - MapMinY, 1d);

            foreach (var point in points)
            {
                double scaledX = MapPadding + ((point.XCoordinate - MapMinX) / xRange) * (MapCanvasWidth - (MapPadding * 2));
                double scaledY = MapPadding + ((MapMaxY - point.YCoordinate) / yRange) * (MapCanvasHeight - (MapPadding * 2));

                TopStructureMapPoints.Add(new StageDamageMapPoint
                {
                    SummaryName = point.SummaryName,
                    StructureFid = point.StructureFid,
                    XCoordinate = point.XCoordinate,
                    YCoordinate = point.YCoordinate,
                    ScaledX = scaledX,
                    ScaledY = scaledY
                });
            }

            SelectedMapPoint = TopStructureMapPoints.FirstOrDefault();
            OnPropertyChanged(nameof(MapPointCountSummary));
            OnPropertyChanged(nameof(MapExtentTopLeft));
            OnPropertyChanged(nameof(MapExtentBottomRight));
        }

        private void ResetMapView()
        {
            MapZoom = 1d;
        }

        private static List<StageDamageCategorySummary> BuildCategorySummaries(
            IEnumerable<StageDamageRecord> records,
            IReadOnlyList<AepColumn> columns,
            Func<StageDamageRecord, IReadOnlyList<StageDamageAepValue>> selector,
            string summaryName)
        {
            return records
                                .GroupBy(r => new
                {
                    ImpactArea = NormalizeImpactArea(r.ImpactArea),
                    DamageCategory = string.IsNullOrWhiteSpace(r.DamageCategory) ? "Uncategorized" : r.DamageCategory.Trim()
                })
                .Select(g => new StageDamageCategorySummary
                {
                    SummaryName = summaryName,
                    ImpactArea = g.Key.ImpactArea,
                    DamageCategory = g.Key.DamageCategory,
                    StructureCount = g.Count(),
                    AepDamages = columns.Select((_, index) => g.Sum(r => index < selector(r).Count ? selector(r)[index].Value : 0d)).ToList(),
                    FrequentSumDamage = g.Sum(r => selector(r).Sum(v => v.Value))
                })
                .OrderByDescending(s => s.FrequentSumDamage)
                .ToList();
        }

        private static List<StageDamageAepStructureCountSummary> BuildStructureCountByAepSummaries(
            IEnumerable<StageDamageRecord> records,
            IReadOnlyList<AepColumn> columns,
            string summaryName)
        {
            return records
                .GroupBy(r => NormalizeImpactArea(r.ImpactArea))
                .Select(g => new StageDamageAepStructureCountSummary
                {
                    SummaryName = summaryName,
                    ImpactArea = g.Key,
                    StructureCount = g.Count(),
                    StructureCountsByAep = columns
                        .Select((_, index) => g.Count(r => index < r.AepDamages.Count && r.AepDamages[index].Value != 0d))
                        .ToList()
                })
                .OrderBy(s => s.ImpactArea)
                .ToList();
        }

        private IEnumerable<StageDamageRecord> GetFilteredRecords()
        {
            if (string.IsNullOrWhiteSpace(SelectedImpactArea)
                || SelectedImpactArea.Equals(AllImpactAreasOption, StringComparison.OrdinalIgnoreCase))
            {
                return Records;
            }

            return Records.Where(r => string.Equals(NormalizeImpactArea(r.ImpactArea), SelectedImpactArea, StringComparison.OrdinalIgnoreCase));
        }

        private void PopulateImpactAreaOptions()
        {
            var previousSelection = SelectedImpactArea;
            ImpactAreaOptions.Clear();
            ImpactAreaOptions.Add(AllImpactAreasOption);

            foreach (var impactArea in Records
                .Select(r => NormalizeImpactArea(r.ImpactArea))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                ImpactAreaOptions.Add(impactArea);
            }

            SelectedImpactArea = ImpactAreaOptions.Contains(previousSelection)
                ? previousSelection
                : AllImpactAreasOption;
        }

        private static string NormalizeImpactArea(string impactArea)
        {
            return string.IsNullOrWhiteSpace(impactArea) ? "Not Set" : impactArea.Trim();
        }

        private StageDamageLoadResult LoadRecords(IEnumerable<string> filePaths)
        {
            var results = new List<StageDamageRecord>();
            var summaries = new List<StageDamageSummaryInfo>();
            _structureAepColumns = new List<AepColumn>();
            _contentAepColumns = new List<AepColumn>();
            _otherAepColumns = new List<AepColumn>();
            _vehicleAepColumns = new List<AepColumn>();
            _depthAboveFirstFloorColumns = new List<AepColumn>();
            foreach (var path in filePaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                using var parser = new TextFieldParser(path)
                {
                    Delimiters = new[] { "," },
                    HasFieldsEnclosedInQuotes = true,
                    TrimWhiteSpace = true,
                    TextFieldType = FieldType.Delimited
                };

                var summaryInfo = new StageDamageSummaryInfo(path, Path.GetFileNameWithoutExtension(path));
                summaries.Add(summaryInfo);

                if (!TryReadHeader(parser, out var header))
                {
                    continue;
                }

                var headerMap = BuildHeaderMap(header!);
                var structureAepColumns = FindDamageColumns(header!, "structure");
                var contentAepColumns = FindDamageColumns(header!, "content");
                var otherAepColumns = FindDamageColumns(header!, "other");
                var vehicleAepColumns = FindDamageColumns(header!, "vehicle");
                var depthColumns = FindDepthAboveFirstFloorColumns(header!);
                if (structureAepColumns.Count == 0)
                {
                    continue;
                }

                MergeDamageColumns(structureAepColumns, _structureAepColumns, results, r => r.AepDamages);
                MergeDamageColumns(contentAepColumns, _contentAepColumns, results, r => r.ContentAepDamages);
                MergeDamageColumns(otherAepColumns, _otherAepColumns, results, r => r.OtherAepDamages);
                MergeDamageColumns(vehicleAepColumns, _vehicleAepColumns, results, r => r.VehicleAepDamages);
                MergeDepthAboveFirstFloorColumns(depthColumns, results);

                while (!parser.EndOfData)
                {
                    var row = parser.ReadFields();
                    if (row == null || row.Length == 0)
                    {
                        continue;
                    }

                    var record = ParseRow(row, headerMap, _structureAepColumns, _contentAepColumns, _otherAepColumns, _vehicleAepColumns, _depthAboveFirstFloorColumns, summaryInfo.Name, summaryInfo.SourceKey);
                    if (record != null)
                    {
                        results.Add(record);
                    }
                }
            }

            return new StageDamageLoadResult(results, summaries);
        }

        private static Dictionary<string, int> BuildHeaderMap(string[] header)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                var normalized = NormalizeHeader(header[i]);
                if (!map.ContainsKey(normalized))
                {
                    map[normalized] = i;
                }
            }

            return map;
        }

        private static List<AepColumn> BuildColumnsFromRecords(IEnumerable<StageDamageAepValue> values)
        {
            return values
                .Select(value => new AepColumn(NormalizeHeader(value.Label), value.Label))
                .DistinctBy(column => column.NormalizedKey)
                .ToList();
        }

        private static void MergeDamageColumns(
            IEnumerable<AepColumn> discoveredColumns,
            List<AepColumn> aggregateColumns,
            List<StageDamageRecord> existingRecords,
            Func<StageDamageRecord, List<StageDamageAepValue>> selector)
        {
            foreach (var column in discoveredColumns)
            {
                if (aggregateColumns.Any(c => c.NormalizedKey.Equals(column.NormalizedKey, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                aggregateColumns.Add(column);
                foreach (var record in existingRecords)
                {
                    selector(record).Add(new StageDamageAepValue(column.Label, 0d));
                }
            }
        }

        private void MergeDepthAboveFirstFloorColumns(IEnumerable<AepColumn> discoveredColumns, List<StageDamageRecord> existingRecords)
        {
            foreach (var column in discoveredColumns)
            {
                if (_depthAboveFirstFloorColumns.Any(c => c.NormalizedKey.Equals(column.NormalizedKey, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _depthAboveFirstFloorColumns.Add(column);
                foreach (var record in existingRecords)
                {
                    record.DepthAboveFirstFloorByAep.Add(new StageDamageAepValue(column.Label, 0d));
                }
            }
        }

        private static StageDamageRecord? ParseRow(
            string[] row,
            IReadOnlyDictionary<string, int> map,
            IReadOnlyList<AepColumn> structureAepColumns,
            IReadOnlyList<AepColumn> contentAepColumns,
            IReadOnlyList<AepColumn> otherAepColumns,
            IReadOnlyList<AepColumn> vehicleAepColumns,
            IReadOnlyList<AepColumn> depthAboveFirstFloorColumns,
            string summaryName,
            string sourceKey)
        {
            string structureFid = ReadString(row, map, "structurefid", "structureid");
            string damageCategory = ReadString(row, map, "damagecatagory", "damagecategory");

            if (string.IsNullOrWhiteSpace(structureFid) && string.IsNullOrWhiteSpace(damageCategory))
            {
                return null;
            }

            return new StageDamageRecord
            {
                StructureFid = string.IsNullOrWhiteSpace(structureFid) ? "Unknown" : structureFid.Trim(),
                DamageCategory = string.IsNullOrWhiteSpace(damageCategory) ? "Uncategorized" : damageCategory.Trim(),
                Description = ReadString(row, map, "description"),
                ImpactArea = ReadString(row, map, "impactarearownumberinimpactareaset", "impactarea"),
                OccTypeName = ReadString(row, map, "occtypename", "occupancytype"),
                XCoordinate = ReadDouble(row, map, "xcoordinate"),
                YCoordinate = ReadDouble(row, map, "ycoordinate"),
                AepDamages = structureAepColumns.Select(c => new StageDamageAepValue(c.Label, ReadDouble(row, map, c.NormalizedKey))).ToList(),
                ContentAepDamages = contentAepColumns.Select(c => new StageDamageAepValue(c.Label, ReadDouble(row, map, c.NormalizedKey))).ToList(),
                OtherAepDamages = otherAepColumns.Select(c => new StageDamageAepValue(c.Label, ReadDouble(row, map, c.NormalizedKey))).ToList(),
                VehicleAepDamages = vehicleAepColumns.Select(c => new StageDamageAepValue(c.Label, ReadDouble(row, map, c.NormalizedKey))).ToList(),
                DepthAboveFirstFloorByAep = depthAboveFirstFloorColumns.Select(c => new StageDamageAepValue(c.Label, ReadDouble(row, map, c.NormalizedKey))).ToList(),
                SummaryName = summaryName,
                SourceKey = sourceKey
            };
        }

        private static bool TryReadHeader(TextFieldParser parser, out string[]? header)
        {
            header = null;
            bool skippedFirst = false;

            while (!parser.EndOfData)
            {
                var candidate = parser.ReadFields();
                if (candidate == null)
                {
                    break;
                }

                if (!skippedFirst)
                {
                    skippedFirst = true;
                    continue;
                }

                // FDA exports typically start with a title row. Walk until we find a row that
                // looks like the actual header instead of skipping only a single line.
                if (candidate.Any(f => f.Contains("Structure", StringComparison.OrdinalIgnoreCase)))
                {
                    header = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeHeader(string header)
        {
            var filtered = header.Where(char.IsLetterOrDigit);
            return new string(filtered.ToArray()).ToLowerInvariant();
        }

        private static List<AepColumn> FindDamageColumns(string[] header, string damageType)
        {
            var columns = new List<AepColumn>();
            foreach (var column in header)
            {
                var normalized = NormalizeHeader(column);
                if (!normalized.Contains($"{damageType}damage") || !normalized.Contains("aep"))
                {
                    continue;
                }

                if (columns.Any(c => c.NormalizedKey.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var label = ExtractAepLabel(column);
                columns.Add(new AepColumn(normalized, label));
            }

            return columns;
        }

        private static List<AepColumn> FindDepthAboveFirstFloorColumns(string[] header)
        {
            var columns = new List<AepColumn>();
            foreach (var column in header)
            {
                var normalized = NormalizeHeader(column);
                if (!normalized.Contains("aep"))
                {
                    continue;
                }

                if (!normalized.Contains("depthabovefirstfloor")
                    && !normalized.Contains("depthabove1stfloor")
                    && !normalized.Contains("depthaboveff"))
                {
                    continue;
                }

                if (columns.Any(c => c.NormalizedKey.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var label = ExtractAepLabel(column);
                columns.Add(new AepColumn(normalized, label));
            }

            return columns;
        }

        private static string ExtractAepLabel(string header)
        {
            var match = Regex.Match(header, @"([0-9]*\.?[0-9]+)\s*AEP", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return $"{match.Groups[1].Value.Trim()} AEP";
            }

            return header.Trim();
        }

        private static string ReadString(string[] row, IReadOnlyDictionary<string, int> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out int index) && index < row.Length)
                {
                    return row[index];
                }
            }

            return string.Empty;
        }

        private static double ReadDouble(string[] row, IReadOnlyDictionary<string, int> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out int index) && index < row.Length)
                {
                    var raw = row[index];
                    if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double value) ||
                        double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                    {
                        if (double.IsNaN(value) || double.IsInfinity(value))
                        {
                            return 0d;
                        }

                        return value;
                    }
                }
            }

            return 0d;
        }

        private void OnSummariesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is StageDamageSummaryInfo info)
                    {
                        AttachSummaryHandlers(info);
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is StageDamageSummaryInfo info)
                    {
                        info.PropertyChanged -= OnSummaryPropertyChanged;
                    }
                }
            }
            RefreshDiagnostics();
        }

        private void AttachSummaryHandlers(StageDamageSummaryInfo info)
        {
            info.PropertyChanged -= OnSummaryPropertyChanged;
            info.PropertyChanged += OnSummaryPropertyChanged;
        }

        private void OnSummaryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(StageDamageSummaryInfo.Name) || sender is not StageDamageSummaryInfo info)
            {
                return;
            }

            var newName = string.IsNullOrWhiteSpace(info.Name) ? info.SourceLabel : info.Name.Trim();
            foreach (var record in Records.Where(r => r.SourceKey == info.SourceKey))
            {
                record.SummaryName = newName;
            }

            _contentAepColumns = BuildColumnsFromRecords(Records.SelectMany(r => r.ContentAepDamages));
            _otherAepColumns = BuildColumnsFromRecords(Records.SelectMany(r => r.OtherAepDamages));
            _vehicleAepColumns = BuildColumnsFromRecords(Records.SelectMany(r => r.VehicleAepDamages));

            PopulateImpactAreaOptions();
            ComputeSummaries();
            RefreshDiagnostics();
        }

        private record StageDamageLoadResult(IReadOnlyList<StageDamageRecord> Records, IReadOnlyList<StageDamageSummaryInfo> Summaries);
    }

    public partial class StageDamageSummaryInfo : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public StageDamageSummaryInfo(string sourceKey, string sourceLabel)
        {
            SourceKey = sourceKey;
            SourceLabel = sourceLabel;
            Name = sourceLabel;
        }

        public string SourceKey { get; }

        public string SourceLabel { get; }

        [ObservableProperty]
        private string _name = string.Empty;
    }

    internal record AepColumn(string NormalizedKey, string Label);
}
