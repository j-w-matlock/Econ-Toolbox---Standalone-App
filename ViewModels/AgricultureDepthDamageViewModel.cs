using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

        private readonly RelayCommand _addDepthDurationPointCommand;
        private readonly RelayCommand _removeDepthDurationPointCommand;

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
                _computeCommand.RaiseCanExecuteChanged();
                _addDepthDurationPointCommand.RaiseCanExecuteChanged();
                _removeDepthDurationPointCommand.RaiseCanExecuteChanged();
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
                _removeDepthDurationPointCommand.RaiseCanExecuteChanged();
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
        public ICommand AddDepthDurationPointCommand => _addDepthDurationPointCommand;
        public ICommand RemoveDepthDurationPointCommand => _removeDepthDurationPointCommand;

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
            _addDepthDurationPointCommand = new RelayCommand(AddDepthDurationPoint, () => SelectedRegion != null);
            _removeDepthDurationPointCommand = new RelayCommand(RemoveDepthDurationPoint, () => SelectedRegionPoint != null);

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

            _removeDepthDurationPointCommand.RaiseCanExecuteChanged();

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

        public class RegionDefinition : BaseViewModel
        {
            private string _name;
            private string _description;
            private double _impactModifier;

            public RegionDefinition(string name, string description, double impactModifier, IEnumerable<DepthDurationPoint> depthDuration, bool isCustom = false)
            {
                _name = name;
                _description = description;
                _impactModifier = impactModifier;
                IsCustom = isCustom;
                DepthDuration = new ObservableCollection<DepthDurationPoint>(depthDuration.Select(p => p.Clone()));
                DepthDuration.CollectionChanged += DepthDuration_CollectionChanged;
                foreach (var point in DepthDuration)
                {
                    point.PropertyChanged += DepthDurationPoint_PropertyChanged;
                }
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

            public ObservableCollection<DepthDurationPoint> DepthDuration { get; }

            public bool IsCustom { get; }

            public double MaxDuration => DepthDuration.Count == 0 ? 1.0 : DepthDuration.Max(p => p.DurationDays);

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
                    if (!IsCustom)
                    {
                        return;
                    }

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
                    if (!IsCustom)
                    {
                        return;
                    }

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
                    if (!IsCustom)
                    {
                        adjusted = _damageFactor;
                    }

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
                    if (!IsCustom)
                    {
                        adjusted = _impactModifier;
                    }

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
    }
}
