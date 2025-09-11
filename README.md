# Economic Toolbox Desktop Application

## Overview

This repository contains a Windows Presentation Foundation (WPF) desktop application that provides simple economic analysis tools.
The project is written in C# using the Model-View-ViewModel (MVVM) pattern and targets **.NET 8**.

The application exposes four calculators:

- **Capital Recovery Factor** – Computes the uniform series capital recovery factor for a given interest rate and number of periods.
- **Expected Annual Damage (EAD)** – Integrates a probability-damage curve to estimate expected annual damage.
- **Storage Cost** – Calculates an updated project cost when storage is reallocated.
- **Interest During Construction (IDC)** – Estimates interest charges accrued during project construction.

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
- `StorageCostModel.cs` – Calculates the prorated cost after reallocating storage.
- `InterestDuringConstructionModel.cs` – Computes financing costs accumulated during construction.

### ViewModels

The `ViewModels/` directory holds the MVVM glue between the user interface and the models:

- `BaseViewModel.cs` – Provides property change notification support.
- `RelayCommand.cs` – Lightweight `ICommand` implementation for button bindings.
- `CapitalRecoveryViewModel.cs` – Exposes rate and period inputs and formats the capital recovery result.
- `EadViewModel.cs` – Parses comma‑separated probabilities and damages and returns the expected annual damage.
- `StorageCostViewModel.cs` – Handles inputs related to storage reallocation and displays the updated cost.
- `InterestDuringConstructionViewModel.cs` – Allows optional monthly cost and timing entries to estimate IDC.
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

## Notes

- `bin/` and `obj/` directories are generated during builds and can be cleaned with `dotnet clean`.
- The zipped archive `Econ-Toolbox-cSharp-and-mvvm.zip` mirrors the project content and is not required for building or running the app.
- This repository does not include unit tests. Building the project is the primary verification step.

