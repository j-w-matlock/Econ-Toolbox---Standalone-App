# Economic Toolbox Desktop Application

## Overview

This repository contains a Windows Presentation Foundation (WPF) desktop application that provides simple economic analysis tools.
The project is written in C# using the Model-View-ViewModel (MVVM) pattern and targets **.NET 8**.

The application exposes several calculators:

- **Expected Annual Damage (EAD)** – Integrates a probability-damage curve to estimate expected annual damage.
- **Updated Cost of Storage** – Provides a multi-step workflow for storage reallocation including O&M and mitigation.
- **Cost Annualization** – Combines first cost, interest-during-construction schedule, capital recovery, O&M, and benefits to annualize project investments.
- **Water Demand Forecasting** – Projects future demand using simple growth or regression models.
- **Unit Day Value (UDV)** – Estimates recreation benefits from unit day values.

## Repository Layout

```
├── App.xaml / App.xaml.cs               # Application entry point
├── MainWindow.xaml / MainWindow.xaml.cs # Main window with tabs for each tool
├── Models/                              # Pure calculation logic
├── ViewModels/                          # UI-facing logic and commands
├── EconToolbox.Desktop.csproj           # .NET project file
├── EconToolbox.Desktop.sln              # Solution file for IDEs
├── bin/ and obj/                        # Build outputs (generated)
└── Econ-Toolbox-cSharp-and-mvvm.zip     # Archived copy of the project
```

### Models

Each file in `Models/` contains a static class focused solely on computation:

- `CapitalRecoveryModel.cs` – Implements the capital recovery factor formula.
- `EadModel.cs` – Integrates probability and damage arrays to compute EAD.
- `InterestDuringConstructionModel.cs` – Computes financing costs accumulated during construction from a month-by-month schedule.
- `UdvModel.cs` – Calculates unit day value benefits.

### ViewModels

The `ViewModels/` directory holds the MVVM glue between the user interface and the models:

- `BaseViewModel.cs` – Provides property change notification support.
- `RelayCommand.cs` – Lightweight `ICommand` implementation for button bindings.
- `EadViewModel.cs` – Parses comma‑separated probabilities and damages and returns the expected annual damage.
- `UpdatedCostViewModel.cs` – Guides users through storage cost updates, O&M, mitigation, and total annual cost.
- `AnnualizerViewModel.cs` – Manages the cost annualizer workflow, including interest-during-construction schedule entries and annual metrics.
- `WaterDemandViewModel.cs` – Maintains demand scenarios, computes projections, and prepares chart data.
- `UdvViewModel.cs` – Wraps the recreation benefit calculator and selected unit day values.
- `MainViewModel.cs` – Aggregates all sub‑view models for use in `MainWindow`.

### Views

- `MainWindow.xaml` defines the tabbed interface and data bindings for each calculator.
- `App.xaml` wires up `MainWindow` as the startup view.

## Building and Running

1. **Install prerequisites**
   - [.NET SDK 8](https://dotnet.microsoft.com/en-us/download)
   - [Visual Studio Code](https://code.visualstudio.com/) with the C# extension
2. **Open the project**: Launch VS Code and select `File > Open Folder`, choosing this repository.
3. **Restore and build**: In the integrated terminal run:
   ```bash
   dotnet build
   ```
4. **Run the app**: Either press `F5` or execute:
   ```bash
   dotnet run
   ```

## Packaging as a Standalone Windows Executable

The application can be published as a self‑contained `.exe` so that it can run on machines without the .NET runtime installed.

1. Ensure you are on Windows with the .NET SDK installed.
2. From VS Code's terminal, publish the project for your desired runtime (example: 64‑bit Windows):
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true \
       /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
   ```
3. The resulting executable will be created under:
   `bin/Release/net8.0-windows/win-x64/publish/EconToolbox.Desktop.exe`
4. Distribute this single file; it includes the .NET runtime and all dependencies.

## Calculations

### Capital Recovery Factor

The capital recovery factor converts a present amount into a uniform annual series:

\[
\text{CRF} =
\begin{cases}
\dfrac{i(1+i)^n}{(1+i)^n - 1}, & i > 0 \\
\dfrac{1}{n}, & i = 0
\end{cases}
\]

where *i* is the interest rate and *n* the number of periods.

### Expected Annual Damage (EAD)

EAD integrates a probability–damage curve assuming probabilities are ordered from high to low:

\[
\text{EAD} = \sum_{k=0}^{m-2} \tfrac{1}{2}(d_k + d_{k+1})(p_k - p_{k+1})
\]

where \(d_k\) are damages and \(p_k\) the associated exceedance probabilities.

If 100% or 0% probability points are omitted, the tool automatically inserts
a 100% probability with $0 damage and a 0% probability with the maximum damage
value from the column.

### Updated Cost of Storage

Updated cost for reallocated storage is computed as:

\[
\text{Cost} = (C_T - C_S) \times \frac{S_R}{S_T}
\]

with total cost \(C_T\), storage price \(C_S\), reallocated storage \(S_R\), and total usable storage \(S_T\).

### Interest During Construction (IDC)

Monthly financing charges accumulate as:

\[
\text{IDC} = \sum_i c_i r_m t_i
\]

where \(c_i\) is the cost in month *i*, \(r_m = r/12\) is the monthly rate, and \(t_i\) is the remaining months adjusted for timing (beginning, middle, or end).

### Annualizer

The annualizer combines first cost, IDC, and discounted future costs:

\[
I = F + \text{IDC} + \sum_j \frac{C_j}{(1+r)^{y_j}}
\]

\[
\text{Annual Cost} = I \times \text{CRF} + O\_\text{M}
\]

\[
\text{BCR} = \frac{B}{\text{Annual Cost}}
\]

where *F* is first cost, *O\_M* annual O&M, *B* annual benefits, and *CRF* the capital recovery factor.

### Unit Day Value (UDV)

The model interpolates the USACE unit day value table and estimates recreation benefits:

\[
\text{Benefit} = \text{UDV} \times D \times V
\]

with user day value \(\text{UDV}\), user days *D*, and visitation *V*.

### Water Demand Forecasting

Two simple forecasting approaches are provided:

- **Linear regression:** fits \(y = a x + b\) to historical year–demand pairs.
- **Growth rate:** applies a compound annual growth rate based on the first and last observations.

## Known Issues and Limitations

- Simplified formulas are provided for educational use and may not capture all nuances of USACE policy.
- Inputs are minimally validated; users must ensure data, discount rates, and assumptions follow current guidance.
- The repository currently lacks automated tests; manual review and `dotnet build` are the primary verification steps.
- Results are sensitive to the ordering of probability data in the EAD calculator.

## Notes

- `bin/` and `obj/` directories are generated during builds and can be cleaned with `dotnet clean`.
- The zipped archive `Econ-Toolbox-cSharp-and-mvvm.zip` mirrors the project content and is not required for building or running the app.
- This repository does not include unit tests. Building the project is the primary verification step.

