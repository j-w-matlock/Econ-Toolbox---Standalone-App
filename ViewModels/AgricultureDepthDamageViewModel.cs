using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using BitMiracle.LibTiff.Classic;
using DotSpatial.Projections;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{
    public class AgricultureDepthDamageViewModel : DiagnosticViewModelBase, IComputeModule
    {
        private const int DaysInYear = 365;

        private readonly RelayCommand _computeCommand;
        private readonly RelayCommand _exportCommand;
        private bool _isInitializing = true;

        public ObservableCollection<RegionDefinition> Regions { get; }
        public ObservableCollection<CropDefinition> Crops { get; }
        public ObservableCollection<StageExposure> StageExposures { get; }
        public ObservableCollection<DepthDurationDamageRow> DepthDurationRows { get; } = new();
        public ObservableCollection<CropScapeDamageRow> CropScapeDamageRows { get; } = new();
        public ObservableCollection<CropScapeAcreageSummary> CropScapeSummaries { get; } = new();

        private readonly RelayCommand _addDepthDurationPointCommand;
        private readonly RelayCommand _removeDepthDurationPointCommand;
        private readonly AsyncRelayCommand _importCropScapeRasterCommand;
        private readonly RelayCommand _clearCropScapeSummaryCommand;
        private readonly CropScapeRasterService _cropScapeRasterService = new();
        private bool _isImportingCropScape;
        private string _cropScapeImportStatus = "No CropScape raster imported.";
        private double _cropScapeTotalAcreage;

        private readonly RelayCommand _computeEstimatorCommand;
        private readonly RelayCommand _importEstimatorCdlRasterCommand;
        private readonly RelayCommand _importEstimatorDepthRasterCommand;
        private readonly RelayCommand _importEstimatorPolygonShapefileCommand;
        private readonly FloodImpactAnalysisService _floodImpactAnalysisService = new();
        private readonly IAppProgressService _appProgressService;
        public ObservableCollection<EstimatorEventRow> EstimatorEvents { get; } = new();
        public ObservableCollection<EstimatorCropRow> EstimatorCropRows { get; } = new();
        public ObservableCollection<EstimatorResultRow> EstimatorResults { get; } = new();
        public ObservableCollection<EstimatorSpatialCropRow> EstimatorSpatialCropRows { get; } = new();
        public ObservableCollection<EstimatorCdlSummaryRow> EstimatorCdlSummaryRows { get; } = new();
        public ObservableCollection<EstimatorSummaryRow> EstimatorSummaryRows { get; } = new();

        private string _estimatorDefaultCurve = "0:0,1:0.5,2:1";
        private double _estimatorDefaultCropValue = 750;
        private double _estimatorDamageStdDev = 0.1;
        private int _estimatorMonteCarloRuns = 250;
        private int _estimatorAnalysisYears = 30;
        private int _estimatorRandomSeed = 42;
        private bool _estimatorRandomizeMonth;
        private double _estimatorDepthStdDev;
        private double _estimatorValueStdDev = 0.15;
        private string _estimatorSummary = "Configure crop rows and event rows, then run the estimator.";
        private string _estimatorCdlRasterPath = string.Empty;
        private string _estimatorDepthRasterPath = string.Empty;
        private string _estimatorPolygonShapefilePath = string.Empty;
        private double _estimatorUniformPolygonDepth;
        private string _estimatorSpatialStatus = "Load a CDL raster and either a depth raster or polygon shapefile.";
        private double _estimatorSpatialCropAcreage;
        private double _estimatorCdlTotalAcreage;
        private bool _estimatorUsePolygonUniformDepth;
        private string _estimatorProjectionSyncStatus = "Projection sync pending.";
        private Rect _estimatorCdlRect = new(20, 20, 280, 200);
        private Rect _estimatorDepthRect = new(35, 35, 280, 200);
        private PointCollection _estimatorPolygonPreviewPoints = new();

        private RegionDefinition? _selectedRegion;
        public RegionDefinition? SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                if (_selectedRegion == value)
                {
                    return;
                }

                DetachRegionHandlers(_selectedRegion);
                _selectedRegion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedRegionDescription));
                SelectedRegionPoint = null;
                AttachRegionHandlers(_selectedRegion);
                _computeCommand.NotifyCanExecuteChanged();
                _addDepthDurationPointCommand.NotifyCanExecuteChanged();
                _removeDepthDurationPointCommand.NotifyCanExecuteChanged();
                if (!_isInitializing)
                {
                    ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
                }
            }
        }

        public string? SelectedRegionDescription => SelectedRegion?.Description;

        private DepthDurationPoint? _selectedRegionPoint;
        public DepthDurationPoint? SelectedRegionPoint
        {
            get => _selectedRegionPoint;
            set
            {
                if (_selectedRegionPoint == value)
                {
                    return;
                }

                _selectedRegionPoint = value;
                OnPropertyChanged();
                _removeDepthDurationPointCommand.NotifyCanExecuteChanged();
            }
        }

        private CropDefinition? _selectedCrop;
        public CropDefinition? SelectedCrop
        {
            get => _selectedCrop;
            set
            {
                if (_selectedCrop == value)
                {
                    return;
                }

                DetachCropHandlers(_selectedCrop);
                _selectedCrop = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCropDescription));
                AttachCropHandlers(_selectedCrop);
                _computeCommand.NotifyCanExecuteChanged();
                if (!_isInitializing)
                {
                    ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
                }
            }
        }

        public string? SelectedCropDescription => SelectedCrop?.Description;

        private double _averageResponse = 1.0;
        public double AverageResponse
        {
            get => _averageResponse;
            set
            {
                double adjusted = double.IsFinite(value) ? Math.Max(0.1, value) : 1.0;
                if (Math.Abs(_averageResponse - adjusted) < 1e-6)
                {
                    return;
                }

                _averageResponse = adjusted;
                OnPropertyChanged();
                ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
            }
        }

        private int _simulationYears = 5000;
        public int SimulationYears
        {
            get => _simulationYears;
            set
            {
                int adjusted = value < 1 ? 1 : value;
                if (_simulationYears == adjusted)
                {
                    return;
                }

                _simulationYears = adjusted;
                OnPropertyChanged();
                ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
            }
        }

        private double _modeledImpactProbability;
        public double ModeledImpactProbability
        {
            get => _modeledImpactProbability;
            private set
            {
                if (Math.Abs(_modeledImpactProbability - value) < 1e-6)
                {
                    return;
                }

                _modeledImpactProbability = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModeledImpactProbabilityDisplay));
            }
        }

        public string ModeledImpactProbabilityDisplay => $"{ModeledImpactProbability * 100:0.##}%";

        private double _meanDamagePercent;
        public double MeanDamagePercent
        {
            get => _meanDamagePercent;
            private set
            {
                if (Math.Abs(_meanDamagePercent - value) < 1e-6)
                {
                    return;
                }

                _meanDamagePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MeanDamageDisplay));
            }
        }

        public string MeanDamageDisplay => $"{MeanDamagePercent:0.##}%";

        private string _impactSummary = "Select a region, crop, and seasonal resilience values to calculate flood impacts.";
        public string ImpactSummary
        {
            get => _impactSummary;
            private set
            {
                if (_impactSummary == value)
                {
                    return;
                }

                _impactSummary = value;
                OnPropertyChanged();
            }
        }

        private string _cropInsight = "Adjust the exposure and tolerance values to understand how resilience affects expected damages.";
        public string CropInsight
        {
            get => _cropInsight;
            private set
            {
                if (_cropInsight == value)
                {
                    return;
                }

                _cropInsight = value;
                OnPropertyChanged();
            }
        }

        public bool IsImportingCropScape
        {
            get => _isImportingCropScape;
            private set
            {
                if (_isImportingCropScape == value)
                {
                    return;
                }

                _isImportingCropScape = value;
                OnPropertyChanged();
                _importCropScapeRasterCommand.NotifyCanExecuteChanged();
                _clearCropScapeSummaryCommand.NotifyCanExecuteChanged();
            }
        }

        public string CropScapeImportStatus
        {
            get => _cropScapeImportStatus;
            private set
            {
                if (_cropScapeImportStatus == value)
                {
                    return;
                }

                _cropScapeImportStatus = value;
                OnPropertyChanged();
            }
        }

        public double CropScapeTotalAcreage
        {
            get => _cropScapeTotalAcreage;
            private set
            {
                if (Math.Abs(_cropScapeTotalAcreage - value) < 1e-6)
                {
                    return;
                }

                _cropScapeTotalAcreage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CropScapeTotalAcreageDisplay));
                foreach (var summary in CropScapeSummaries)
                {
                    summary.UpdateShare(_cropScapeTotalAcreage);
                }
                UpdateCropScapeDamageOutputs();
            }
        }

        public string CropScapeTotalAcreageDisplay => $"{CropScapeTotalAcreage:N1} acres";

        public bool HasCropScapeSummary => CropScapeSummaries.Count > 0;

        private PointCollection _cropScapeDamagePoints = new();
        public PointCollection CropScapeDamagePoints
        {
            get => _cropScapeDamagePoints;
            private set
            {
                if (Equals(_cropScapeDamagePoints, value))
                {
                    return;
                }

                _cropScapeDamagePoints = value;
                OnPropertyChanged();
            }
        }

        public bool HasCropScapeDamage => CropScapeDamageRows.Count > 0;

        public string EstimatorDefaultCurve
        {
            get => _estimatorDefaultCurve;
            set
            {
                if (_estimatorDefaultCurve == value) return;
                _estimatorDefaultCurve = value;
                OnPropertyChanged();
            }
        }

        public double EstimatorDefaultCropValue
        {
            get => _estimatorDefaultCropValue;
            set
            {
                double adjusted = double.IsFinite(value) ? Math.Max(0.0, value) : 0.0;
                if (Math.Abs(_estimatorDefaultCropValue - adjusted) < 1e-6) return;
                _estimatorDefaultCropValue = adjusted;
                OnPropertyChanged();
            }
        }

        public double EstimatorDamageStdDev
        {
            get => _estimatorDamageStdDev;
            set
            {
                double adjusted = double.IsFinite(value) ? Math.Max(0.0, value) : 0.0;
                if (Math.Abs(_estimatorDamageStdDev - adjusted) < 1e-6) return;
                _estimatorDamageStdDev = adjusted;
                OnPropertyChanged();
            }
        }

        public double EstimatorDepthStdDev
        {
            get => _estimatorDepthStdDev;
            set
            {
                double adjusted = double.IsFinite(value) ? Math.Max(0.0, value) : 0.0;
                if (Math.Abs(_estimatorDepthStdDev - adjusted) < 1e-6) return;
                _estimatorDepthStdDev = adjusted;
                OnPropertyChanged();
            }
        }

        public double EstimatorValueStdDev
        {
            get => _estimatorValueStdDev;
            set
            {
                double adjusted = double.IsFinite(value) ? Math.Max(0.0, value) : 0.0;
                if (Math.Abs(_estimatorValueStdDev - adjusted) < 1e-6) return;
                _estimatorValueStdDev = adjusted;
                OnPropertyChanged();
            }
        }

        public int EstimatorMonteCarloRuns
        {
            get => _estimatorMonteCarloRuns;
            set
            {
                int adjusted = value < 1 ? 1 : value;
                if (_estimatorMonteCarloRuns == adjusted) return;
                _estimatorMonteCarloRuns = adjusted;
                OnPropertyChanged();
            }
        }

        public int EstimatorAnalysisYears
        {
            get => _estimatorAnalysisYears;
            set
            {
                int adjusted = value < 1 ? 1 : value;
                if (_estimatorAnalysisYears == adjusted) return;
                _estimatorAnalysisYears = adjusted;
                OnPropertyChanged();
            }
        }

        public int EstimatorRandomSeed
        {
            get => _estimatorRandomSeed;
            set
            {
                if (_estimatorRandomSeed == value) return;
                _estimatorRandomSeed = value;
                OnPropertyChanged();
            }
        }

        public bool EstimatorRandomizeMonth
        {
            get => _estimatorRandomizeMonth;
            set
            {
                if (_estimatorRandomizeMonth == value) return;
                _estimatorRandomizeMonth = value;
                OnPropertyChanged();
            }
        }

        public string EstimatorSummary
        {
            get => _estimatorSummary;
            private set
            {
                if (_estimatorSummary == value) return;
                _estimatorSummary = value;
                OnPropertyChanged();
            }
        }

        public string EstimatorCdlRasterPath
        {
            get => _estimatorCdlRasterPath;
            private set
            {
                if (_estimatorCdlRasterPath == value) return;
                _estimatorCdlRasterPath = value;
                OnPropertyChanged();
            }
        }

        public string EstimatorDepthRasterPath
        {
            get => _estimatorDepthRasterPath;
            private set
            {
                if (_estimatorDepthRasterPath == value) return;
                _estimatorDepthRasterPath = value;
                OnPropertyChanged();
            }
        }

        public string EstimatorPolygonShapefilePath
        {
            get => _estimatorPolygonShapefilePath;
            private set
            {
                if (_estimatorPolygonShapefilePath == value) return;
                _estimatorPolygonShapefilePath = value;
                OnPropertyChanged();
            }
        }

        public bool EstimatorUsePolygonUniformDepth
        {
            get => _estimatorUsePolygonUniformDepth;
            set
            {
                if (_estimatorUsePolygonUniformDepth == value) return;
                _estimatorUsePolygonUniformDepth = value;
                OnPropertyChanged();
                RefreshEstimatorSpatialData();
            }
        }

        public double EstimatorUniformPolygonDepth
        {
            get => _estimatorUniformPolygonDepth;
            set
            {
                var adjusted = Math.Max(0, value);
                if (Math.Abs(_estimatorUniformPolygonDepth - adjusted) < 1e-6) return;
                _estimatorUniformPolygonDepth = adjusted;
                OnPropertyChanged();
                if (EstimatorUsePolygonUniformDepth) RefreshEstimatorSpatialData();
            }
        }

        public string EstimatorSpatialStatus
        {
            get => _estimatorSpatialStatus;
            private set
            {
                if (_estimatorSpatialStatus == value) return;
                _estimatorSpatialStatus = value;
                OnPropertyChanged();
            }
        }

        public double EstimatorSpatialCropAcreage
        {
            get => _estimatorSpatialCropAcreage;
            private set
            {
                if (Math.Abs(_estimatorSpatialCropAcreage - value) < 1e-6) return;
                _estimatorSpatialCropAcreage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EstimatorSpatialCropAcreageDisplay));
            }
        }

        public string EstimatorSpatialCropAcreageDisplay => $"{EstimatorSpatialCropAcreage:N1} acres";

        public double EstimatorCdlTotalAcreage
        {
            get => _estimatorCdlTotalAcreage;
            private set
            {
                if (Math.Abs(_estimatorCdlTotalAcreage - value) < 1e-6) return;
                _estimatorCdlTotalAcreage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EstimatorCdlTotalAcreageDisplay));
            }
        }

        public string EstimatorCdlTotalAcreageDisplay => $"{EstimatorCdlTotalAcreage:N1} acres";

        public string EstimatorProjectionSyncStatus
        {
            get => _estimatorProjectionSyncStatus;
            private set
            {
                if (_estimatorProjectionSyncStatus == value) return;
                _estimatorProjectionSyncStatus = value;
                OnPropertyChanged();
            }
        }

        public Rect EstimatorCdlRect { get => _estimatorCdlRect; private set { _estimatorCdlRect = value; OnPropertyChanged(); } }
        public Rect EstimatorDepthRect { get => _estimatorDepthRect; private set { _estimatorDepthRect = value; OnPropertyChanged(); } }
        public PointCollection EstimatorPolygonPreviewPoints { get => _estimatorPolygonPreviewPoints; private set { _estimatorPolygonPreviewPoints = value; OnPropertyChanged(); } }

        public ICommand ComputeCommand => _computeCommand;
        public ICommand ExportCommand => _exportCommand;
        public ICommand AddDepthDurationPointCommand => _addDepthDurationPointCommand;
        public ICommand RemoveDepthDurationPointCommand => _removeDepthDurationPointCommand;
        public IAsyncRelayCommand ImportCropScapeRasterCommand => _importCropScapeRasterCommand;
        public ICommand ClearCropScapeSummaryCommand => _clearCropScapeSummaryCommand;
        public ICommand ComputeEstimatorCommand => _computeEstimatorCommand;
        public ICommand ImportEstimatorCdlRasterCommand => _importEstimatorCdlRasterCommand;
        public ICommand ImportEstimatorDepthRasterCommand => _importEstimatorDepthRasterCommand;
        public ICommand ImportEstimatorPolygonShapefileCommand => _importEstimatorPolygonShapefileCommand;

        public AgricultureDepthDamageViewModel(IAppProgressService appProgressService)
        {
            _appProgressService = appProgressService;
            Regions = new ObservableCollection<RegionDefinition>(RegionDefinition.CreateDefaults());
            Crops = new ObservableCollection<CropDefinition>(CropDefinition.CreateDefaults());
            StageExposures = new ObservableCollection<StageExposure>(StageExposure.CreateDefaults());
            CropScapeSummaries.CollectionChanged += CropScapeSummaries_CollectionChanged;
            CropScapeDamageRows.CollectionChanged += CropScapeDamageRows_CollectionChanged;

            foreach (var stage in StageExposures)
            {
                stage.PropertyChanged += Stage_PropertyChanged;
            }

            _computeCommand = new RelayCommand(Compute, CanCompute);
            _exportCommand = new RelayCommand(Export, () => DepthDurationRows.Count > 0);
            _addDepthDurationPointCommand = new RelayCommand(AddDepthDurationPoint, () => SelectedRegion != null);
            _removeDepthDurationPointCommand = new RelayCommand(RemoveDepthDurationPoint, () => SelectedRegionPoint != null);
            _importCropScapeRasterCommand = new AsyncRelayCommand(ImportCropScapeRasterAsync, () => !IsImportingCropScape);
            _clearCropScapeSummaryCommand = new RelayCommand(ClearCropScapeSummary, () => CropScapeSummaries.Count > 0 && !IsImportingCropScape);
            _computeEstimatorCommand = new RelayCommand(ComputeFloodDamageEstimator);
            _importEstimatorCdlRasterCommand = new RelayCommand(ImportEstimatorCdlRaster);
            _importEstimatorDepthRasterCommand = new RelayCommand(ImportEstimatorDepthRaster);
            _importEstimatorPolygonShapefileCommand = new RelayCommand(ImportEstimatorPolygonShapefile);

            SeedEstimatorInputs();

            if (Regions.Count > 0)
            {
                SelectedRegion = Regions[0];
            }

            if (Crops.Count > 0)
            {
                SelectedCrop = Crops[0];
            }

            _isInitializing = false;
            Compute();
            RefreshDiagnostics();
        }

        private void AttachRegionHandlers(RegionDefinition? region)
        {
            if (region == null)
            {
                return;
            }

            region.PropertyChanged += Region_PropertyChanged;
            region.DepthDuration.CollectionChanged += RegionDepthDuration_CollectionChanged;
            foreach (var point in region.DepthDuration)
            {
                point.PropertyChanged += DepthDurationPoint_PropertyChanged;
            }
        }

        private void DetachRegionHandlers(RegionDefinition? region)
        {
            if (region == null)
            {
                return;
            }

            region.PropertyChanged -= Region_PropertyChanged;
            region.DepthDuration.CollectionChanged -= RegionDepthDuration_CollectionChanged;
            foreach (var point in region.DepthDuration)
            {
                point.PropertyChanged -= DepthDurationPoint_PropertyChanged;
            }
        }

        private void Region_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            MarkDirty();
            if (e.PropertyName == nameof(RegionDefinition.Description) || e.PropertyName == nameof(RegionDefinition.Name))
            {
                OnPropertyChanged(nameof(SelectedRegionDescription));
                ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
                RefreshDiagnostics();
                return;
            }

            if (e.PropertyName == nameof(RegionDefinition.FloodWindowStartDay)
                || e.PropertyName == nameof(RegionDefinition.FloodWindowEndDay)
                || e.PropertyName == nameof(RegionDefinition.FloodSeasonPeakDay)
                || e.PropertyName == nameof(RegionDefinition.SeasonShiftDays))
            {
                Compute();
                RefreshDiagnostics();
                return;
            }

            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
            RefreshDiagnostics();
        }

        private void RegionDepthDuration_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (DepthDurationPoint point in e.OldItems)
                {
                    point.PropertyChanged -= DepthDurationPoint_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (DepthDurationPoint point in e.NewItems)
                {
                    point.PropertyChanged += DepthDurationPoint_PropertyChanged;
                }
            }

            _removeDepthDurationPointCommand.NotifyCanExecuteChanged();

            if (_isInitializing)
            {
                return;
            }

            MarkDirty();
            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
            RefreshDiagnostics();
        }

        private void DepthDurationPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            MarkDirty();
            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
            RefreshDiagnostics();
        }

        private void AttachCropHandlers(CropDefinition? crop)
        {
            if (crop == null)
            {
                return;
            }

            crop.PropertyChanged += Crop_PropertyChanged;
        }

        private void DetachCropHandlers(CropDefinition? crop)
        {
            if (crop == null)
            {
                return;
            }

            crop.PropertyChanged -= Crop_PropertyChanged;
        }

        private void Crop_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            MarkDirty();
            if (e.PropertyName == nameof(CropDefinition.Description) || e.PropertyName == nameof(CropDefinition.Name))
            {
                OnPropertyChanged(nameof(SelectedCropDescription));
            }

            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
            RefreshDiagnostics();
        }

        private void Stage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            MarkDirty();
            if (e.PropertyName == nameof(StageExposure.TimingModifier)
                || e.PropertyName == nameof(StageExposure.OverlapFraction)
                || e.PropertyName == nameof(StageExposure.ShiftedStartDayOfYear)
                || e.PropertyName == nameof(StageExposure.ShiftedEndDayOfYear)
                || e.PropertyName == nameof(StageExposure.TimingWindowDisplay)
                || e.PropertyName == nameof(StageExposure.TimingModifierDisplay)
                || e.PropertyName == nameof(StageExposure.StageGuidance)
                || e.PropertyName == nameof(StageExposure.AppliedSeasonShiftDays))
            {
                return;
            }

            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
            RefreshDiagnostics();
        }

        private bool CanCompute() => SelectedRegion != null && SelectedCrop != null;

        private void AddDepthDurationPoint()
        {
            if (SelectedRegion == null)
            {
                return;
            }

            var last = SelectedRegion.DepthDuration.LastOrDefault();
            double nextDepth = last?.DepthFeet + 0.5 ?? 1.0;
            double nextDuration = last?.DurationDays + 1.0 ?? 3.0;
            double nextDamage = last?.BaseDamage ?? 0.25;

            var newPoint = new DepthDurationPoint(nextDepth, nextDuration, nextDamage);
            SelectedRegion.DepthDuration.Add(newPoint);
            SelectedRegionPoint = newPoint;
            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
        }

        private void RemoveDepthDurationPoint()
        {
            if (SelectedRegion == null || SelectedRegionPoint == null)
            {
                return;
            }

            SelectedRegion.DepthDuration.Remove(SelectedRegionPoint);
            SelectedRegionPoint = null;
            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
        }

        private async Task ImportCropScapeRasterAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select CropScape CDL raster",
                Filter = "CropScape CDL (*.tif;*.tiff)|*.tif;*.tiff|All files (*.*)|*.*"
            };

            bool? result = dialog.ShowDialog();
            if (result != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return;
            }

            string filePath = dialog.FileName;

            try
            {
                IsImportingCropScape = true;
                CropScapeImportStatus = $"Processing {Path.GetFileName(filePath)}…";
                _appProgressService.Start("Importing CropScape raster...", 10);

                var areas = await Task.Run(() => _cropScapeRasterService.ReadClassAreas(filePath));

                var summaries = CropScapeAcreageSummary.FromAreas(areas, out double totalAcres);

                _appProgressService.Report("Calculating crop class acreage summaries...", 65);
                CropScapeSummaries.Clear();

                if (summaries.Count == 0)
                {
                    CropScapeTotalAcreage = 0;
                    CropScapeImportStatus = $"No crop classes found in \"{Path.GetFileName(filePath)}\".";
                    _appProgressService.Complete("CropScape import complete (no classes found).");
                    return;
                }

                foreach (var summary in summaries)
                {
                    CropScapeSummaries.Add(summary);
                }

                CropScapeTotalAcreage = totalAcres;

                CropScapeImportStatus = $"Loaded {CropScapeSummaries.Count} crop classes from \"{Path.GetFileName(filePath)}\".";
                _appProgressService.Complete("CropScape import complete.");
            }
            catch (Exception ex)
            {
                CropScapeImportStatus = $"Failed to import CropScape raster: {ex.Message}";
                _appProgressService.Fail("CropScape import failed.");
            }
            finally
            {
                IsImportingCropScape = false;
            }
        }

        private void ClearCropScapeSummary()
        {
            CropScapeSummaries.Clear();
            CropScapeTotalAcreage = 0;
            CropScapeImportStatus = "CropScape acreage summary cleared.";
        }

        private void CropScapeSummaries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            MarkDirty();
            OnPropertyChanged(nameof(HasCropScapeSummary));
            _clearCropScapeSummaryCommand.NotifyCanExecuteChanged();
            RefreshDiagnostics();
        }

        private void CropScapeDamageRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            MarkDirty();
            OnPropertyChanged(nameof(HasCropScapeDamage));
            RefreshDiagnostics();
        }

        private void UpdateCropScapeDamageOutputs()
        {
            CropScapeDamageRows.Clear();

            if (CropScapeTotalAcreage <= 0 || DepthDurationRows.Count == 0)
            {
                CropScapeDamagePoints = new PointCollection();
                return;
            }

            var chartData = new List<(double Depth, double Acres)>();

            foreach (var row in DepthDurationRows)
            {
                double damageFraction = Math.Clamp(row.DamagePercent / 100.0, 0.0, 1.0);
                double damagedAcres = CropScapeTotalAcreage * damageFraction;
                double residualAcres = CropScapeTotalAcreage - damagedAcres;

                CropScapeDamageRows.Add(new CropScapeDamageRow(
                    row.DepthFeet,
                    row.DurationDays,
                    row.DamagePercent,
                    damagedAcres,
                    residualAcres,
                    CropScapeTotalAcreage));

                chartData.Add((row.DepthFeet, damagedAcres));
            }

            chartData.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            CropScapeDamagePoints = CreatePointCollection(chartData);
        }

        private static PointCollection CreatePointCollection(List<(double Depth, double Acres)> data)
        {
            var points = new PointCollection();
            if (data.Count == 0)
            {
                return points;
            }

            double maxDepth = Math.Max(data.Max(p => p.Depth), 0.1);
            double maxAcres = Math.Max(data.Max(p => p.Acres), 0.1);
            const double width = 300.0;
            const double height = 180.0;

            foreach (var point in data)
            {
                double normalizedX = point.Depth / maxDepth;
                double normalizedY = point.Acres / maxAcres;
                double x = normalizedX * width;
                double y = height - (normalizedY * height);
                points.Add(new Point(x, y));
            }

            return points;
        }

        private void Compute()
        {
            if (!CanCompute())
            {
                ModeledImpactProbability = 0;
                MeanDamagePercent = 0;
                DepthDurationRows.Clear();
                CropScapeDamageRows.Clear();
                CropScapeDamagePoints = new PointCollection();
                ImpactSummary = "Select a region and crop to calculate flood impacts.";
                CropInsight = "";
                _exportCommand.NotifyCanExecuteChanged();
                MarkClean();
                return;
            }

            try
            {
                double totalWeight = StageExposures.Sum(s => s.Stage.Weight);
                double weightedStress = 0.0;

                foreach (var stageExposure in StageExposures)
                {
                    var timing = EvaluateStageTiming(stageExposure.Stage, SelectedRegion!);
                    stageExposure.ApplyTiming(
                        timing.shiftedStartDay,
                        timing.shiftedEndDay,
                        timing.wrapsYear,
                        timing.overlapFraction,
                        timing.timingModifier,
                        SelectedRegion!.SeasonShiftDays);

                    double stageStress = stageExposure.GetStressRatio() * timing.timingModifier;
                    weightedStress += stageExposure.Stage.Weight * stageStress;
                }

                double normalizedStress = totalWeight > 0 ? weightedStress / totalWeight : 0;
                normalizedStress = Math.Clamp(normalizedStress, 0.0, 2.0);

                double responseScaling = Math.Clamp(AverageResponse, 0.1, 5.0);
                double baselineAep = Math.Clamp(SelectedRegion!.AnnualExceedanceProbability, 0.0, 1.0);
                double probabilityMultiplier = normalizedStress * SelectedRegion.ImpactModifier * SelectedCrop!.ImpactModifier * responseScaling;
                double probability = baselineAep * probabilityMultiplier;
                ModeledImpactProbability = Math.Clamp(probability, 0.0, 1.0);

#if DEBUG
            if (baselineAep > 0.0 && probabilityMultiplier > 0.0 && ModeledImpactProbability < 1.0)
            {
                double increasedAep = Math.Clamp(baselineAep * 1.1, 0.0, 1.0);
                double increasedProbability = Math.Clamp(increasedAep * probabilityMultiplier, 0.0, 1.0);
                Debug.Assert(increasedProbability > ModeledImpactProbability, "Increasing the baseline AEP should increase the modeled impact probability when not clamped at 1.");
            }
#endif

            DepthDurationRows.Clear();
            foreach (var point in SelectedRegion.DepthDuration)
            {
                double baseline = Math.Clamp(point.BaseDamage, 0.0, 1.0);
                double stressMultiplier = 0.6 + 0.4 * normalizedStress;
                double cropModifier = SelectedCrop.DamageFactor;
                double durationAdjustment = 1 + (point.DurationDays / SelectedRegion.MaxDuration) * 0.25;
                double damage = baseline * stressMultiplier * cropModifier * durationAdjustment * Math.Max(1.0, responseScaling * 0.5);
                damage = Math.Clamp(damage, 0.0, 1.0);

                DepthDurationRows.Add(new DepthDurationDamageRow(point.DepthFeet, point.DurationDays, damage * 100.0));
            }

            MeanDamagePercent = DepthDurationRows.Count > 0
                ? DepthDurationRows.Average(r => r.DamagePercent)
                : 0.0;

            double averageTimingModifier = StageExposures.Count > 0
                ? StageExposures.Average(s => s.TimingModifier)
                : 0.0;

            ImpactSummary =
                $"Simulated {SimulationYears:N0} seasons with {StageExposures.Count} growth stages. The baseline annual exceedance probability of {baselineAep:P2} scaled by stress, resilience, and seasonal timing yields a modeled impact probability of {ModeledImpactProbabilityDisplay}. Average timing modifier: ×{averageTimingModifier:0.##}.";

            var highestTimingStage = StageExposures
                .OrderByDescending(s => s.TimingModifier)
                .FirstOrDefault();

            string alignmentInsight = highestTimingStage == null
                ? string.Empty
                : $" The {highestTimingStage.StageName} stage currently overlaps about {highestTimingStage.OverlapFraction * 100:0.#}% of the {SelectedRegion!.Name} flood window (peak ≈ day {SelectedRegion.FloodSeasonPeakDay}), so its stress is weighted ×{highestTimingStage.TimingModifier:0.##}.";

            CropInsight =
                $"Average expected damage across representative depth-duration events is {MeanDamageDisplay}. Seasonal alignment considers the flood window (days {SelectedRegion!.FloodWindowStartDay}–{SelectedRegion.FloodWindowEndDay}) and any {SelectedRegion.SeasonShiftDays:+#;-#;0}-day shift.{alignmentInsight} Adjust exposure days or tolerance to explore resilience.";

                UpdateCropScapeDamageOutputs();
                _exportCommand.NotifyCanExecuteChanged();
            }
            finally
            {
                MarkClean();
            }
        }

        private void Export()
        {
            Compute();

            if (DepthDurationRows.Count == 0)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = "agriculture-depth-damage.csv",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var lines = new List<string>();

            if (CropScapeDamageRows.Count > 0)
            {
                lines.Add("Depth (ft),Duration (days),Damage (%),Damaged acres,Residual acres,Total acres");
                lines.AddRange(CropScapeDamageRows.Select(r => string.Join(',',
                    r.DepthFeet.ToString("0.##", CultureInfo.InvariantCulture),
                    r.DurationDays.ToString("0.##", CultureInfo.InvariantCulture),
                    r.DamagePercent.ToString("0.##", CultureInfo.InvariantCulture),
                    r.DamagedAcres.ToString("N1", CultureInfo.InvariantCulture),
                    r.ResidualAcres.ToString("N1", CultureInfo.InvariantCulture),
                    r.TotalAcres.ToString("N1", CultureInfo.InvariantCulture))));
            }
            else
            {
                lines.Add("Depth (ft),Duration (days),Damage (%)");
                lines.AddRange(DepthDurationRows.Select(r => string.Join(',',
                    r.DepthFeet.ToString("0.##", CultureInfo.InvariantCulture),
                    r.DurationDays.ToString("0.##", CultureInfo.InvariantCulture),
                    r.DamagePercent.ToString("0.##", CultureInfo.InvariantCulture))));
            }

            if (CropScapeSummaries.Count > 0)
            {
                if (lines.Count > 0)
                {
                    lines.Add(string.Empty);
                }

                lines.Add("CropScape acreage summary");
                lines.Add("Crop code,Crop name,Pixel count,Acres,Percent of total");
                lines.AddRange(CropScapeSummaries.Select(summary => string.Join(',',
                    summary.Code.ToString(CultureInfo.InvariantCulture),
                    summary.Name,
                    summary.PixelCount.ToString(CultureInfo.InvariantCulture),
                    summary.Acres.ToString("N1", CultureInfo.InvariantCulture),
                    summary.PercentOfTotal.ToString("P1", CultureInfo.InvariantCulture))));
            }

            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("Calculation notes");
            lines.Add("Modeled impact probability = baseline annual exceedance probability scaled by stress and timing modifiers.");
            lines.Add("Average damage = mean of depth-duration damage percentages.");

            File.WriteAllLines(dialog.FileName, lines);
        }

        private (int shiftedStartDay, int shiftedEndDay, bool wrapsYear, double overlapFraction, double timingModifier) EvaluateStageTiming(StageDefinition stage, RegionDefinition region)
        {
            int duration = Math.Max(1, stage.DurationDays);
            int rawStageStart = stage.StartDayOfYear + region.SeasonShiftDays;
            int rawStageEnd = rawStageStart + duration - 1;

            var stageSegments = BuildWindowSegments(rawStageStart, duration);
            var floodSegments = BuildWindowSegments(region.FloodWindowStartDay, CalculateInclusiveDuration(region.FloodWindowStartDay, region.FloodWindowEndDay));

            double overlapDays = 0;
            foreach (var stageSegment in stageSegments)
            {
                foreach (var floodSegment in floodSegments)
                {
                    overlapDays += CalculateSegmentOverlap(stageSegment, floodSegment);
                }
            }

            double overlapFraction = Math.Clamp(overlapDays / duration, 0.0, 1.0);

            int distanceToPeak = CalculateDistanceToSegments(region.FloodSeasonPeakDay, stageSegments);
            double peakWeight = Math.Clamp(1.0 - distanceToPeak / 120.0, 0.0, 1.0);
            double timingModifier = Math.Clamp(overlapFraction * (0.6 + 0.4 * peakWeight), 0.0, 1.0);

            return (
                NormalizeDayOfYear(rawStageStart),
                NormalizeDayOfYear(rawStageEnd),
                stageSegments.Count > 1,
                overlapFraction,
                timingModifier);
        }

        private static List<(int Start, int End)> BuildWindowSegments(int rawStartDay, int durationDays)
        {
            var segments = new List<(int Start, int End)>();
            int remaining = Math.Max(1, durationDays);
            int current = rawStartDay;

            while (remaining > 0)
            {
                int normalizedStart = NormalizeDayOfYear(current);
                int span = Math.Min(remaining, DaysUntilYearEnd(current));
                int normalizedEnd = NormalizeDayOfYear(current + span - 1);
                segments.Add((normalizedStart, normalizedEnd));
                remaining -= span;
                current += span;
            }

            return segments;
        }

        private static int CalculateSegmentOverlap((int Start, int End) a, (int Start, int End) b)
        {
            int start = Math.Max(a.Start, b.Start);
            int end = Math.Min(a.End, b.End);
            if (end < start)
            {
                return 0;
            }

            return end - start + 1;
        }

        private static int CalculateDistanceToSegments(int day, IReadOnlyList<(int Start, int End)> segments)
        {
            int normalizedDay = NormalizeDayOfYear(day);
            int minDistance = DaysInYear;

            foreach (var segment in segments)
            {
                if (normalizedDay >= segment.Start && normalizedDay <= segment.End)
                {
                    return 0;
                }

                int distanceToStart = CircularDayDistance(normalizedDay, segment.Start);
                int distanceToEnd = CircularDayDistance(normalizedDay, segment.End);
                minDistance = Math.Min(minDistance, Math.Min(distanceToStart, distanceToEnd));
            }

            return minDistance;
        }

        private static int CircularDayDistance(int dayA, int dayB)
        {
            int diff = Math.Abs(dayA - dayB);
            return Math.Min(diff, DaysInYear - diff);
        }

        private static int NormalizeDayOfYear(int day)
        {
            int normalized = day % DaysInYear;
            if (normalized <= 0)
            {
                normalized += DaysInYear;
            }

            return normalized;
        }

        private static int CalculateInclusiveDuration(int startDay, int endDay)
        {
            int normalizedStart = NormalizeDayOfYear(startDay);
            int normalizedEnd = NormalizeDayOfYear(endDay);

            if (normalizedStart <= normalizedEnd)
            {
                return (normalizedEnd - normalizedStart) + 1;
            }

            return (DaysInYear - normalizedStart + 1) + normalizedEnd;
        }

        private static int DaysUntilYearEnd(int rawDay)
        {
            int normalized = NormalizeDayOfYear(rawDay);
            return DaysInYear - normalized + 1;
        }

        private void ImportEstimatorCdlRaster()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff|All files (*.*)|*.*",
                Title = "Select CDL raster"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            EstimatorCdlRasterPath = dialog.FileName;
            EstimatorSpatialStatus = "CDL raster loaded.";
            RefreshEstimatorSpatialData();
        }

        private void ImportEstimatorDepthRaster()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff|All files (*.*)|*.*",
                Title = "Select depth raster"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            EstimatorDepthRasterPath = dialog.FileName;
            EstimatorSpatialStatus = "Depth raster loaded.";
            EstimatorUsePolygonUniformDepth = false;
            RefreshEstimatorSpatialData();
        }

        private void ImportEstimatorPolygonShapefile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Shapefile (*.shp)|*.shp|All files (*.*)|*.*",
                Title = "Select flood polygon shapefile"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            EstimatorPolygonShapefilePath = dialog.FileName;
            EstimatorUsePolygonUniformDepth = true;
            EstimatorSpatialStatus = "Flood polygon loaded; uniform depth mode active.";
            RefreshEstimatorSpatialData();
        }

        private void RefreshEstimatorSpatialData()
        {
            EstimatorSpatialCropRows.Clear();
            EstimatorCdlSummaryRows.Clear();
            EstimatorPolygonPreviewPoints = new PointCollection();
            if (string.IsNullOrWhiteSpace(EstimatorCdlRasterPath) || !File.Exists(EstimatorCdlRasterPath))
            {
                EstimatorSpatialStatus = "Load a CDL raster and either a depth raster or polygon shapefile.";
                return;
            }

            try
            {
                var cdl = ReadSingleBandRaster(EstimatorCdlRasterPath);
                RasterData? depth = null;
                PolygonShapeData? polygonData = null;
                if (!EstimatorUsePolygonUniformDepth && !string.IsNullOrWhiteSpace(EstimatorDepthRasterPath) && File.Exists(EstimatorDepthRasterPath))
                {
                    depth = ReadSingleBandRaster(EstimatorDepthRasterPath);
                    depth = ReprojectRasterIfNeeded(depth, cdl);
                }

                Rect? polygonRect = null;
                if (EstimatorUsePolygonUniformDepth && !string.IsNullOrWhiteSpace(EstimatorPolygonShapefilePath) && File.Exists(EstimatorPolygonShapefilePath))
                {
                    polygonData = ReadShapefilePolygon(EstimatorPolygonShapefilePath, cdl);
                    polygonRect = polygonData.Bounds;
                }

                double acresPerPixel = Math.Abs(cdl.PixelWidth * cdl.PixelHeight) / 4046.8564224;
                var cdlPixels = new Dictionary<int, long>();
                var byCrop = new Dictionary<int, (long Count,double DepthTotal)>();
                for (int y = 0; y < cdl.Height; y++)
                {
                    for (int x = 0; x < cdl.Width; x++)
                    {
                        int idx = y * cdl.Width + x;
                        int code = (int)Math.Round(cdl.Values[idx]);
                        if (code <= 0) continue;

                        cdlPixels.TryGetValue(code, out long pixelCount);
                        cdlPixels[code] = pixelCount + 1;

                        double sampledDepth = 0;
                        if (EstimatorUsePolygonUniformDepth)
                        {
                            if (polygonData == null || !CellInPolygon(cdl, x, y, polygonData.Vertices)) continue;
                            sampledDepth = EstimatorUniformPolygonDepth;
                        }
                        else if (depth != null)
                        {
                            sampledDepth = depth.Values[idx];
                        }

                        byCrop.TryGetValue(code, out var cur);
                        byCrop[code] = (cur.Count + 1, cur.DepthTotal + Math.Max(0, sampledDepth));
                    }
                }

                long totalPixels = cdlPixels.Values.Sum();
                foreach (var kvp in cdlPixels.OrderByDescending(k => k.Value))
                {
                    double acres = kvp.Value * acresPerPixel;
                    double percent = totalPixels > 0 ? kvp.Value / (double)totalPixels : 0;
                    EstimatorCdlSummaryRows.Add(new EstimatorCdlSummaryRow(kvp.Key, CropScapeLegend.Lookup(kvp.Key, new Dictionary<int, string>()), kvp.Value, acres, percent));
                }
                EstimatorCdlTotalAcreage = EstimatorCdlSummaryRows.Sum(row => row.Acres);

                foreach (var kvp in byCrop.OrderByDescending(k=>k.Value.Count))
                {
                    double acres = kvp.Value.Count * acresPerPixel;
                    double avgDepth = kvp.Value.Count > 0 ? kvp.Value.DepthTotal / kvp.Value.Count : 0;
                    EstimatorSpatialCropRows.Add(new EstimatorSpatialCropRow(kvp.Key, CropScapeLegend.Lookup(kvp.Key, new Dictionary<int,string>()), acres, avgDepth));
                }

                EstimatorSpatialCropAcreage = EstimatorSpatialCropRows.Sum(r => r.Acres);
                EstimatorCropRows.Clear();
                string firstEvent = EstimatorEvents.FirstOrDefault()?.Name ?? "Event";
                foreach (var row in EstimatorSpatialCropRows)
                {
                    EstimatorCropRows.Add(new EstimatorCropRow(row.CropCode, row.CropName, firstEvent, row.Acres, 0, "", ""));
                }

                var cdlBounds = GetRasterBounds(cdl);
                var cdlActiveBounds = GetRasterActiveBounds(cdl);
                var depthBounds = depth != null ? GetRasterBounds(depth) : (Rect?)null;
                var previewFrame = new Rect(20, 20, 300, 200);
                var worldBounds = cdlBounds;
                if (depthBounds.HasValue)
                {
                    worldBounds.Union(depthBounds.Value);
                }
                if (polygonRect.HasValue)
                {
                    worldBounds.Union(polygonRect.Value);
                }

                EstimatorCdlRect = ProjectWorldRect(cdlActiveBounds ?? cdlBounds, worldBounds, previewFrame);
                EstimatorDepthRect = depthBounds.HasValue ? ProjectWorldRect(depthBounds.Value, worldBounds, previewFrame) : new Rect(0, 0, 0, 0);
                if (polygonData != null)
                {
                    EstimatorPolygonPreviewPoints = BuildPolygonPreview(polygonData.Vertices, worldBounds, previewFrame);
                }

                var cdlProjection = string.IsNullOrWhiteSpace(cdl.ProjectionName) ? "Unknown" : cdl.ProjectionName;
                var depthProjection = depth != null ? (string.IsNullOrWhiteSpace(depth.SourceProjectionName) ? "Unknown" : depth.SourceProjectionName) : "N/A";
                EstimatorProjectionSyncStatus = depth == null
                    ? $"Projection sync: CDL={cdlProjection}; depth=N/A; polygon aligned to CDL coordinates."
                    : depth.WasReprojected
                        ? $"Projection sync: CDL={cdlProjection}; depth source={depthProjection}; depth was reprojected to CDL grid."
                        : $"Projection sync: CDL={cdlProjection}; depth source={depthProjection}; no reprojection required.";
                EstimatorSpatialStatus = $"Spatial sampling complete. {EstimatorSpatialCropRows.Count} impacted crop classes and {EstimatorCdlSummaryRows.Count} CDL classes populated.";
            }
            catch (Exception ex)
            {
                EstimatorSpatialStatus = $"Spatial import failed: {ex.Message}";
            }
        }

        private static bool CellInPolygon(RasterData raster, int x, int y, IReadOnlyList<Point> polygon)
        {
            var cx = raster.OriginX + ((x + 0.5) * raster.PixelWidth);
            var cy = raster.OriginY + ((y + 0.5) * raster.PixelHeight);
            return IsPointInPolygon(new Point(cx, cy), polygon);
        }

        private static bool IsPointInPolygon(Point point, IReadOnlyList<Point> polygon)
        {
            if (polygon.Count < 3)
            {
                return false;
            }

            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                bool intersects = ((pi.Y > point.Y) != (pj.Y > point.Y))
                    && (point.X < ((pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) + 1e-12)) + pi.X);
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static RasterData ReprojectRasterIfNeeded(RasterData source, RasterData target)
        {
            if (string.IsNullOrWhiteSpace(source.ProjectionWkt) || string.IsNullOrWhiteSpace(target.ProjectionWkt))
            {
                return source;
            }

            if (string.Equals(source.ProjectionWkt, target.ProjectionWkt, StringComparison.Ordinal))
            {
                return source;
            }

            ProjectionInfo sourceProjection;
            ProjectionInfo targetProjection;
            try
            {
                sourceProjection = ProjectionInfo.FromEsriString(source.ProjectionWkt);
                targetProjection = ProjectionInfo.FromEsriString(target.ProjectionWkt);
            }
            catch
            {
                return source;
            }

            var reprojected = new double[target.Width * target.Height];
            for (int y = 0; y < target.Height; y++)
            {
                for (int x = 0; x < target.Width; x++)
                {
                    int idx = (y * target.Width) + x;
                    var point = new[]
                    {
                        target.OriginX + ((x + 0.5) * target.PixelWidth),
                        target.OriginY + ((y + 0.5) * target.PixelHeight)
                    };
                    var z = new[] { 0d };
                    Reproject.ReprojectPoints(point, z, targetProjection, sourceProjection, 0, 1);

                    int sx = (int)Math.Floor((point[0] - source.OriginX) / source.PixelWidth);
                    int sy = (int)Math.Floor((point[1] - source.OriginY) / source.PixelHeight);
                    sx = Math.Clamp(sx, 0, source.Width - 1);
                    sy = Math.Clamp(sy, 0, source.Height - 1);
                    reprojected[idx] = source.Values[(sy * source.Width) + sx];
                }
            }

            return new RasterData(
                target.Width,
                target.Height,
                reprojected,
                target.PixelWidth,
                target.PixelHeight,
                target.OriginX,
                target.OriginY,
                target.ProjectionName,
                target.ProjectionWkt,
                true,
                source.ProjectionName);
        }

        private static PointCollection BuildPolygonPreview(IReadOnlyList<Point> polygonPoints, Rect worldBounds, Rect previewFrame)
        {
            var preview = new PointCollection();
            if (polygonPoints.Count == 0)
            {
                return preview;
            }

            foreach (var point in polygonPoints)
            {
                preview.Add(ProjectPoint(point, worldBounds, previewFrame));
            }

            if (preview.Count > 0 && preview[0] != preview[^1])
            {
                preview.Add(preview[0]);
            }

            return preview;
        }

        private static PolygonShapeData ReadShapefilePolygon(string shpPath, RasterData targetRaster)
        {
            using var stream = File.OpenRead(shpPath);
            using var reader = new BinaryReader(stream);
            stream.Seek(36, SeekOrigin.Begin);
            double minX = reader.ReadDouble();
            double minY = reader.ReadDouble();
            double maxX = reader.ReadDouble();
            double maxY = reader.ReadDouble();
            var bounds = new Rect(new Point(minX, minY), new Point(maxX, maxY));

            stream.Seek(100, SeekOrigin.Begin);
            var vertices = new List<Point>();
            while (stream.Position + 12 <= stream.Length)
            {
                _ = ReadInt32BigEndian(reader);
                int contentLengthWords = ReadInt32BigEndian(reader);
                long contentBytes = contentLengthWords * 2L;
                long contentStart = stream.Position;
                if (contentBytes < 4 || contentStart + contentBytes > stream.Length)
                {
                    break;
                }

                int shapeType = reader.ReadInt32();
                if (shapeType is 5 or 15 or 25)
                {
                    _ = reader.ReadDouble();
                    _ = reader.ReadDouble();
                    _ = reader.ReadDouble();
                    _ = reader.ReadDouble();
                    int partCount = reader.ReadInt32();
                    int pointCount = reader.ReadInt32();
                    var parts = new int[partCount];
                    for (int i = 0; i < partCount; i++)
                    {
                        parts[i] = reader.ReadInt32();
                    }

                    var points = new Point[pointCount];
                    for (int i = 0; i < pointCount; i++)
                    {
                        points[i] = new Point(reader.ReadDouble(), reader.ReadDouble());
                    }

                    if (partCount > 0)
                    {
                        int start = parts[0];
                        int end = (partCount > 1 ? parts[1] : pointCount) - 1;
                        for (int i = start; i <= end && i < points.Length; i++)
                        {
                            vertices.Add(points[i]);
                        }
                    }

                    break;
                }

                stream.Seek(contentStart + contentBytes, SeekOrigin.Begin);
            }

            var polygonProjectionWkt = ReadShapefileProjectionWkt(shpPath);
            if (!string.IsNullOrWhiteSpace(polygonProjectionWkt)
                && !string.IsNullOrWhiteSpace(targetRaster.ProjectionWkt)
                && !string.Equals(polygonProjectionWkt, targetRaster.ProjectionWkt, StringComparison.Ordinal)
                && vertices.Count > 0)
            {
                try
                {
                    var sourceProjection = ProjectionInfo.FromEsriString(polygonProjectionWkt);
                    var targetProjection = ProjectionInfo.FromEsriString(targetRaster.ProjectionWkt);
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        var xy = new[] { vertices[i].X, vertices[i].Y };
                        var z = new[] { 0d };
                        Reproject.ReprojectPoints(xy, z, sourceProjection, targetProjection, 0, 1);
                        vertices[i] = new Point(xy[0], xy[1]);
                    }

                    var minX2 = vertices.Min(v => v.X);
                    var minY2 = vertices.Min(v => v.Y);
                    var maxX2 = vertices.Max(v => v.X);
                    var maxY2 = vertices.Max(v => v.Y);
                    bounds = new Rect(new Point(minX2, minY2), new Point(maxX2, maxY2));
                }
                catch
                {
                    // Keep original coordinates when projection transform fails.
                }
            }

            return new PolygonShapeData(bounds, vertices);
        }

        private static string ReadShapefileProjectionWkt(string shpPath)
        {
            var prjPath = Path.ChangeExtension(shpPath, ".prj");
            if (!File.Exists(prjPath))
            {
                return string.Empty;
            }

            return File.ReadAllText(prjPath).Trim();
        }

        private static int ReadInt32BigEndian(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(sizeof(int));
            if (bytes.Length < sizeof(int))
            {
                throw new EndOfStreamException();
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToInt32(bytes, 0);
        }

        private static Rect? GetRasterActiveBounds(RasterData raster)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int y = 0; y < raster.Height; y++)
            {
                for (int x = 0; x < raster.Width; x++)
                {
                    int idx = (y * raster.Width) + x;
                    if (raster.Values[idx] <= 0)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (minX == int.MaxValue)
            {
                return null;
            }

            var left = raster.OriginX + (minX * raster.PixelWidth);
            var right = raster.OriginX + ((maxX + 1) * raster.PixelWidth);
            var top = raster.OriginY + (minY * raster.PixelHeight);
            var bottom = raster.OriginY + ((maxY + 1) * raster.PixelHeight);
            return new Rect(new Point(Math.Min(left, right), Math.Min(top, bottom)), new Point(Math.Max(left, right), Math.Max(top, bottom)));
        }

        private static Rect GetRasterBounds(RasterData raster)
        {
            double x1 = raster.OriginX;
            double x2 = raster.OriginX + (raster.Width * raster.PixelWidth);
            double y1 = raster.OriginY;
            double y2 = raster.OriginY + (raster.Height * raster.PixelHeight);
            return new Rect(new Point(Math.Min(x1, x2), Math.Min(y1, y2)), new Point(Math.Max(x1, x2), Math.Max(y1, y2)));
        }

        private static Rect ProjectWorldRect(Rect source, Rect worldBounds, Rect frame)
        {
            var topLeft = ProjectPoint(new Point(source.Left, source.Top), worldBounds, frame);
            var bottomRight = ProjectPoint(new Point(source.Right, source.Bottom), worldBounds, frame);
            return new Rect(topLeft, bottomRight);
        }

        private static Point ProjectPoint(Point point, Rect worldBounds, Rect frame)
        {
            double worldWidth = Math.Max(worldBounds.Width, 1e-6);
            double worldHeight = Math.Max(worldBounds.Height, 1e-6);
            double x = frame.Left + ((point.X - worldBounds.Left) / worldWidth) * frame.Width;
            double y = frame.Top + (1.0 - ((point.Y - worldBounds.Top) / worldHeight)) * frame.Height;
            return new Point(x, y);
        }

        private static RasterData ReadSingleBandRaster(string path)
        {
            using var tiff = Tiff.Open(path, "r") ?? throw new InvalidOperationException("Unable to open raster.");
            int width = tiff.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt() ?? 0;
            int height = tiff.GetField(TiffTag.IMAGELENGTH)?[0].ToInt() ?? 0;
            int bits = tiff.GetField(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 8;
            int samples = tiff.GetField(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 1;
            if (width <= 0 || height <= 0 || samples != 1) throw new InvalidOperationException("Raster must be single-band with valid dimensions.");

            double pixelWidth = 1;
            double pixelHeight = -1;
            double originX = 0;
            double originY = 0;
            var scale = tiff.GetField((TiffTag)33550);
            if (scale != null)
            {
                var vals = scale[1].ToDoubleArray();
                if (vals != null && vals.Length >= 2)
                {
                    pixelWidth = vals[0];
                    pixelHeight = -Math.Abs(vals[1]);
                }
            }
            var tie = tiff.GetField((TiffTag)33922);
            if (tie != null)
            {
                var vals = tie[1].ToDoubleArray();
                if (vals != null && vals.Length >= 6)
                {
                    originX = vals[3];
                    originY = vals[4];
                }
            }

            var values = new double[width*height];
            if (bits == 32)
            {
                var scan = new float[width];
                var buf = new byte[width * sizeof(float)];
                for (int row=0; row<height; row++)
                {
                    tiff.ReadScanline(buf, row);
                    Buffer.BlockCopy(buf,0,scan,0,buf.Length);
                    for (int col=0; col<width; col++) values[row*width+col]=scan[col];
                }
            }
            else
            {
                var buf = new byte[tiff.ScanlineSize()];
                for (int row=0; row<height; row++)
                {
                    tiff.ReadScanline(buf,row);
                    for (int col=0; col<width; col++) values[row*width+col]=buf[col];
                }
            }

            string projectionWkt = ReadGeoTiffProjectionWkt(tiff);
            string projectionName = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(projectionWkt))
            {
                try
                {
                    var info = ProjectionInfo.FromEsriString(projectionWkt);
                    if (info != null && !string.IsNullOrWhiteSpace(info.Name))
                    {
                        projectionName = info.Name;
                    }
                }
                catch
                {
                    // If projection parsing fails, keep file name as display name.
                }
            }

            return new RasterData(width, height, values, pixelWidth, pixelHeight, originX, originY, projectionName, projectionWkt, false, projectionName);
        }

        private static string ReadGeoTiffProjectionWkt(Tiff tiff)
        {
            var ascii = tiff.GetField((TiffTag)34737);
            if (ascii != null && ascii.Length > 0)
            {
                var value = ascii[0].ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim('\0', ' ');
                }
            }

            return string.Empty;
        }

        private void SeedEstimatorInputs()
        {
            EstimatorEvents.Clear();
            EstimatorEvents.Add(new EstimatorEventRow("10-year", 1.2, 5, "0.1", 10));
            EstimatorEvents.Add(new EstimatorEventRow("50-year", 2.4, 6, "0.02", 50));
            EstimatorEvents.Add(new EstimatorEventRow("100-year", 3.2, 6, "0.01", 100));

            EstimatorCropRows.Clear();
            EstimatorCropRows.Add(new EstimatorCropRow(1, "Corn", "10-year", 350, 0, "4,5,6,7,8,9", ""));
            EstimatorCropRows.Add(new EstimatorCropRow(5, "Soybeans", "50-year", 220, 0, "5,6,7,8,9", "0:0,1:0.45,2:0.85,3:1"));
            EstimatorCropRows.Add(new EstimatorCropRow(24, "Winter Wheat", "100-year", 120, 0, "10,11,12,1,2,3", ""));
        }

        private void ComputeFloodDamageEstimator()
        {
            EstimatorResults.Clear();
            EstimatorSummaryRows.Clear();

            if (EstimatorEvents.Count == 0 || EstimatorCropRows.Count == 0)
            {
                EstimatorSummary = "Add at least one event and one crop row before running the estimator.";
                return;
            }

            try
            {
                _appProgressService.Start("Running flood impact analysis...", 10);

                var spatialDepthLookup = EstimatorSpatialCropRows
                    .GroupBy(row => row.CropCode)
                    .ToDictionary(group => group.Key, group => group.First().AverageDepthFeet);

                var request = new FloodImpactAnalysisRequest(
                    EstimatorEvents.Select(evt => new FloodEventInput(evt.Name, evt.DepthFeet, evt.FloodMonth, evt.AnnualExceedanceProbabilitiesCsv, evt.ReturnPeriodYears)).ToList(),
                    EstimatorCropRows.Select(crop => new CropImpactInput(
                        crop.CropCode,
                        crop.CropName,
                        crop.EventName,
                        crop.Acres,
                        crop.ValuePerAcre,
                        crop.GrowingMonthsCsv,
                        crop.SpecificCurve,
                        spatialDepthLookup.TryGetValue(crop.CropCode, out var avgDepth) ? avgDepth : 0.0)).ToList(),
                    new FloodImpactUncertaintySettings(
                        EstimatorDefaultCurve,
                        EstimatorDefaultCropValue,
                        EstimatorDamageStdDev,
                        EstimatorDepthStdDev,
                        EstimatorValueStdDev,
                        EstimatorMonteCarloRuns,
                        EstimatorAnalysisYears,
                        EstimatorRandomSeed,
                        EstimatorRandomizeMonth));

                _appProgressService.Report("Simulating event damages...", 55);
                var analysisResult = _floodImpactAnalysisService.Run(request);
                foreach (var row in analysisResult.Events)
                {
                    EstimatorResults.Add(new EstimatorResultRow(row.EventName, row.MeanDamage, row.StdDamage, row.P5Damage, row.P95Damage, row.DiscreteEadContribution));
                }

                EstimatorSummaryRows.Add(new EstimatorSummaryRow("Events Processed", analysisResult.Summary.EventCount.ToString(CultureInfo.InvariantCulture), "Events with valid return periods included in analysis."));
                EstimatorSummaryRows.Add(new EstimatorSummaryRow("Crop Rows", analysisResult.Summary.CropCount.ToString(CultureInfo.InvariantCulture), "Crop rows evaluated across all event simulations."));
                EstimatorSummaryRows.Add(new EstimatorSummaryRow("Samples", analysisResult.Summary.Samples.ToString("N0", CultureInfo.InvariantCulture), "Total Monte Carlo draws = events × years × runs."));
                EstimatorSummaryRows.Add(new EstimatorSummaryRow("Mean Damage", analysisResult.Summary.TotalMeanDamage.ToString("C0", CultureInfo.CurrentCulture), "Sum of mean damages over all events."));
                EstimatorSummaryRows.Add(new EstimatorSummaryRow("Discrete EAD", analysisResult.Summary.TotalDiscreteEad.ToString("C0", CultureInfo.CurrentCulture), "Σ(event mean damage ÷ return period)."));
                EstimatorSummaryRows.Add(new EstimatorSummaryRow("Mean COV", analysisResult.Summary.MeanCoefficientOfVariation.ToString("0.###", CultureInfo.InvariantCulture), "Average coefficient of variation (std/mean) across processed events."));

                EstimatorSummary = $"Estimator complete. {analysisResult.Summary.EventCount} events processed. Discrete EAD Σ(Damage/RP) = {analysisResult.Summary.TotalDiscreteEad:C0}.";
                _appProgressService.Complete("Flood impact analysis complete.");
            }
            catch (Exception ex)
            {
                EstimatorSummary = $"Estimator failed: {ex.Message}";
                _appProgressService.Fail("Flood impact analysis failed.");
            }
        }

        private static List<(double Depth, double Damage)> ParseDepthDamageCurve(string curveText)
        {
            if (string.IsNullOrWhiteSpace(curveText))
            {
                throw new InvalidOperationException("Default curve cannot be blank.");
            }

            var points = new List<(double Depth, double Damage)>();
            foreach (var token in curveText.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException("Curve format must be depth:damage pairs separated by commas.");
                }

                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var depth)
                    || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var damage))
                {
                    throw new InvalidOperationException("Curve values must be numeric.");
                }

                points.Add((Math.Max(0.0, depth), Math.Clamp(damage, 0.0, 1.0)));
            }

            if (points.Count < 2)
            {
                throw new InvalidOperationException("Curve must have at least two points.");
            }

            points.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            return points;
        }

        private static HashSet<int> ParseMonths(string monthsCsv)
        {
            var months = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(monthsCsv))
            {
                return months;
            }

            foreach (var token in monthsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token.Trim(), out var month) && month >= 1 && month <= 12)
                {
                    months.Add(month);
                }
            }

            return months;
        }

        private static double InterpolateDamage(double depth, List<(double Depth, double Damage)> curve)
        {
            if (depth <= curve[0].Depth)
            {
                return curve[0].Damage;
            }

            for (int i = 1; i < curve.Count; i++)
            {
                if (depth <= curve[i].Depth)
                {
                    var p0 = curve[i - 1];
                    var p1 = curve[i];
                    if (Math.Abs(p1.Depth - p0.Depth) < 1e-9)
                    {
                        return p1.Damage;
                    }

                    var t = (depth - p0.Depth) / (p1.Depth - p0.Depth);
                    return p0.Damage + ((p1.Damage - p0.Damage) * t);
                }
            }

            return curve[^1].Damage;
        }

        private static double NextGaussian(Random random, double mean, double stdDev)
        {
            if (stdDev <= 0)
            {
                return 0;
            }

            var u1 = 1.0 - random.NextDouble();
            var u2 = 1.0 - random.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + (stdDev * randStdNormal);
        }

        private static double PercentileFromSorted(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0;
            }

            percentile = Math.Clamp(percentile, 0.0, 1.0);
            var index = percentile * (sortedValues.Count - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);
            if (lower == upper)
            {
                return sortedValues[lower];
            }

            var fraction = index - lower;
            return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction);
        }

        public override object CaptureState()
        {
            return new AgricultureDepthDamageData
            {
                SelectedRegionName = SelectedRegion?.Name,
                SelectedCropName = SelectedCrop?.Name,
                AverageResponse = AverageResponse,
                SimulationYears = SimulationYears,
                Regions = Regions.Select(region => new AgricultureRegionData
                {
                    Name = region.Name,
                    Description = region.Description,
                    ImpactModifier = region.ImpactModifier,
                    FloodWindowStartDay = region.FloodWindowStartDay,
                    FloodWindowEndDay = region.FloodWindowEndDay,
                    AnnualExceedanceProbability = region.AnnualExceedanceProbability,
                    FloodSeasonPeakDay = region.FloodSeasonPeakDay,
                    SeasonShiftDays = region.SeasonShiftDays,
                    IsCustom = region.IsCustom,
                    DepthDuration = region.DepthDuration.Select(point => new AgricultureDepthDurationPointData
                    {
                        DepthFeet = point.DepthFeet,
                        DurationDays = point.DurationDays,
                        BaseDamage = point.BaseDamage
                    }).ToList()
                }).ToList(),
                Crops = Crops.Select(crop => new AgricultureCropData
                {
                    Name = crop.Name,
                    Description = crop.Description,
                    DamageFactor = crop.DamageFactor,
                    ImpactModifier = crop.ImpactModifier,
                    IsCustom = crop.IsCustom
                }).ToList(),
                StageExposures = StageExposures.Select(stage => new AgricultureStageExposureData
                {
                    StageName = stage.StageName,
                    ExposureDays = stage.ExposureDays,
                    FloodToleranceDays = stage.FloodToleranceDays
                }).ToList(),
                CropScapeTotalAcreage = CropScapeTotalAcreage,
                CropScapeImportStatus = CropScapeImportStatus,
                CropScapeSummaries = CropScapeSummaries.Select(summary => new CropScapeSummaryData
                {
                    Code = summary.Code,
                    Name = summary.Name,
                    PixelCount = summary.PixelCount,
                    Acres = summary.Acres,
                    PercentOfTotal = summary.PercentOfTotal
                }).ToList(),
                EstimatorDefaultCurve = EstimatorDefaultCurve,
                EstimatorDefaultCropValue = EstimatorDefaultCropValue,
                EstimatorDamageStdDev = EstimatorDamageStdDev,
                EstimatorDepthStdDev = EstimatorDepthStdDev,
                EstimatorValueStdDev = EstimatorValueStdDev,
                EstimatorMonteCarloRuns = EstimatorMonteCarloRuns,
                EstimatorAnalysisYears = EstimatorAnalysisYears,
                EstimatorRandomSeed = EstimatorRandomSeed,
                EstimatorRandomizeMonth = EstimatorRandomizeMonth,
                EstimatorEvents = EstimatorEvents.Select(evt => new EstimatorEventData
                {
                    Name = evt.Name,
                    DepthFeet = evt.DepthFeet,
                    FloodMonth = evt.FloodMonth,
                    AnnualExceedanceProbabilitiesCsv = evt.AnnualExceedanceProbabilitiesCsv,
                    ReturnPeriodYears = evt.ReturnPeriodYears
                }).ToList(),
                EstimatorCropRows = EstimatorCropRows.Select(row => new EstimatorCropData
                {
                    CropCode = row.CropCode,
                    CropName = row.CropName,
                    EventName = row.EventName,
                    Acres = row.Acres,
                    ValuePerAcre = row.ValuePerAcre,
                    GrowingMonthsCsv = row.GrowingMonthsCsv,
                    SpecificCurve = row.SpecificCurve
                }).ToList(),
                EstimatorCdlRasterPath = EstimatorCdlRasterPath,
                EstimatorDepthRasterPath = EstimatorDepthRasterPath,
                EstimatorPolygonShapefilePath = EstimatorPolygonShapefilePath,
                EstimatorUniformPolygonDepth = EstimatorUniformPolygonDepth,
                EstimatorUsePolygonUniformDepth = EstimatorUsePolygonUniformDepth,
                EstimatorSpatialCropRows = EstimatorSpatialCropRows.Select(row => new EstimatorSpatialCropData
                {
                    CropCode = row.CropCode,
                    CropName = row.CropName,
                    Acres = row.Acres,
                    AverageDepthFeet = row.AverageDepthFeet
                }).ToList()
            };
        }

        public override void RestoreState(object state)
        {
            if (state is not AgricultureDepthDamageData data)
            {
                return;
            }

            _isInitializing = true;
            try
            {
                DetachRegionHandlers(SelectedRegion);
                DetachCropHandlers(SelectedCrop);

                foreach (var region in Regions)
                {
                    DetachRegionHandlers(region);
                }

                foreach (var crop in Crops)
                {
                    DetachCropHandlers(crop);
                }

                foreach (var stage in StageExposures)
                {
                    stage.PropertyChanged -= Stage_PropertyChanged;
                }

                Regions.Clear();
                foreach (var region in data.Regions)
                {
                    var depthDuration = region.DepthDuration.Select(point =>
                        new DepthDurationPoint(point.DepthFeet, point.DurationDays, point.BaseDamage));

                    Regions.Add(new RegionDefinition(
                        region.Name,
                        region.Description,
                        region.ImpactModifier,
                        region.FloodWindowStartDay,
                        region.FloodWindowEndDay,
                        region.AnnualExceedanceProbability,
                        region.FloodSeasonPeakDay,
                        region.SeasonShiftDays,
                        depthDuration,
                        region.IsCustom));
                }

                Crops.Clear();
                foreach (var crop in data.Crops)
                {
                    Crops.Add(new CropDefinition(
                        crop.Name,
                        crop.Description,
                        crop.DamageFactor,
                        crop.ImpactModifier,
                        crop.IsCustom));
                }

                StageExposures.Clear();
                foreach (var stage in StageExposure.CreateDefaults())
                {
                    StageExposures.Add(stage);
                }

                foreach (var stage in StageExposures)
                {
                    stage.PropertyChanged += Stage_PropertyChanged;
                }

                foreach (var stageData in data.StageExposures)
                {
                    var match = StageExposures.FirstOrDefault(stage =>
                        stage.StageName.Equals(stageData.StageName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.ExposureDays = stageData.ExposureDays;
                        match.FloodToleranceDays = stageData.FloodToleranceDays;
                    }
                }

                AverageResponse = data.AverageResponse;
                SimulationYears = data.SimulationYears;

                SelectedRegion = Regions.FirstOrDefault(r => string.Equals(r.Name, data.SelectedRegionName, StringComparison.OrdinalIgnoreCase))
                                 ?? Regions.FirstOrDefault();
                SelectedCrop = Crops.FirstOrDefault(c => string.Equals(c.Name, data.SelectedCropName, StringComparison.OrdinalIgnoreCase))
                               ?? Crops.FirstOrDefault();

                _appProgressService.Report("Calculating crop class acreage summaries...", 65);
                CropScapeSummaries.Clear();
                foreach (var summary in data.CropScapeSummaries)
                {
                    CropScapeSummaries.Add(new CropScapeAcreageSummary(
                        summary.Code,
                        summary.Name,
                        summary.PixelCount,
                        summary.Acres,
                        summary.PercentOfTotal));
                }

                CropScapeTotalAcreage = data.CropScapeTotalAcreage;
                CropScapeImportStatus = string.IsNullOrWhiteSpace(data.CropScapeImportStatus)
                    ? "No CropScape raster imported."
                    : data.CropScapeImportStatus;

                EstimatorDefaultCurve = string.IsNullOrWhiteSpace(data.EstimatorDefaultCurve) ? EstimatorDefaultCurve : data.EstimatorDefaultCurve;
                EstimatorDefaultCropValue = data.EstimatorDefaultCropValue > 0 ? data.EstimatorDefaultCropValue : EstimatorDefaultCropValue;
                EstimatorDamageStdDev = data.EstimatorDamageStdDev;
                EstimatorDepthStdDev = data.EstimatorDepthStdDev;
                EstimatorValueStdDev = data.EstimatorValueStdDev;
                EstimatorMonteCarloRuns = data.EstimatorMonteCarloRuns > 0 ? data.EstimatorMonteCarloRuns : EstimatorMonteCarloRuns;
                EstimatorAnalysisYears = data.EstimatorAnalysisYears > 0 ? data.EstimatorAnalysisYears : EstimatorAnalysisYears;
                EstimatorRandomSeed = data.EstimatorRandomSeed;
                EstimatorRandomizeMonth = data.EstimatorRandomizeMonth;
                EstimatorCdlRasterPath = data.EstimatorCdlRasterPath ?? string.Empty;
                EstimatorDepthRasterPath = data.EstimatorDepthRasterPath ?? string.Empty;
                EstimatorPolygonShapefilePath = data.EstimatorPolygonShapefilePath ?? string.Empty;
                EstimatorUniformPolygonDepth = data.EstimatorUniformPolygonDepth;
                EstimatorUsePolygonUniformDepth = data.EstimatorUsePolygonUniformDepth;

                if (data.EstimatorEvents.Count > 0)
                {
                    EstimatorEvents.Clear();
                    foreach (var evt in data.EstimatorEvents)
                    {
                        EstimatorEvents.Add(new EstimatorEventRow(evt.Name, evt.DepthFeet, evt.FloodMonth, evt.AnnualExceedanceProbabilitiesCsv, evt.ReturnPeriodYears));
                    }
                }

                if (data.EstimatorCropRows.Count > 0)
                {
                    EstimatorCropRows.Clear();
                    foreach (var row in data.EstimatorCropRows)
                    {
                        EstimatorCropRows.Add(new EstimatorCropRow(row.CropCode, row.CropName, row.EventName, row.Acres, row.ValuePerAcre, row.GrowingMonthsCsv, row.SpecificCurve));
                    }
                }

                EstimatorSpatialCropRows.Clear();
                foreach (var row in data.EstimatorSpatialCropRows)
                {
                    EstimatorSpatialCropRows.Add(new EstimatorSpatialCropRow(row.CropCode, row.CropName, row.Acres, row.AverageDepthFeet));
                }
                EstimatorSpatialCropAcreage = EstimatorSpatialCropRows.Sum(row => row.Acres);
            }
            finally
            {
                _isInitializing = false;
            }

            Compute();
            RefreshDiagnostics();
        }

        protected override IEnumerable<DiagnosticItem> BuildDiagnostics()
        {
            var diagnostics = new List<DiagnosticItem>();

            if (SelectedRegion == null)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Error,
                    "Region not selected",
                    "Choose a region to evaluate depth-duration impacts."));
            }

            if (SelectedCrop == null)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Error,
                    "Crop not selected",
                    "Choose a crop to evaluate damage sensitivity."));
            }

            if (SelectedRegion != null && SelectedRegion.DepthDuration.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Warning,
                    "No depth-duration points",
                    "Add depth-duration points for the selected region."));
            }

            if (StageExposures.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Warning,
                    "No stage exposures",
                    "Stage exposure definitions are missing. Add stages to compute seasonal impacts."));
            }

            if (CropScapeSummaries.Count == 0 && CropScapeTotalAcreage <= 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Info,
                    "CropScape data not loaded",
                    "Import a CropScape raster if you want acreage-weighted damage estimates."));
            }

            if (diagnostics.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Info,
                    "Agriculture inputs look good",
                    "Region, crop, and exposure inputs are ready for calculation."));
            }

            return diagnostics;
        }

        public class RegionDefinition : BaseViewModel
        {
            private string _name;
            private string _description;
            private double _impactModifier;
            private int _floodWindowStartDay;
            private int _floodWindowEndDay;
            private double _annualExceedanceProbability;
            private int _floodSeasonPeakDay;
            private int _seasonShiftDays;
            private string _annualExceedanceProbabilityDisplayText;

            public RegionDefinition(
                string name,
                string description,
                double impactModifier,
                int floodWindowStartDay,
                int floodWindowEndDay,
                double annualExceedanceProbability,
                int floodSeasonPeakDay,
                int seasonShiftDays,
                IEnumerable<DepthDurationPoint> depthDuration,
                bool isCustom = false)
            {
                _name = name;
                _description = description;
                _impactModifier = impactModifier;
                _floodWindowStartDay = Math.Clamp(floodWindowStartDay, 1, DaysInYear);
                _floodWindowEndDay = Math.Clamp(floodWindowEndDay, _floodWindowStartDay, DaysInYear);
                _annualExceedanceProbability = Math.Clamp(annualExceedanceProbability, 0.0, 1.0);
                _floodSeasonPeakDay = Math.Clamp(floodSeasonPeakDay, _floodWindowStartDay, _floodWindowEndDay);
                _seasonShiftDays = Math.Clamp(seasonShiftDays, -180, 180);
                IsCustom = isCustom;
                DepthDuration = new ObservableCollection<DepthDurationPoint>(depthDuration.Select(p => p.Clone()));
                DepthDuration.CollectionChanged += DepthDuration_CollectionChanged;
                foreach (var point in DepthDuration)
                {
                    point.PropertyChanged += DepthDurationPoint_PropertyChanged;
                }
                _annualExceedanceProbabilityDisplayText = _annualExceedanceProbability.ToString("0.###", CultureInfo.CurrentCulture);
            }

            public string Name
            {
                get => _name;
                set
                {
                    string adjusted = value?.Trim() ?? string.Empty;
                    if (_name == adjusted)
                    {
                        return;
                    }

                    _name = adjusted;
                    OnPropertyChanged();
                }
            }

            public string Description
            {
                get => _description;
                set
                {
                    string adjusted = value ?? string.Empty;
                    if (_description == adjusted)
                    {
                        return;
                    }

                    _description = adjusted;
                    OnPropertyChanged();
                }
            }

            public double ImpactModifier
            {
                get => _impactModifier;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Clamp(value, 0.1, 5.0) : 1.0;
                    if (Math.Abs(_impactModifier - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _impactModifier = adjusted;
                    OnPropertyChanged();
                }
            }

            public double AnnualExceedanceProbability
            {
                get => _annualExceedanceProbability;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : _annualExceedanceProbability;
                    if (Math.Abs(_annualExceedanceProbability - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _annualExceedanceProbability = adjusted;
                    _annualExceedanceProbabilityDisplayText = _annualExceedanceProbability.ToString("0.###", CultureInfo.CurrentCulture);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AnnualExceedanceProbabilityDisplay));
                }
            }

            public string AnnualExceedanceProbabilityDisplay
            {
                get => _annualExceedanceProbabilityDisplayText;
                set
                {
                    string newText = value ?? string.Empty;
                    if (_annualExceedanceProbabilityDisplayText == newText)
                    {
                        return;
                    }

                    _annualExceedanceProbabilityDisplayText = newText;
                    OnPropertyChanged();

                    string trimmed = newText.Trim();
                    if (trimmed.Length == 0)
                    {
                        return;
                    }

                    var numberFormat = CultureInfo.CurrentCulture.NumberFormat;
                    if (trimmed.EndsWith(numberFormat.NumberDecimalSeparator, StringComparison.Ordinal) ||
                        trimmed == numberFormat.NegativeSign ||
                        trimmed == numberFormat.PositiveSign)
                    {
                        return;
                    }

                    if (double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out double parsed))
                    {
                        AnnualExceedanceProbability = parsed;
                    }
                }
            }

            public ObservableCollection<DepthDurationPoint> DepthDuration { get; }

            public bool IsCustom { get; }

            public double MaxDuration => DepthDuration.Count == 0 ? 1.0 : DepthDuration.Max(p => p.DurationDays);

            public int FloodWindowStartDay
            {
                get => _floodWindowStartDay;
                set
                {
                    int adjusted = Math.Clamp(value, 1, DaysInYear);
                    if (_floodWindowStartDay == adjusted)
                    {
                        return;
                    }

                    _floodWindowStartDay = adjusted;
                    if (_floodWindowEndDay < _floodWindowStartDay)
                    {
                        _floodWindowEndDay = _floodWindowStartDay;
                        OnPropertyChanged(nameof(FloodWindowEndDay));
                    }

                    if (_floodSeasonPeakDay < _floodWindowStartDay)
                    {
                        _floodSeasonPeakDay = _floodWindowStartDay;
                        OnPropertyChanged(nameof(FloodSeasonPeakDay));
                    }

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FloodWindowRangeDisplay));
                }
            }

            public int FloodWindowEndDay
            {
                get => _floodWindowEndDay;
                set
                {
                    int adjusted = Math.Clamp(value, 1, DaysInYear);
                    if (adjusted < _floodWindowStartDay)
                    {
                        adjusted = _floodWindowStartDay;
                    }

                    if (_floodWindowEndDay == adjusted)
                    {
                        return;
                    }

                    _floodWindowEndDay = adjusted;
                    if (_floodSeasonPeakDay > _floodWindowEndDay)
                    {
                        _floodSeasonPeakDay = _floodWindowEndDay;
                        OnPropertyChanged(nameof(FloodSeasonPeakDay));
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FloodWindowRangeDisplay));
                }
            }

            public int FloodSeasonPeakDay
            {
                get => _floodSeasonPeakDay;
                set
                {
                    int adjusted = Math.Clamp(value, _floodWindowStartDay, _floodWindowEndDay);
                    if (_floodSeasonPeakDay == adjusted)
                    {
                        return;
                    }

                    _floodSeasonPeakDay = adjusted;
                    OnPropertyChanged();
                }
            }

            public int SeasonShiftDays
            {
                get => _seasonShiftDays;
                set
                {
                    int adjusted = Math.Clamp(value, -180, 180);
                    if (_seasonShiftDays == adjusted)
                    {
                        return;
                    }

                    _seasonShiftDays = adjusted;
                    OnPropertyChanged();
                }
            }

            public string FloodWindowRangeDisplay =>
                $"Flood season: days {FloodWindowStartDay} – {FloodWindowEndDay} (peak ≈ day {FloodSeasonPeakDay}, shift {SeasonShiftDays:+#;-#;0} days).";

            private void DepthDuration_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.OldItems != null)
                {
                    foreach (DepthDurationPoint point in e.OldItems)
                    {
                        point.PropertyChanged -= DepthDurationPoint_PropertyChanged;
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (DepthDurationPoint point in e.NewItems)
                    {
                        point.PropertyChanged += DepthDurationPoint_PropertyChanged;
                    }
                }

                OnPropertyChanged(nameof(MaxDuration));
            }

            private void DepthDurationPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(DepthDurationPoint.DurationDays))
                {
                    OnPropertyChanged(nameof(MaxDuration));
                }
            }

            public static IEnumerable<RegionDefinition> CreateDefaults()
            {
                var regions = new List<RegionDefinition>
                {
                    new RegionDefinition(
                        "North Atlantic Division",
                        "Coastal and riverine floodplains from Virginia to Maine with extensive drainage infrastructure and shorter ponding durations.",
                        0.82,
                        75,
                        190,
                        0.14,
                        132,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(1.0, 2, 0.16),
                            new DepthDurationPoint(1.8, 4, 0.29),
                            new DepthDurationPoint(2.6, 6, 0.47),
                            new DepthDurationPoint(3.4, 9, 0.63),
                            new DepthDurationPoint(4.2, 12, 0.78)
                        }),
                    new RegionDefinition(
                        "South Atlantic Division",
                        "Southeastern alluvial plains where tropical rainfall and longer drainage times elevate losses.",
                        0.96,
                        90,
                        260,
                        0.18,
                        175,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(1.2, 3, 0.2),
                            new DepthDurationPoint(2.1, 5, 0.36),
                            new DepthDurationPoint(3.0, 7, 0.55),
                            new DepthDurationPoint(4.0, 10, 0.72),
                            new DepthDurationPoint(5.0, 14, 0.86)
                        }),
                    new RegionDefinition(
                        "Great Lakes & Ohio River Division",
                        "Interior basin cropland subject to protracted spring floods along the Ohio and Tennessee systems.",
                        0.88,
                        60,
                        200,
                        0.12,
                        130,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(1.0, 3, 0.18),
                            new DepthDurationPoint(1.8, 5, 0.32),
                            new DepthDurationPoint(2.6, 7, 0.5),
                            new DepthDurationPoint(3.5, 10, 0.68),
                            new DepthDurationPoint(4.3, 13, 0.8)
                        }),
                    new RegionDefinition(
                        "Mississippi Valley Division",
                        "Lower Mississippi and tributary bottoms with high exposure to deep, slow-draining floods.",
                        1.1,
                        70,
                        230,
                        0.2,
                        150,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(1.5, 4, 0.26),
                            new DepthDurationPoint(2.6, 6, 0.45),
                            new DepthDurationPoint(3.5, 9, 0.67),
                            new DepthDurationPoint(4.6, 12, 0.82),
                            new DepthDurationPoint(5.8, 16, 0.92)
                        }),
                    new RegionDefinition(
                        "Lower Mississippi Alluvial Valley",
                        "Backwater rice and row-crop systems with the longest flood residence times in the system.",
                        1.08,
                        80,
                        250,
                        0.22,
                        165,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(1.6, 4, 0.3),
                            new DepthDurationPoint(2.8, 6, 0.5),
                            new DepthDurationPoint(3.8, 9, 0.7),
                            new DepthDurationPoint(4.9, 12, 0.85),
                            new DepthDurationPoint(6.0, 16, 0.95)
                        }),
                    new RegionDefinition(
                        "Northwestern Division",
                        "High-gradient basins and irrigated valleys in the northern plains with faster drawdown.",
                        0.72,
                        65,
                        185,
                        0.13,
                        125,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(0.8, 2, 0.12),
                            new DepthDurationPoint(1.5, 4, 0.22),
                            new DepthDurationPoint(2.3, 6, 0.36),
                            new DepthDurationPoint(3.2, 9, 0.52),
                            new DepthDurationPoint(4.0, 12, 0.68)
                        }),
                    new RegionDefinition(
                        "Southwestern Division",
                        "Wide alluvial fans and interior plains from the Red River through Texas with intermittent flooding.",
                        0.78,
                        85,
                        220,
                        0.17,
                        152,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(0.9, 2, 0.14),
                            new DepthDurationPoint(1.6, 4, 0.25),
                            new DepthDurationPoint(2.4, 6, 0.4),
                            new DepthDurationPoint(3.3, 9, 0.58),
                            new DepthDurationPoint(4.1, 12, 0.74)
                        }),
                    new RegionDefinition(
                        "South Pacific Division",
                        "Irrigated valleys along California and Arizona rivers where managed systems reduce depth exposure.",
                        0.7,
                        50,
                        170,
                        0.1,
                        110,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(0.7, 2, 0.1),
                            new DepthDurationPoint(1.4, 4, 0.2),
                            new DepthDurationPoint(2.2, 6, 0.32),
                            new DepthDurationPoint(3.0, 8, 0.48),
                            new DepthDurationPoint(3.8, 11, 0.62)
                        }),
                    new RegionDefinition(
                        "Pacific Ocean Division",
                        "Tropical systems and volcanic island valleys with rapid runoff and shorter flood durations.",
                        0.76,
                        120,
                        275,
                        0.09,
                        198,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(0.6, 1.5, 0.09),
                            new DepthDurationPoint(1.2, 3, 0.18),
                            new DepthDurationPoint(2.0, 5, 0.32),
                            new DepthDurationPoint(2.8, 7, 0.5),
                            new DepthDurationPoint(3.6, 10, 0.66)
                        }),
                    new RegionDefinition(
                        "Texas Gulf Coast",
                        "Coastal prairie systems where tropical rainfall and surge can inundate cropland for extended periods.",
                        0.92,
                        110,
                        270,
                        0.22,
                        190,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(1.2, 3, 0.22),
                            new DepthDurationPoint(2.1, 5, 0.38),
                            new DepthDurationPoint(3.0, 8, 0.56),
                            new DepthDurationPoint(4.0, 11, 0.72),
                            new DepthDurationPoint(5.0, 14, 0.84)
                        }),
                    new RegionDefinition(
                        "Custom region",
                        "Define the location-specific depth-duration relationship and impact modifier for your project area.",
                        1.0,
                        90,
                        210,
                        0.25,
                        150,
                        0,
                        new[]
                        {
                            new DepthDurationPoint(1.0, 3, 0.2),
                            new DepthDurationPoint(2.5, 6, 0.45),
                            new DepthDurationPoint(4.0, 10, 0.7)
                        },
                        isCustom: true)
                };

                return regions;
            }
        }

        public class CropDefinition : BaseViewModel
        {
            private string _name;
            private string _description;
            private double _damageFactor;
            private double _impactModifier;

            public CropDefinition(string name, string description, double damageFactor, double impactModifier, bool isCustom = false)
            {
                _name = name;
                _description = description;
                _damageFactor = damageFactor;
                _impactModifier = impactModifier;
                IsCustom = isCustom;
            }

            public string Name
            {
                get => _name;
                set
                {
                    string adjusted = value?.Trim() ?? string.Empty;
                    if (_name == adjusted)
                    {
                        return;
                    }

                    _name = adjusted;
                    OnPropertyChanged();
                }
            }

            public string Description
            {
                get => _description;
                set
                {
                    string adjusted = value ?? string.Empty;
                    if (_description == adjusted)
                    {
                        return;
                    }

                    _description = adjusted;
                    OnPropertyChanged();
                }
            }

            public double DamageFactor
            {
                get => _damageFactor;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Clamp(value, 0.1, 5.0) : 1.0;
                    if (Math.Abs(_damageFactor - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _damageFactor = adjusted;
                    OnPropertyChanged();
                }
            }

            public double ImpactModifier
            {
                get => _impactModifier;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Clamp(value, 0.1, 5.0) : 1.0;
                    if (Math.Abs(_impactModifier - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _impactModifier = adjusted;
                    OnPropertyChanged();
                }
            }

            public bool IsCustom { get; }

            public static IEnumerable<CropDefinition> CreateDefaults()
            {
                var crops = new List<CropDefinition>
                {
                    new CropDefinition(
                        "Corn (grain)",
                        "Warm-season row crop with high yield potential but sensitivity during tasseling.",
                        0.95,
                        1.1),
                    new CropDefinition(
                        "Corn (silage)",
                        "Harvested at higher moisture with slightly more resilience during late season cutting.",
                        0.9,
                        1.05),
                    new CropDefinition(
                        "Soybeans",
                        "Legume crop with moderate flood resilience until pod fill.",
                        0.85,
                        0.95),
                    new CropDefinition(
                        "Wheat",
                        "Cool-season grain often harvested before peak flood season, offering lower damage exposure.",
                        0.7,
                        0.7),
                    new CropDefinition(
                        "Cotton",
                        "Perennial-like annual with longer season and high sensitivity to prolonged ponding.",
                        0.9,
                        1.05),
                    new CropDefinition(
                        "Rice",
                        "Flood-tolerant crop typically grown in managed paddies, dampening incremental damage from riverine flooding.",
                        0.6,
                        0.65),
                    new CropDefinition(
                        "Sorghum",
                        "Drought-tolerant grain that can endure modest inundation but is vulnerable during heading.",
                        0.8,
                        0.9),
                    new CropDefinition(
                        "Peanuts",
                        "Low-growing legume with underground pods susceptible to prolonged saturation.",
                        0.88,
                        1.0),
                    new CropDefinition(
                        "Sugarcane",
                        "Perennial grass with high biomass and extended harvest season concentrated along the Gulf Coast.",
                        0.92,
                        1.15),
                    new CropDefinition(
                        "Alfalfa / hay",
                        "Forage systems with moderate tolerance to standing water but rapid quality losses if ponded.",
                        0.65,
                        0.75),
                    new CropDefinition(
                        "Pasture / rangeland",
                        "Grazing lands with low investment per acre and faster recovery following shallow inundation.",
                        0.5,
                        0.6),
                    new CropDefinition(
                        "Vegetables (fresh market)",
                        "High-value specialty crops with acute sensitivity to even short flood events.",
                        0.98,
                        1.2),
                    new CropDefinition(
                        "Vegetables (processing)",
                        "Contract vegetable acreage with slightly lower value and staggered harvest windows.",
                        0.9,
                        1.0),
                    new CropDefinition(
                        "Fruit & nut orchards",
                        "Permanent tree crops where prolonged ponding can cause stand mortality and multi-year losses.",
                        0.96,
                        1.25),
                    new CropDefinition(
                        "Berries & vineyards",
                        "Perennial specialty fruit with trellised systems and elevated damage during bloom.",
                        0.94,
                        1.18),
                    new CropDefinition(
                        "Tobacco",
                        "Labor-intensive specialty crop common in the Southeast with low tolerance to saturated soils.",
                        0.93,
                        1.12),
                    new CropDefinition(
                        "Barley",
                        "Cool-season grain concentrated in the Northern Plains; NASS yield data indicate moderate resilience but quality losses after ponding.",
                        0.78,
                        0.82),
                    new CropDefinition(
                        "Oats",
                        "Spring cereal with relatively shallow roots and quick maturity reported by NASS across northern states.",
                        0.72,
                        0.78),
                    new CropDefinition(
                        "Canola",
                        "Oilseed crop tracked by NASS in the Dakotas and Pacific Northwest that suffers from saturation during flowering.",
                        0.84,
                        0.9),
                    new CropDefinition(
                        "Sunflowers",
                        "High-value oil and confection acreage documented by NASS; tall stalks tolerate shallow floods but seed heads are sensitive.",
                        0.88,
                        0.98),
                    new CropDefinition(
                        "Potatoes",
                        "Tubers grown in irrigated systems with extensive NASS reporting; sustained ponding quickly reduces marketable yield.",
                        0.97,
                        1.18),
                    new CropDefinition(
                        "Sugarbeets",
                        "Root crop with substantial NASS acreage in the Upper Midwest and West requiring well-drained soils.",
                        0.9,
                        1.05),
                    new CropDefinition(
                        "Dry edible beans",
                        "Pulse crop tracked by NASS that is vulnerable to waterlogging during pod fill and harvest.",
                        0.86,
                        0.96),
                    new CropDefinition(
                        "Custom crop",
                        "Define crop-specific sensitivity, damage factors, and narrative for localized analyses.",
                        1.0,
                        1.0,
                        isCustom: true)
                };

                return crops;
            }
        }

        public class StageDefinition
        {
            public StageDefinition(string name, string dateRange, string description, double defaultExposureDays, double defaultFloodToleranceDays, double weight)
            {
                Name = name;
                DateRange = dateRange;
                Description = description;
                DefaultExposureDays = defaultExposureDays;
                DefaultFloodToleranceDays = defaultFloodToleranceDays;
                Weight = weight;

                (StartDayOfYear, EndDayOfYear) = ParseDateRange(dateRange);
                DurationDays = CalculateInclusiveDuration(StartDayOfYear, EndDayOfYear);
            }

            public string Name { get; }
            public string DateRange { get; }
            public string Description { get; }
            public double DefaultExposureDays { get; }
            public double DefaultFloodToleranceDays { get; }
            public double Weight { get; }
            public int StartDayOfYear { get; }
            public int EndDayOfYear { get; }
            public int DurationDays { get; }

            private static readonly string[] DateFormats =
            {
                "MMM d",
                "MMM dd",
                "MMMM d",
                "MMMM dd"
            };

            private static (int start, int end) ParseDateRange(string dateRange)
            {
                if (string.IsNullOrWhiteSpace(dateRange))
                {
                    return (1, DaysInYear);
                }

                string normalized = dateRange
                    .Replace('—', '-')
                    .Replace('–', '-');

                var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    int start = ParseDate(parts[0]);
                    int end = ParseDate(parts[1]);
                    return (start, end);
                }

                int fallback = ParseDate(normalized);
                return (fallback, fallback);
            }

            private static int ParseDate(string text)
            {
                if (DateTime.TryParseExact(
                        text,
                        DateFormats,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out DateTime date))
                {
                    return NormalizeDayOfYear(date.DayOfYear);
                }

                return 1;
            }
        }

        public class StageExposure : BaseViewModel
        {
            private double _exposureDays;
            private double _floodToleranceDays;
            private double _timingModifier = 1.0;
            private double _overlapFraction = 1.0;
            private int _shiftedStartDayOfYear;
            private int _shiftedEndDayOfYear;
            private bool _wrapsYear;
            private int _appliedSeasonShiftDays;

            public StageExposure(StageDefinition stage)
            {
                Stage = stage;
                _exposureDays = stage.DefaultExposureDays;
                _floodToleranceDays = stage.DefaultFloodToleranceDays;
                ResetCommand = new RelayCommand(Reset);
                _shiftedStartDayOfYear = stage.StartDayOfYear;
                _shiftedEndDayOfYear = stage.EndDayOfYear;
            }

            public StageDefinition Stage { get; }

            public double ExposureDays
            {
                get => _exposureDays;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Max(0.0, value) : Stage.DefaultExposureDays;
                    if (Math.Abs(_exposureDays - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _exposureDays = adjusted;
                    OnPropertyChanged();
                }
            }

            public double FloodToleranceDays
            {
                get => _floodToleranceDays;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Max(0.1, value) : Stage.DefaultFloodToleranceDays;
                    if (Math.Abs(_floodToleranceDays - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _floodToleranceDays = adjusted;
                    OnPropertyChanged();
                }
            }

            public string StageName => Stage.Name;

            public string StageWindow
            {
                get
                {
                    string wrapText = Stage.StartDayOfYear <= Stage.EndDayOfYear
                        ? string.Empty
                        : " (wraps year end)";
                    return $"{Stage.DateRange} • Days {Stage.StartDayOfYear} – {Stage.EndDayOfYear}{wrapText}";
                }
            }

            public string StageGuidance
            {
                get
                {
                    string shiftPhrase = AppliedSeasonShiftDays == 0
                        ? "with no seasonal shift applied"
                        : $"after a {Math.Abs(AppliedSeasonShiftDays)} day {(AppliedSeasonShiftDays > 0 ? "later" : "earlier")} shift";
                    return $"{Stage.Description} Approximately {(OverlapFraction * 100):0.#}% of this growth window overlaps the regional flood season {shiftPhrase}, scaling the stress ratio by ×{TimingModifier:0.##}.";
                }
            }

            public string TimingWindowDisplay
            {
                get
                {
                    string shiftPhrase = AppliedSeasonShiftDays == 0
                        ? "no seasonal shift"
                        : $"{Math.Abs(AppliedSeasonShiftDays)} day {(AppliedSeasonShiftDays > 0 ? "later" : "earlier")} shift";
                    string wrapNote = WrapsYear ? " (wraps year end)" : string.Empty;
                    return $"Shifted window ({shiftPhrase}): days {ShiftedStartDayOfYear} – {ShiftedEndDayOfYear}{wrapNote}";
                }
            }

            public string TimingModifierDisplay =>
                $"Flood-season overlap: {(OverlapFraction * 100):0.#}% • Timing modifier ×{TimingModifier:0.##}";

            public string DefaultToleranceDisplay => $"Default tolerance: {Stage.DefaultFloodToleranceDays:0.#} days";

            public ICommand ResetCommand { get; }

            public void Reset()
            {
                ExposureDays = Stage.DefaultExposureDays;
                FloodToleranceDays = Stage.DefaultFloodToleranceDays;
            }

            public double GetStressRatio()
            {
                if (FloodToleranceDays <= 0)
                {
                    return 1.0;
                }

                double ratio = ExposureDays / FloodToleranceDays;
                if (!double.IsFinite(ratio))
                {
                    return 0.0;
                }

                return Math.Clamp(ratio, 0.0, 2.0);
            }

            public double TimingModifier => _timingModifier;
            public double OverlapFraction => _overlapFraction;
            public int ShiftedStartDayOfYear => _shiftedStartDayOfYear;
            public int ShiftedEndDayOfYear => _shiftedEndDayOfYear;
            public bool WrapsYear => _wrapsYear;
            public int AppliedSeasonShiftDays => _appliedSeasonShiftDays;

            public void ApplyTiming(int shiftedStartDay, int shiftedEndDay, bool wrapsYear, double overlapFraction, double timingModifier, int seasonShiftDays)
            {
                _shiftedStartDayOfYear = shiftedStartDay;
                _shiftedEndDayOfYear = shiftedEndDay;
                _wrapsYear = wrapsYear;
                _overlapFraction = overlapFraction;
                _timingModifier = timingModifier;
                _appliedSeasonShiftDays = seasonShiftDays;

                OnPropertyChanged(nameof(ShiftedStartDayOfYear));
                OnPropertyChanged(nameof(ShiftedEndDayOfYear));
                OnPropertyChanged(nameof(WrapsYear));
                OnPropertyChanged(nameof(OverlapFraction));
                OnPropertyChanged(nameof(TimingModifier));
                OnPropertyChanged(nameof(AppliedSeasonShiftDays));
                OnPropertyChanged(nameof(TimingWindowDisplay));
                OnPropertyChanged(nameof(TimingModifierDisplay));
                OnPropertyChanged(nameof(StageGuidance));
            }

            public static IEnumerable<StageExposure> CreateDefaults()
            {
                var stages = new[]
                {
                    new StageDefinition(
                        "Stand establishment",
                        "Apr 1 – May 15",
                        "Represents early stand establishment and germination when seedlings are most vulnerable to saturated soils.",
                        12,
                        15,
                        0.2),
                    new StageDefinition(
                        "Vegetative growth",
                        "May 15 – Jul 5",
                        "Canopy development and rapid biomass accumulation.",
                        6,
                        12.5,
                        0.3),
                    new StageDefinition(
                        "Silking / tassel",
                        "Jul 5 – Aug 5",
                        "Critical reproductive window where even short ponding can cause losses.",
                        2,
                        5,
                        0.3),
                    new StageDefinition(
                        "Maturity & dry down",
                        "Aug 5 – Oct 15",
                        "Late season ripening and harvest preparation.",
                        3,
                        12,
                        0.2)
                };

                return stages.Select(stage => new StageExposure(stage));
            }
        }

        private sealed record RasterData(
            int Width,
            int Height,
            double[] Values,
            double PixelWidth,
            double PixelHeight,
            double OriginX,
            double OriginY,
            string ProjectionName,
            string ProjectionWkt,
            bool WasReprojected,
            string SourceProjectionName);
        private sealed record PolygonShapeData(Rect Bounds, IReadOnlyList<Point> Vertices);

        public record EstimatorCdlSummaryRow(int CropCode, string CropName, long PixelCount, double Acres, double PercentOfRaster);

        public class EstimatorSpatialCropRow : BaseViewModel
        {
            private int _cropCode;
            private string _cropName;
            private double _acres;
            private double _averageDepthFeet;

            public EstimatorSpatialCropRow(int cropCode, string cropName, double acres, double averageDepthFeet)
            {
                _cropCode = cropCode;
                _cropName = cropName;
                _acres = acres;
                _averageDepthFeet = averageDepthFeet;
            }

            public int CropCode { get => _cropCode; set { _cropCode = value; OnPropertyChanged(); } }
            public string CropName { get => _cropName; set { _cropName = value; OnPropertyChanged(); } }
            public double Acres { get => _acres; set { _acres = Math.Max(0, value); OnPropertyChanged(); } }
            public double AverageDepthFeet { get => _averageDepthFeet; set { _averageDepthFeet = Math.Max(0, value); OnPropertyChanged(); } }
        }

        public class EstimatorEventRow : BaseViewModel
        {
            private string _name;
            private double _depthFeet;
            private int _floodMonth;
            private string _annualExceedanceProbabilitiesCsv;
            private double _returnPeriodYears;

            public EstimatorEventRow(string name, double depthFeet, int floodMonth, string annualExceedanceProbabilitiesCsv, double returnPeriodYears)
            {
                _name = name;
                _depthFeet = depthFeet;
                _floodMonth = floodMonth;
                _annualExceedanceProbabilitiesCsv = annualExceedanceProbabilitiesCsv;
                _returnPeriodYears = returnPeriodYears;
            }

            public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
            public double DepthFeet { get => _depthFeet; set { _depthFeet = Math.Max(0.0, value); OnPropertyChanged(); } }
            public int FloodMonth { get => _floodMonth; set { _floodMonth = Math.Clamp(value, 1, 12); OnPropertyChanged(); } }
            public string AnnualExceedanceProbabilitiesCsv { get => _annualExceedanceProbabilitiesCsv; set { _annualExceedanceProbabilitiesCsv = value; OnPropertyChanged(); } }
            public double ReturnPeriodYears { get => _returnPeriodYears; set { _returnPeriodYears = Math.Max(0.1, value); OnPropertyChanged(); } }
        }

        public class EstimatorCropRow : BaseViewModel
        {
            private int _cropCode;
            private string _cropName;
            private string _eventName;
            private double _acres;
            private double _valuePerAcre;
            private string _growingMonthsCsv;
            private string _specificCurve;

            public EstimatorCropRow(int cropCode, string cropName, string eventName, double acres, double valuePerAcre, string growingMonthsCsv, string specificCurve)
            {
                _cropCode = cropCode;
                _cropName = cropName;
                _eventName = eventName;
                _acres = acres;
                _valuePerAcre = valuePerAcre;
                _growingMonthsCsv = growingMonthsCsv;
                _specificCurve = specificCurve;
            }

            public int CropCode { get => _cropCode; set { _cropCode = value; OnPropertyChanged(); } }
            public string CropName { get => _cropName; set { _cropName = value; OnPropertyChanged(); } }
            public string EventName { get => _eventName; set { _eventName = value; OnPropertyChanged(); } }
            public double Acres { get => _acres; set { _acres = Math.Max(0.0, value); OnPropertyChanged(); } }
            public double ValuePerAcre { get => _valuePerAcre; set { _valuePerAcre = Math.Max(0.0, value); OnPropertyChanged(); } }
            public string GrowingMonthsCsv { get => _growingMonthsCsv; set { _growingMonthsCsv = value; OnPropertyChanged(); } }
            public string SpecificCurve { get => _specificCurve; set { _specificCurve = value; OnPropertyChanged(); } }
        }

        public record EstimatorSummaryRow(string Metric, string Value, string Description);

        public record EstimatorResultRow(string EventName, double MeanDamage, double StdDamage, double P5Damage, double P95Damage, double DiscreteEadContribution);

        public class DepthDurationPoint : BaseViewModel
        {
            private double _depthFeet;
            private double _durationDays;
            private double _baseDamage;

            public DepthDurationPoint(double depthFeet, double durationDays, double baseDamage)
            {
                _depthFeet = double.IsFinite(depthFeet) ? Math.Max(0.0, depthFeet) : 0.0;
                _durationDays = double.IsFinite(durationDays) ? Math.Max(0.1, durationDays) : 0.1;
                _baseDamage = double.IsFinite(baseDamage) ? Math.Clamp(baseDamage, 0.0, 1.0) : 0.0;
            }

            public double DepthFeet
            {
                get => _depthFeet;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Max(0.0, value) : _depthFeet;
                    if (Math.Abs(_depthFeet - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _depthFeet = adjusted;
                    OnPropertyChanged();
                }
            }

            public double DurationDays
            {
                get => _durationDays;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Max(0.1, value) : _durationDays;
                    if (Math.Abs(_durationDays - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _durationDays = adjusted;
                    OnPropertyChanged();
                }
            }

            public double BaseDamage
            {
                get => _baseDamage;
                set
                {
                    double adjusted = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : _baseDamage;
                    if (Math.Abs(_baseDamage - adjusted) < 1e-6)
                    {
                        return;
                    }

                    _baseDamage = adjusted;
                    OnPropertyChanged();
                }
            }

            public DepthDurationPoint Clone() => new DepthDurationPoint(DepthFeet, DurationDays, BaseDamage);
        }

        public record DepthDurationDamageRow(double DepthFeet, double DurationDays, double DamagePercent);

        public record CropScapeDamageRow(double DepthFeet, double DurationDays, double DamagePercent, double DamagedAcres, double ResidualAcres, double TotalAcres);
    }
}
