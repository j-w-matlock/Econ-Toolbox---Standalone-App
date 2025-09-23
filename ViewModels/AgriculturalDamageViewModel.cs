using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private CropDamageProfile? _selectedCrop;
        private double _fieldAcreage = 1.0;
        private int _simulationYears = 5000;
        private double _probabilityOfImpact;
        private double _averageVulnerability;
        private double _averageToleranceDays;
        private string _probabilitySummary = string.Empty;
        private string _monteCarloNarrative = string.Empty;
        private string _summaryText = string.Empty;
        private string _mostVulnerableStage = string.Empty;

        public AgriculturalDamageViewModel()
        {
            Regions = new ObservableCollection<FloodRegionProfile>(AgriculturalDamageLibrary.Regions);
            Crops = new ObservableCollection<CropDamageProfile>(AgriculturalDamageLibrary.Crops);
            DamageTable = new ObservableCollection<DamageTableRow>();
            _computeCommand = new RelayCommand(Compute, CanCompute);
            SelectedRegion = Regions.FirstOrDefault();
            SelectedCrop = Crops.FirstOrDefault();
        }

        public ObservableCollection<FloodRegionProfile> Regions { get; }
        public ObservableCollection<CropDamageProfile> Crops { get; }
        public ObservableCollection<DamageTableRow> DamageTable { get; }

        public ICommand ComputeCommand => _computeCommand;

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
            set
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

        public string OccupancyType => SelectedCrop?.OccupancyType ?? "Agricultural Field";

        public string RegionDescription => SelectedRegion?.Description ?? string.Empty;

        public string CropDescription => SelectedCrop?.Description ?? string.Empty;

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
            var date = DateOnly.FromDayOfYear(dayOfYear, 2021);
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
