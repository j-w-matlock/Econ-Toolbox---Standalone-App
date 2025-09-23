using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels
{
    public class AgriculturalDamageViewModel : BaseViewModel
    {
        private readonly RelayCommand _computeCommand;
        private FloodRegionProfile? _selectedRegion;
        private CropOption? _selectedCropOption;
        private CropDamageProfile? _selectedCrop;
        private CropDamageProfile? _customCropProfile;
        private double _fieldAcreage = 1.0;
        private int _simulationYears = 5000;
        private double _probabilityOfImpact;
        private double _averageVulnerability;
        private double _averageToleranceDays;
        private string _probabilitySummary = string.Empty;
        private string _monteCarloNarrative = string.Empty;
        private string _summaryText = string.Empty;
        private string _mostVulnerableStage = string.Empty;
        private string _customCropName = "Custom crop";
        private string _customOccupancyType = "Agricultural Field - Custom";
        private string _customDescription = "Define crop value, stages, and flood sensitivity.";
        private double _customValuePerAcre = 800;
        private int _customPlantingStartDay = 110;
        private int _customPlantingEndDay = 140;
        private int _customHarvestEndDay = 280;
        private string _customProfileStatus = "Enter crop details to build a custom profile.";

        public AgriculturalDamageViewModel()
        {
            Regions = new ObservableCollection<FloodRegionProfile>(AgriculturalDamageLibrary.Regions);
            CropOptions = new ObservableCollection<CropOption>(
                AgriculturalDamageLibrary.Crops.Select(c => new CropOption(c.CropName, c)));
            CropOptions.Add(new CropOption("Custom crop (configure below)", null, true));
            DamageTable = new ObservableCollection<DamageTableRow>();
            CustomStages = new ObservableCollection<CustomStageInput>();
            CustomDamageCurve = new ObservableCollection<CustomDamagePointInput>();

            CustomStages.CollectionChanged += CustomStagesCollectionChanged;
            CustomDamageCurve.CollectionChanged += CustomDamageCurveCollectionChanged;

            AddCustomStageCommand = new RelayCommand(_ => AddCustomStage());
            RemoveCustomStageCommand = new RelayCommand(
                parameter => RemoveCustomStage(parameter as CustomStageInput),
                parameter => parameter is CustomStageInput);
            AddCustomDamagePointCommand = new RelayCommand(_ => AddCustomDamagePoint());
            RemoveCustomDamagePointCommand = new RelayCommand(
                parameter => RemoveCustomDamagePoint(parameter as CustomDamagePointInput),
                parameter => parameter is CustomDamagePointInput);

            InitializeCustomDefaults();

            _computeCommand = new RelayCommand(Compute, CanCompute);
            SelectedRegion = Regions.FirstOrDefault();
            SelectedCropOption = CropOptions.FirstOrDefault();
        }

        public ObservableCollection<FloodRegionProfile> Regions { get; }
        public ObservableCollection<CropOption> CropOptions { get; }
        public ObservableCollection<DamageTableRow> DamageTable { get; }
        public ObservableCollection<CustomStageInput> CustomStages { get; }
        public ObservableCollection<CustomDamagePointInput> CustomDamageCurve { get; }

        public ICommand ComputeCommand => _computeCommand;
        public ICommand AddCustomStageCommand { get; }
        public ICommand RemoveCustomStageCommand { get; }
        public ICommand AddCustomDamagePointCommand { get; }
        public ICommand RemoveCustomDamagePointCommand { get; }

        public FloodRegionProfile? SelectedRegion
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
                OnPropertyChanged(nameof(RegionDescription));
                OnPropertyChanged(nameof(GrowthWindowSummary));
                OnPropertyChanged(nameof(GrowthStageSummaries));
                _computeCommand.RaiseCanExecuteChanged();
            }
        }

        public CropDamageProfile? SelectedCrop
        {
            get => _selectedCrop;
            private set
            {
                if (_selectedCrop == value)
                {
                    return;
                }

                _selectedCrop = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OccupancyType));
                OnPropertyChanged(nameof(CropDescription));
                OnPropertyChanged(nameof(GrowthWindowSummary));
                OnPropertyChanged(nameof(GrowthStageSummaries));
                _computeCommand.RaiseCanExecuteChanged();
            }
        }

        public CropOption? SelectedCropOption
        {
            get => _selectedCropOption;
            set
            {
                if (_selectedCropOption == value)
                {
                    return;
                }

                _selectedCropOption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomSelected));

                if (IsCustomSelected)
                {
                    UpdateCustomCropProfile();
                }
                else
                {
                    SelectedCrop = value?.Profile;
                }

                OnPropertyChanged(nameof(CropDescription));
                OnPropertyChanged(nameof(OccupancyType));
            }
        }

        public bool IsCustomSelected => SelectedCropOption?.IsCustom == true;

        public string CustomCropName
        {
            get => _customCropName;
            set
            {
                if (_customCropName == value)
                {
                    return;
                }

                _customCropName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OccupancyType));
                UpdateCustomCropProfile();
            }
        }

        public string CustomOccupancyType
        {
            get => _customOccupancyType;
            set
            {
                if (_customOccupancyType == value)
                {
                    return;
                }

                _customOccupancyType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OccupancyType));
                UpdateCustomCropProfile();
            }
        }

        public string CustomDescription
        {
            get => _customDescription;
            set
            {
                if (_customDescription == value)
                {
                    return;
                }

                _customDescription = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CropDescription));
                UpdateCustomCropProfile();
            }
        }

        public double CustomValuePerAcre
        {
            get => _customValuePerAcre;
            set
            {
                if (Math.Abs(_customValuePerAcre - value) < 0.0001)
                {
                    return;
                }

                _customValuePerAcre = value;
                OnPropertyChanged();
                UpdateCustomCropProfile();
            }
        }

        public int CustomPlantingStartDay
        {
            get => _customPlantingStartDay;
            set
            {
                if (_customPlantingStartDay == value)
                {
                    return;
                }

                _customPlantingStartDay = value;
                OnPropertyChanged();
                UpdateCustomCropProfile();
            }
        }

        public int CustomPlantingEndDay
        {
            get => _customPlantingEndDay;
            set
            {
                if (_customPlantingEndDay == value)
                {
                    return;
                }

                _customPlantingEndDay = value;
                OnPropertyChanged();
                UpdateCustomCropProfile();
            }
        }

        public int CustomHarvestEndDay
        {
            get => _customHarvestEndDay;
            set
            {
                if (_customHarvestEndDay == value)
                {
                    return;
                }

                _customHarvestEndDay = value;
                OnPropertyChanged();
                UpdateCustomCropProfile();
            }
        }

        public string CustomProfileStatus
        {
            get => _customProfileStatus;
            private set
            {
                if (_customProfileStatus == value)
                {
                    return;
                }

                _customProfileStatus = value;
                OnPropertyChanged();
            }
        }

        private void InitializeCustomDefaults()
        {
            if (CustomStages.Count == 0)
            {
                CustomStages.Add(new CustomStageInput
                {
                    Name = "Stand establishment",
                    StartOffsetDays = 0,
                    EndOffsetDays = 25,
                    Vulnerability = 0.35,
                    FloodToleranceDays = 2.5
                });
                CustomStages.Add(new CustomStageInput
                {
                    Name = "Vegetative growth",
                    StartOffsetDays = 25,
                    EndOffsetDays = 70,
                    Vulnerability = 0.6,
                    FloodToleranceDays = 3.0
                });
                CustomStages.Add(new CustomStageInput
                {
                    Name = "Reproductive",
                    StartOffsetDays = 70,
                    EndOffsetDays = 110,
                    Vulnerability = 0.9,
                    FloodToleranceDays = 1.5
                });
                CustomStages.Add(new CustomStageInput
                {
                    Name = "Maturity",
                    StartOffsetDays = 110,
                    EndOffsetDays = 150,
                    Vulnerability = 0.45,
                    FloodToleranceDays = 2.0
                });
            }

            if (CustomDamageCurve.Count == 0)
            {
                CustomDamageCurve.Add(new CustomDamagePointInput { DepthFeet = 0.5, DamagePercent = 5 });
                CustomDamageCurve.Add(new CustomDamagePointInput { DepthFeet = 1, DamagePercent = 15 });
                CustomDamageCurve.Add(new CustomDamagePointInput { DepthFeet = 2, DamagePercent = 35 });
                CustomDamageCurve.Add(new CustomDamagePointInput { DepthFeet = 3, DamagePercent = 55 });
                CustomDamageCurve.Add(new CustomDamagePointInput { DepthFeet = 4, DamagePercent = 75 });
            }
        }

        private void CustomStagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CustomStageInput stage in e.NewItems)
                {
                    stage.PropertyChanged += CustomStagePropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (CustomStageInput stage in e.OldItems)
                {
                    stage.PropertyChanged -= CustomStagePropertyChanged;
                }
            }

            if (IsCustomSelected)
            {
                UpdateCustomCropProfile();
            }
        }

        private void CustomDamageCurveCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CustomDamagePointInput point in e.NewItems)
                {
                    point.PropertyChanged += CustomDamagePointPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (CustomDamagePointInput point in e.OldItems)
                {
                    point.PropertyChanged -= CustomDamagePointPropertyChanged;
                }
            }

            if (IsCustomSelected)
            {
                UpdateCustomCropProfile();
            }
        }

        private void CustomStagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsCustomSelected)
            {
                UpdateCustomCropProfile();
            }
        }

        private void CustomDamagePointPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsCustomSelected)
            {
                UpdateCustomCropProfile();
            }
        }

        private void AddCustomStage()
        {
            int start = CustomStages.Count > 0 ? CustomStages.Last().EndOffsetDays : 0;
            var stage = new CustomStageInput
            {
                Name = $"Stage {CustomStages.Count + 1}",
                StartOffsetDays = start,
                EndOffsetDays = start + 30,
                Vulnerability = 0.5,
                FloodToleranceDays = 2.0
            };

            CustomStages.Add(stage);
        }

        private void RemoveCustomStage(CustomStageInput? stage)
        {
            if (stage == null)
            {
                return;
            }

            CustomStages.Remove(stage);
        }

        private void AddCustomDamagePoint()
        {
            double nextDepth = CustomDamageCurve.Count > 0
                ? Math.Round(CustomDamageCurve.Last().DepthFeet + 1, 1)
                : 0.5;
            double nextDamage = CustomDamageCurve.Count > 0
                ? Math.Min(100, CustomDamageCurve.Last().DamagePercent + 15)
                : 10;

            var point = new CustomDamagePointInput
            {
                DepthFeet = nextDepth,
                DamagePercent = nextDamage
            };

            CustomDamageCurve.Add(point);
        }

        private void RemoveCustomDamagePoint(CustomDamagePointInput? point)
        {
            if (point == null)
            {
                return;
            }

            CustomDamageCurve.Remove(point);
        }

        private void UpdateCustomCropProfile()
        {
            if (!IsCustomSelected)
            {
                return;
            }

            if (TryBuildCustomProfile(out var profile, out string statusMessage))
            {
                _customCropProfile = profile;
                CustomProfileStatus = statusMessage;
                SelectedCrop = _customCropProfile;
            }
            else
            {
                _customCropProfile = null;
                CustomProfileStatus = statusMessage;
                SelectedCrop = null;
            }

            OnPropertyChanged(nameof(CropDescription));
            OnPropertyChanged(nameof(OccupancyType));
        }

        private bool TryBuildCustomProfile(out CropDamageProfile? profile, out string statusMessage)
        {
            profile = null;

            if (string.IsNullOrWhiteSpace(CustomCropName))
            {
                statusMessage = "Enter a crop name to label the profile.";
                return false;
            }

            if (CustomValuePerAcre <= 0)
            {
                statusMessage = "Value per acre must be greater than zero.";
                return false;
            }

            if (!IsValidDay(CustomPlantingStartDay) || !IsValidDay(CustomPlantingEndDay) || !IsValidDay(CustomHarvestEndDay))
            {
                statusMessage = "Calendar days must be between 1 and 365.";
                return false;
            }

            if (CustomPlantingEndDay < CustomPlantingStartDay)
            {
                statusMessage = "Planting end day must be on or after the start day.";
                return false;
            }

            if (CustomHarvestEndDay < CustomPlantingEndDay)
            {
                statusMessage = "Harvest end day must occur after planting is complete.";
                return false;
            }

            if (CustomStages.Count == 0)
            {
                statusMessage = "Add at least one growth stage describing the season.";
                return false;
            }

            if (CustomDamageCurve.Count < 2)
            {
                statusMessage = "Add two or more depth points to define the damage curve.";
                return false;
            }

            foreach (var stage in CustomStages)
            {
                if (string.IsNullOrWhiteSpace(stage.Name))
                {
                    statusMessage = "Provide a name for each growth stage.";
                    return false;
                }

                if (stage.StartOffsetDays < 0)
                {
                    statusMessage = "Stage offsets cannot be negative.";
                    return false;
                }

                if (stage.EndOffsetDays < stage.StartOffsetDays)
                {
                    statusMessage = "Stage end offset must be after the start offset.";
                    return false;
                }

                if (stage.Vulnerability < 0)
                {
                    statusMessage = "Vulnerability weights must be zero or greater.";
                    return false;
                }

                if (stage.FloodToleranceDays <= 0)
                {
                    statusMessage = "Flood tolerance days must be greater than zero.";
                    return false;
                }
            }

            foreach (var point in CustomDamageCurve)
            {
                if (point.DepthFeet < 0)
                {
                    statusMessage = "Depth values must be zero or greater.";
                    return false;
                }

                if (point.DamagePercent < 0)
                {
                    statusMessage = "Damage percentages cannot be negative.";
                    return false;
                }
            }

            var stages = CustomStages
                .OrderBy(stage => stage.StartOffsetDays)
                .Select(stage => new CropGrowthStage(
                    stage.Name,
                    stage.StartOffsetDays,
                    stage.EndOffsetDays,
                    stage.Vulnerability,
                    stage.FloodToleranceDays))
                .ToList();

            var curve = CustomDamageCurve
                .OrderBy(point => point.DepthFeet)
                .Select(point => new DamageCurvePoint(point.DepthFeet, Math.Clamp(point.DamagePercent, 0, 100)))
                .ToList();

            string occupancy = string.IsNullOrWhiteSpace(CustomOccupancyType)
                ? CustomCropName
                : CustomOccupancyType;
            string description = string.IsNullOrWhiteSpace(CustomDescription)
                ? "User defined crop profile configured in the tool."
                : CustomDescription;

            profile = new CropDamageProfile(
                CustomCropName,
                occupancy,
                description,
                CustomValuePerAcre,
                CustomPlantingStartDay,
                CustomPlantingEndDay,
                CustomHarvestEndDay,
                stages,
                curve);

            statusMessage = string.Format(
                CultureInfo.InvariantCulture,
                "Custom crop ready: {0} stages and {1} depth points.",
                stages.Count,
                curve.Count);
            return true;
        }

        private static bool IsValidDay(int day) => day is >= 1 and <= 365;

        public string OccupancyType
        {
            get
            {
                if (IsCustomSelected)
                {
                    return string.IsNullOrWhiteSpace(CustomOccupancyType) ? CustomCropName : CustomOccupancyType;
                }

                return SelectedCrop?.OccupancyType ?? "Agricultural Field";
            }
        }

        public string RegionDescription => SelectedRegion?.Description ?? string.Empty;

        public string CropDescription
        {
            get
            {
                if (IsCustomSelected)
                {
                    return CustomDescription;
                }

                return SelectedCrop?.Description ?? string.Empty;
            }
        }

        public string GrowthWindowSummary
        {
            get
            {
                var resolved = ResolveProfile();
                if (resolved == null)
                {
                    return string.Empty;
                }

                string plantWindow = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} to {1}",
                    FormatDay(resolved.PlantingStartDay),
                    FormatDay(resolved.PlantingEndDay));
                string harvestWindow = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} to {1}",
                    FormatDay(Math.Max(resolved.PlantingEndDay, resolved.HarvestEndDay - 30)),
                    FormatDay(resolved.HarvestEndDay));

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Planting window: {0}. Harvest window: {1}.",
                    plantWindow,
                    harvestWindow);
            }
        }

        public IEnumerable<GrowthStageSummary> GrowthStageSummaries
        {
            get
            {
                var resolved = ResolveProfile();
                if (resolved == null)
                {
                    return Enumerable.Empty<GrowthStageSummary>();
                }

                return resolved.Stages.Select(stage => new GrowthStageSummary
                {
                    Name = stage.Name,
                    CalendarWindow = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} to {1}",
                        FormatDay(stage.StartDay),
                        FormatDay(stage.EndDay)),
                    VulnerabilityText = string.Format(
                        CultureInfo.InvariantCulture,
                        "Exposure weight: {0:P0}",
                        stage.Vulnerability),
                    ToleranceText = string.Format(
                        CultureInfo.InvariantCulture,
                        "Tolerance: {0:F1} days", stage.FloodToleranceDays)
                });
            }
        }

        public double FieldAcreage
        {
            get => _fieldAcreage;
            set
            {
                if (Math.Abs(_fieldAcreage - value) < 0.0001)
                {
                    return;
                }

                _fieldAcreage = value;
                OnPropertyChanged();
            }
        }

        public int SimulationYears
        {
            get => _simulationYears;
            set
            {
                if (_simulationYears == value)
                {
                    return;
                }

                _simulationYears = value;
                OnPropertyChanged();
            }
        }

        public double ProbabilityOfImpact
        {
            get => _probabilityOfImpact;
            private set
            {
                if (Math.Abs(_probabilityOfImpact - value) < 0.0001)
                {
                    return;
                }

                _probabilityOfImpact = value;
                OnPropertyChanged();
            }
        }

        public double AverageVulnerability
        {
            get => _averageVulnerability;
            private set
            {
                if (Math.Abs(_averageVulnerability - value) < 0.0001)
                {
                    return;
                }

                _averageVulnerability = value;
                OnPropertyChanged();
            }
        }

        public double AverageToleranceDays
        {
            get => _averageToleranceDays;
            private set
            {
                if (Math.Abs(_averageToleranceDays - value) < 0.0001)
                {
                    return;
                }

                _averageToleranceDays = value;
                OnPropertyChanged();
            }
        }

        public string ProbabilitySummary
        {
            get => _probabilitySummary;
            private set
            {
                if (_probabilitySummary == value)
                {
                    return;
                }

                _probabilitySummary = value;
                OnPropertyChanged();
            }
        }

        public string MonteCarloNarrative
        {
            get => _monteCarloNarrative;
            private set
            {
                if (_monteCarloNarrative == value)
                {
                    return;
                }

                _monteCarloNarrative = value;
                OnPropertyChanged();
            }
        }

        public string SummaryText
        {
            get => _summaryText;
            private set
            {
                if (_summaryText == value)
                {
                    return;
                }

                _summaryText = value;
                OnPropertyChanged();
            }
        }

        public string MostVulnerableStage
        {
            get => _mostVulnerableStage;
            private set
            {
                if (_mostVulnerableStage == value)
                {
                    return;
                }

                _mostVulnerableStage = value;
                OnPropertyChanged();
            }
        }

        private bool CanCompute() => SelectedRegion != null && SelectedCrop != null && FieldAcreage > 0 && SimulationYears > 0;

        private void Compute()
        {
            var resolved = ResolveProfile();
            if (SelectedRegion == null || resolved == null)
            {
                return;
            }

            var simulation = RunSimulation(resolved, SelectedRegion);
            ProbabilityOfImpact = simulation.ProbabilityOfImpact;
            AverageVulnerability = simulation.EffectiveVulnerability;
            AverageToleranceDays = simulation.AverageToleranceDays;
            MostVulnerableStage = simulation.MostImpactedStage;

            ProbabilitySummary = string.Format(
                CultureInfo.InvariantCulture,
                "Annual chance a flood overlaps the growing season: {0:P1}",
                simulation.ProbabilityOfImpact);
            MonteCarloNarrative = string.Format(
                CultureInfo.InvariantCulture,
                "Simulated {0:N0} seasons with {1:P1} flood likelihood. {2:N0} seasons experienced damaging overlap, most often during {3}.",
                SimulationYears,
                simulation.FloodOccurrenceRate,
                simulation.ImpactYears,
                simulation.MostImpactedStage);
            SummaryText = string.Format(
                CultureInfo.CurrentCulture,
                "{0} valued at {1} per acre across {2:N2} acres.",
                OccupancyType,
                resolved.Profile.ValuePerAcre.ToString("C0", CultureInfo.CurrentCulture),
                FieldAcreage);

            BuildDamageTable(resolved, simulation);
            OnPropertyChanged(nameof(DamageTable));
        }

        private void BuildDamageTable(ResolvedCropProfile resolved, SimulationResult simulation)
        {
            DamageTable.Clear();

            foreach (var duration in AgriculturalDamageLibrary.StandardDurations)
            {
                double durationMultiplier = AgriculturalDamageLibrary.GetDurationMultiplier(duration);
                double toleranceFactor = ComputeToleranceFactor(simulation.AverageToleranceDays, duration);

                foreach (var depth in AgriculturalDamageLibrary.StandardDepths)
                {
                    double baseDamage = resolved.Profile.GetBaseDamage(depth);
                    double expectedPercent = baseDamage * durationMultiplier * toleranceFactor * simulation.EffectiveVulnerability;
                    expectedPercent = Math.Clamp(expectedPercent, 0, 100);
                    double expectedDollars = expectedPercent / 100d * resolved.Profile.ValuePerAcre * FieldAcreage;

                    DamageTable.Add(new DamageTableRow
                    {
                        DepthFeet = depth,
                        DurationDays = duration,
                        DamagePercent = Math.Round(expectedPercent, 2),
                        ExpectedDamageDollars = Math.Round(expectedDollars, 2)
                    });
                }
            }
        }

        private static double ComputeToleranceFactor(double averageToleranceDays, int duration)
        {
            if (averageToleranceDays <= 0)
            {
                return 1.0;
            }

            double ratio = duration / averageToleranceDays;
            if (ratio < 1)
            {
                return Math.Clamp(0.6 + 0.4 * ratio, 0.5, 1.0);
            }

            return Math.Clamp(1.0 + 0.35 * (ratio - 1.0), 1.0, 1.6);
        }

        private SimulationResult RunSimulation(ResolvedCropProfile profile, FloodRegionProfile region)
        {
            var random = new Random(HashCode.Combine(profile.Profile.CropName, region.Name));
            int impactYears = 0;
            int floodYears = 0;
            double vulnerabilitySum = 0;
            double toleranceWeighted = 0;
            double vulnerabilityWeight = 0;
            var stageHitCounts = profile.Stages.ToDictionary(s => s.Name, _ => 0);

            for (int i = 0; i < SimulationYears; i++)
            {
                if (random.NextDouble() > region.AnnualFloodProbability)
                {
                    continue;
                }

                floodYears++;

                int eventDay = (int)Math.Round(SampleTriangular(random, region.FloodSeasonStartDay, region.FloodSeasonPeakDay, region.FloodSeasonEndDay));
                eventDay = Math.Clamp(eventDay, 1, 365);

                if (eventDay < profile.PlantingStartDay || eventDay > profile.HarvestEndDay)
                {
                    continue;
                }

                var stage = profile.Stages.FirstOrDefault(s => eventDay >= s.StartDay && eventDay <= s.EndDay) ?? profile.Stages.Last();
                impactYears++;
                vulnerabilitySum += stage.Vulnerability;
                toleranceWeighted += stage.Vulnerability * stage.FloodToleranceDays;
                vulnerabilityWeight += stage.Vulnerability;
                stageHitCounts[stage.Name]++;
            }

            double probabilityOfImpact = SimulationYears > 0 ? (double)impactYears / SimulationYears : 0;
            double effectiveVulnerability = SimulationYears > 0 ? vulnerabilitySum / SimulationYears : 0;
            double averageTolerance = vulnerabilityWeight > 0 ? toleranceWeighted / vulnerabilityWeight : 0;
            double floodOccurrenceRate = SimulationYears > 0 ? (double)floodYears / SimulationYears : 0;
            string mostImpactedStage = stageHitCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "growing season";

            return new SimulationResult(probabilityOfImpact, effectiveVulnerability, averageTolerance, floodOccurrenceRate, impactYears, mostImpactedStage);
        }

        private static double SampleTriangular(Random random, int min, int mode, int max)
        {
            double u = random.NextDouble();
            double c = (double)(mode - min) / (max - min);
            if (u < c)
            {
                return min + Math.Sqrt(u * (max - min) * (mode - min));
            }

            return max - Math.Sqrt((1 - u) * (max - min) * (max - mode));
        }

        private ResolvedCropProfile? ResolveProfile()
        {
            if (SelectedCrop == null)
            {
                return null;
            }

            int shift = SelectedRegion?.GrowingSeasonShiftDays ?? 0;
            return SelectedCrop.ResolveForRegion(shift);
        }

        private static string FormatDay(int dayOfYear)
        {
            dayOfYear = Math.Clamp(dayOfYear, 1, 365);
            var date = DateOnly.FromDateTime(new DateTime(2021, 1, 1).AddDays(dayOfYear - 1));
            return date.ToString("MMM d", CultureInfo.InvariantCulture);
        }

        private record SimulationResult(
            double ProbabilityOfImpact,
            double EffectiveVulnerability,
            double AverageToleranceDays,
            double FloodOccurrenceRate,
            int ImpactYears,
            string MostImpactedStage);
    }

    public class CustomStageInput : BaseViewModel
    {
        private string _name = string.Empty;
        private int _startOffsetDays;
        private int _endOffsetDays;
        private double _vulnerability = 0.5;
        private double _floodToleranceDays = 2.0;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                OnPropertyChanged();
            }
        }

        public int StartOffsetDays
        {
            get => _startOffsetDays;
            set
            {
                if (_startOffsetDays == value)
                {
                    return;
                }

                _startOffsetDays = value;
                OnPropertyChanged();
            }
        }

        public int EndOffsetDays
        {
            get => _endOffsetDays;
            set
            {
                if (_endOffsetDays == value)
                {
                    return;
                }

                _endOffsetDays = value;
                OnPropertyChanged();
            }
        }

        public double Vulnerability
        {
            get => _vulnerability;
            set
            {
                if (Math.Abs(_vulnerability - value) < 0.0001)
                {
                    return;
                }

                _vulnerability = value;
                OnPropertyChanged();
            }
        }

        public double FloodToleranceDays
        {
            get => _floodToleranceDays;
            set
            {
                if (Math.Abs(_floodToleranceDays - value) < 0.0001)
                {
                    return;
                }

                _floodToleranceDays = value;
                OnPropertyChanged();
            }
        }
    }

    public class CustomDamagePointInput : BaseViewModel
    {
        private double _depthFeet;
        private double _damagePercent;

        public double DepthFeet
        {
            get => _depthFeet;
            set
            {
                if (Math.Abs(_depthFeet - value) < 0.0001)
                {
                    return;
                }

                _depthFeet = value;
                OnPropertyChanged();
            }
        }

        public double DamagePercent
        {
            get => _damagePercent;
            set
            {
                if (Math.Abs(_damagePercent - value) < 0.0001)
                {
                    return;
                }

                _damagePercent = value;
                OnPropertyChanged();
            }
        }
    }

    public class CropOption
    {
        public CropOption(string displayName, CropDamageProfile? profile, bool isCustom = false)
        {
            DisplayName = displayName;
            Profile = profile;
            IsCustom = isCustom;
        }

        public string DisplayName { get; }
        public CropDamageProfile? Profile { get; }
        public bool IsCustom { get; }

        public override string ToString() => DisplayName;
    }

    public class DamageTableRow
    {
        public double DepthFeet { get; set; }
        public int DurationDays { get; set; }
        public double DamagePercent { get; set; }
        public double ExpectedDamageDollars { get; set; }
    }

    public class GrowthStageSummary
    {
        public string Name { get; set; } = string.Empty;
        public string CalendarWindow { get; set; } = string.Empty;
        public string VulnerabilityText { get; set; } = string.Empty;
        public string ToleranceText { get; set; } = string.Empty;
    }
}
