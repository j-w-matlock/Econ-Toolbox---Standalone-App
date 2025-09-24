using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;

namespace EconToolbox.Desktop.ViewModels
{
    public class AgricultureDepthDamageViewModel : BaseViewModel
    {
        private readonly RelayCommand _computeCommand;
        private readonly RelayCommand _exportCommand;
        private bool _isInitializing = true;

        public ObservableCollection<RegionDefinition> Regions { get; }
        public ObservableCollection<CropDefinition> Crops { get; }
        public ObservableCollection<StageExposure> StageExposures { get; }
        public ObservableCollection<DepthDurationDamageRow> DepthDurationRows { get; } = new();

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

                _selectedRegion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedRegionDescription));
                _computeCommand.RaiseCanExecuteChanged();
                if (!_isInitializing)
                {
                    ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
                }
            }
        }

        public string? SelectedRegionDescription => SelectedRegion?.Description;

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

                _selectedCrop = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCropDescription));
                _computeCommand.RaiseCanExecuteChanged();
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

        public ICommand ComputeCommand => _computeCommand;
        public ICommand ExportCommand => _exportCommand;

        public AgricultureDepthDamageViewModel()
        {
            Regions = new ObservableCollection<RegionDefinition>(RegionDefinition.CreateDefaults());
            Crops = new ObservableCollection<CropDefinition>(CropDefinition.CreateDefaults());
            StageExposures = new ObservableCollection<StageExposure>(StageExposure.CreateDefaults());

            foreach (var stage in StageExposures)
            {
                stage.PropertyChanged += Stage_PropertyChanged;
            }

            _computeCommand = new RelayCommand(Compute, CanCompute);
            _exportCommand = new RelayCommand(Export, () => DepthDurationRows.Count > 0);

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

        private void Stage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            ImpactSummary = "Inputs updated. Press Calculate to refresh results.";
        }

        private bool CanCompute() => SelectedRegion != null && SelectedCrop != null;

        private void Compute()
        {
            if (!CanCompute())
            {
                ModeledImpactProbability = 0;
                MeanDamagePercent = 0;
                DepthDurationRows.Clear();
                ImpactSummary = "Select a region and crop to calculate flood impacts.";
                CropInsight = "";
                _exportCommand.RaiseCanExecuteChanged();
                return;
            }

            double totalWeight = StageExposures.Sum(s => s.Stage.Weight);
            double weightedStress = StageExposures.Sum(s => s.Stage.Weight * s.GetStressRatio());
            double normalizedStress = totalWeight > 0 ? weightedStress / totalWeight : 0;
            normalizedStress = Math.Clamp(normalizedStress, 0.0, 2.0);

            double responseScaling = Math.Clamp(AverageResponse, 0.1, 5.0);
            double probabilityDriver = normalizedStress * SelectedRegion!.ImpactModifier * SelectedCrop!.ImpactModifier * responseScaling;
            double probability = 1.0 - Math.Exp(-probabilityDriver);
            ModeledImpactProbability = Math.Clamp(probability, 0.0, 1.0);

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

            ImpactSummary =
                $"Simulated {SimulationYears:N0} seasons with {StageExposures.Count} growth stages. Estimated flood impact probability: {ModeledImpactProbabilityDisplay}.";

            CropInsight =
                $"Average expected damage across representative depth-duration events is {MeanDamageDisplay}. Adjust exposure days or tolerance to explore resilience.";

            _exportCommand.RaiseCanExecuteChanged();
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

        public class RegionDefinition
        {
            public RegionDefinition(string name, string description, double impactModifier, IReadOnlyList<DepthDurationPoint> depthDuration)
            {
                Name = name;
                Description = description;
                ImpactModifier = impactModifier;
                DepthDuration = depthDuration;
                MaxDuration = depthDuration.Count == 0 ? 1.0 : depthDuration.Max(p => p.DurationDays);
            }

            public string Name { get; }
            public string Description { get; }
            public double ImpactModifier { get; }
            public IReadOnlyList<DepthDurationPoint> DepthDuration { get; }
            public double MaxDuration { get; }

            public static IEnumerable<RegionDefinition> CreateDefaults()
            {
                return new[]
                {
                    new RegionDefinition(
                        "Midwest",
                        "Represents cropland in the Ohio and lower Mississippi River basins with moderate levee protection.",
                        0.85,
                        new[]
                        {
                            new DepthDurationPoint(1.2, 3, 0.18),
                            new DepthDurationPoint(2.1, 5, 0.32),
                            new DepthDurationPoint(3.0, 7, 0.5),
                            new DepthDurationPoint(4.2, 10, 0.68),
                            new DepthDurationPoint(5.1, 14, 0.82)
                        }),
                    new RegionDefinition(
                        "Great Plains",
                        "Captures broad, relatively flat floodplains along interior prairie rivers.",
                        0.75,
                        new[]
                        {
                            new DepthDurationPoint(0.8, 2, 0.12),
                            new DepthDurationPoint(1.5, 4, 0.22),
                            new DepthDurationPoint(2.4, 6, 0.36),
                            new DepthDurationPoint(3.6, 9, 0.58),
                            new DepthDurationPoint(4.2, 12, 0.74)
                        }),
                    new RegionDefinition(
                        "Mississippi Delta",
                        "Low-lying backwater areas with prolonged inundation potential and shallow topographic relief.",
                        1.05,
                        new[]
                        {
                            new DepthDurationPoint(1.5, 4, 0.24),
                            new DepthDurationPoint(2.8, 6, 0.42),
                            new DepthDurationPoint(3.5, 9, 0.63),
                            new DepthDurationPoint(4.8, 12, 0.78),
                            new DepthDurationPoint(5.6, 16, 0.9)
                        }),
                    new RegionDefinition(
                        "Mountain West",
                        "Irrigated valleys with flashy runoff and faster drainage.",
                        0.65,
                        new[]
                        {
                            new DepthDurationPoint(0.6, 1, 0.1),
                            new DepthDurationPoint(1.4, 3, 0.18),
                            new DepthDurationPoint(2.1, 5, 0.3),
                            new DepthDurationPoint(3.2, 7, 0.45),
                            new DepthDurationPoint(3.8, 10, 0.6)
                        })
                };
            }
        }

        public class CropDefinition
        {
            public CropDefinition(string name, string description, double damageFactor, double impactModifier)
            {
                Name = name;
                Description = description;
                DamageFactor = damageFactor;
                ImpactModifier = impactModifier;
            }

            public string Name { get; }
            public string Description { get; }
            public double DamageFactor { get; }
            public double ImpactModifier { get; }

            public static IEnumerable<CropDefinition> CreateDefaults()
            {
                return new[]
                {
                    new CropDefinition(
                        "Corn (grain)",
                        "Warm-season row crop with high yield potential but sensitivity during tasseling.",
                        0.95,
                        1.1),
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
                        0.65)
                };
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
            }

            public string Name { get; }
            public string DateRange { get; }
            public string Description { get; }
            public double DefaultExposureDays { get; }
            public double DefaultFloodToleranceDays { get; }
            public double Weight { get; }
        }

        public class StageExposure : BaseViewModel
        {
            private double _exposureDays;
            private double _floodToleranceDays;

            public StageExposure(StageDefinition stage)
            {
                Stage = stage;
                _exposureDays = stage.DefaultExposureDays;
                _floodToleranceDays = stage.DefaultFloodToleranceDays;
                ResetCommand = new RelayCommand(Reset);
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
            public string StageWindow => Stage.DateRange;
            public string StageGuidance => Stage.Description;
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

        public record DepthDurationPoint(double DepthFeet, double DurationDays, double BaseDamage);

        public record DepthDurationDamageRow(double DepthFeet, double DurationDays, double DamagePercent);
    }
}
