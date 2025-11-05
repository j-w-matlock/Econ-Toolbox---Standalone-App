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
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{
    public class AgricultureDepthDamageViewModel : BaseViewModel
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

        public ICommand ComputeCommand => _computeCommand;
        public ICommand ExportCommand => _exportCommand;
        public ICommand AddDepthDurationPointCommand => _addDepthDurationPointCommand;
        public ICommand RemoveDepthDurationPointCommand => _removeDepthDurationPointCommand;
        public IAsyncRelayCommand ImportCropScapeRasterCommand => _importCropScapeRasterCommand;
        public ICommand ClearCropScapeSummaryCommand => _clearCropScapeSummaryCommand;

        public AgricultureDepthDamageViewModel()
        {
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

            if (e.PropertyName == nameof(RegionDefinition.Description) || e.PropertyName == nameof(RegionDefinition.Name))
            {
                OnPropertyChanged(nameof(SelectedRegionDescription));
                ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
                return;
            }

            if (e.PropertyName == nameof(RegionDefinition.FloodWindowStartDay)
                || e.PropertyName == nameof(RegionDefinition.FloodWindowEndDay)
                || e.PropertyName == nameof(RegionDefinition.FloodSeasonPeakDay)
                || e.PropertyName == nameof(RegionDefinition.SeasonShiftDays))
            {
                Compute();
                return;
            }

            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
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

            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
        }

        private void DepthDurationPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
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

            if (e.PropertyName == nameof(CropDefinition.Description) || e.PropertyName == nameof(CropDefinition.Name))
            {
                OnPropertyChanged(nameof(SelectedCropDescription));
            }

            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
        }

        private void Stage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

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

                var areas = await Task.Run(() => _cropScapeRasterService.ReadClassAreas(filePath));

                CropScapeSummaries.Clear();

                if (areas.Count == 0)
                {
                    CropScapeTotalAcreage = 0;
                    CropScapeImportStatus = $"No crop classes found in \"{Path.GetFileName(filePath)}\".";
                    return;
                }

                double totalAcres = areas.Sum(area => area.Acres);

                foreach (var area in areas)
                {
                    double share = totalAcres > 0 ? area.Acres / totalAcres : 0;
                    CropScapeSummaries.Add(new CropScapeAcreageSummary(area.Code, area.Name, area.PixelCount, area.Acres, share));
                }

                CropScapeTotalAcreage = totalAcres;

                CropScapeImportStatus = $"Loaded {CropScapeSummaries.Count} crop classes from \"{Path.GetFileName(filePath)}\".";
            }
            catch (Exception ex)
            {
                CropScapeImportStatus = $"Failed to import CropScape raster: {ex.Message}";
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
            OnPropertyChanged(nameof(HasCropScapeSummary));
            _clearCropScapeSummaryCommand.NotifyCanExecuteChanged();
        }

        private void CropScapeDamageRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasCropScapeDamage));
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
                    residualAcres));

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
                return;
            }

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

        private void Export()
        {
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

            var lines = new List<string>
            {
                "Depth (ft),Duration (days),Damage (%)"
            };

            lines.AddRange(DepthDurationRows.Select(r => string.Join(',',
                r.DepthFeet.ToString("0.##", CultureInfo.InvariantCulture),
                r.DurationDays.ToString("0.##", CultureInfo.InvariantCulture),
                r.DamagePercent.ToString("0.##", CultureInfo.InvariantCulture))));

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
                _floodWindowStartDay = Math.Clamp(floodWindowStartDay, 1, 366);
                _floodWindowEndDay = Math.Clamp(floodWindowEndDay, _floodWindowStartDay, 366);
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
                    int adjusted = Math.Clamp(value, 1, 366);
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
                    int adjusted = Math.Clamp(value, 1, 366);
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

        public record CropScapeDamageRow(double DepthFeet, double DurationDays, double DamagePercent, double DamagedAcres, double ResidualAcres);
    }
}
