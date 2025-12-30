# Economic Toolbox Desktop Application

## Overview

The Economic Toolbox is a Windows Presentation Foundation (WPF) desktop application that streamlines common U.S. Army Corps of Engineers planning workflows. The app targets **.NET 8**, follows the Model-View-ViewModel (MVVM) pattern via [CommunityToolkit.Mvvm](https://learn.microsoft.com/windows/communitytoolkit/mvvm/introduction), and boots with a host-based dependency injection container powered by `Microsoft.Extensions.Hosting`.

> 💡 **In-app documentation:** The full contents of this README are rendered directly inside the Economic Toolbox under the **Project README** tab so you can review setup guidance, module notes, and publishing steps without leaving the application.

### Key capabilities

- **Ten ready-to-use modules** covering expected annual damage, cost annualization, recreation benefits, agricultural depth-damage modelling, water demand forecasting, qualitative mind mapping, Gantt scheduling, and freehand sketching.
- **Consistent tooltips and onboarding cues** so every field explains the assumptions behind the required inputs, including the in-app README tab for on-demand documentation.
- **Export-ready Excel workbooks** with a dashboard landing page, formatted tables, Segoe UI typography, and embedded visualizations for each calculator.
- **Responsive MVVM infrastructure** using observable view models, async commands, and a shared dependency injection container.

## Technology Stack

- **Framework:** .NET 8 WPF desktop application
- **MVVM:** CommunityToolkit.Mvvm for observable objects, relay commands, and source generators
- **Styling:** Centralized theme resources in `Themes/Design.xaml`
- **Excel output:** [ClosedXML](https://closedxml.io/) for professional workbook generation with charts and tables

## Getting Started

1. **Install prerequisites**
   - [.NET SDK 8](https://dotnet.microsoft.com/en-us/download)
   - [Visual Studio Code](https://code.visualstudio.com/) with the C# extension (or Visual Studio 2022)
2. **Open the project**: Launch your IDE and open the `Econ-Toolbox---Standalone-App` folder.
3. **Restore and build** (run commands from the repository root that contains `EconToolbox.Desktop.sln`):
   ```bash
   dotnet build EconToolbox.Desktop.sln
   ```
   The solution includes a `NuGet.config` that adds the MSAGL MyGet feed so the `Microsoft.Msagl.WpfGraphControl` dependency restores correctly; ensure your restore uses this configuration.
   - If the MyGet feed is unavailable, you can point restores to a local MSAGL build by setting an environment variable before restoring:
     ```bash
     set MSAGL_LOCAL_SOURCE=C:\path\to\local\nuget\feed   # Windows (PowerShell: $env:MSAGL_LOCAL_SOURCE=\"...\")
     dotnet restore EconToolbox.Desktop.sln --configfile NuGet.config
     ```
     The property is also honored by Visual Studio restores. The path should be a folder that contains the locally packed `Microsoft.Msagl` and `Microsoft.Msagl.WpfGraphControl` `.nupkg` files you built from the MSAGL source.
4. **Run the app**: Either press `F5` in your IDE or execute:
   ```bash
   dotnet run
   ```

## Module Reference

Each tab inside the toolbox focuses on a specific planning task. The hero banner at the top of the window summarizes expected inputs, outputs, and real-world use cases for the currently selected module.

### Project README
Renders this documentation so onboarding instructions and architectural notes are available offline. External links open in your default browser.

### Expected Annual Damage (EAD)
Integrate exceedance probabilities with damages (and optional stage data) to compute expected annual damages for multiple scenarios. Charts illustrate frequency-damage and stage-damage curves, and the export bundles raw data, summary text, and visuals.

### Agriculture Depth-Damage
Blend regional flood timing, growth-stage resilience, CropScape acreage, and Monte Carlo simulations to estimate agricultural losses. Interactive tables adjust depth-duration points, stage exposure, and crop sensitivity, producing narrative insight and exportable summaries.

### Updated Cost of Storage
Escalate historical joint-use costs, scale O&M and mitigation expenses by storage allocation, and compare capital recovery scenarios. The module produces a full audit trail across cost tabs for documentation.

### Cost Annualization
Combine first cost, interest-during-construction schedules, future costs, O&M, and benefits to compute annualized values and benefit-cost ratios. IDC and future cost registers provide detailed traceability.

### Water Demand Forecasting
Project baseline and alternative demand scenarios using historic records, sector shares, growth adjustments, system improvements, and losses. Comparative charts highlight divergence between scenarios.

### Unit Day Value (UDV)
Calibrate recreational benefits using USACE unit day value tables. Quality points, visitation cadence, and season length feed into annual benefit estimates.

### Mind Map Workspace
Capture qualitative insights during workshops. Drag-and-drop nodes, add notes, and export the resulting hierarchy for meeting summaries or reports.
- Choose from curated icon palettes, electrical-style connectors, and relationship notes to visually reinforce how parents, siblings, and child ideas relate.

### Standard Gantt Planner
Plan tasks, dependencies, and milestones in a familiar tabular layout. The timeline visual automatically reflects task color, duration, percent complete, and milestone diamonds.

### Sketch Pad
Sketch freehand notes, diagrams, or signatures using configurable pen palettes and stroke thickness. Undo and Clear commands help refine drawings before exporting.

## Excel Export Experience

Selecting **Export** creates a single workbook with:

- A **dashboard** summarizing metrics from every module with consistent typography, alternating row styles, comments, and embedded charts.
- Individual worksheets for each calculator:
  - **EAD** inputs and charts, including the stage-damage overlay when stage data is supplied.
  - **Agriculture** narrative summary, stage exposure table, depth-duration damages, CropScape acreage ledger, and simulation insights.
  - **Updated Cost of Storage** registers for legacy cost escalation, RR&R adjustments, and allocation totals.
  - **Annualizer** key metrics, future cost schedule, and a visual comparison of annual benefits versus annual costs.
  - **Water Demand** scenario workbooks with sector allocations, adjustments, and automatically generated line charts.
  - **UDV, Mind Map, Gantt, and Sketch** exports mirroring the in-app state, including formatted tables and embedded imagery for mind-map and drawing outputs.
- Auto-formatted tables, Segoe UI fonts, and modern theming so deliverables are publication-ready without manual cleanup.

## Repository Layout

```
├── App.xaml / App.xaml.cs               # Application entry point and host bootstrapping
├── GlobalUsings.cs                      # Shared MVVM command aliases and toolkit imports
├── MainWindow.xaml / MainWindow.xaml.cs # Shell view composed through DI
├── Models/                              # Calculation logic shared across modules
├── Services/                            # Excel export implementation and support services
├── ViewModels/                          # UI-facing logic and commands for each module
├── Views/                               # XAML user interfaces
├── Themes/                              # Shared resources for colors, typography, spacing
├── EconToolbox.Desktop.csproj           # .NET project file
└── EconToolbox.Desktop.sln              # Solution file for IDEs
```

## Architecture Notes

- **Composition through DI:** View models, services, and the main window resolve through the container configured in `App.xaml.cs`.
- **Async-first commands:** Heavy operations (like exports) surface as `AsyncRelayCommand` instances to keep the UI responsive.
- **Theming resources:** Colors, fonts, and spacing tokens live in `Themes/Design.xaml`. Reference these semantic resources instead of hard-coded values.
- **Validation-first inputs:** Extend `BaseViewModel` or use `ObservableValidator` when adding modules so input validation errors surface inline.

## Publishing a Standalone Executable

Create a self-contained `.exe` to distribute without requiring a local .NET install:

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
    /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

The packaged executable is emitted to `bin/Release/net8.0-windows/win-x64/publish/EconToolbox.Desktop.exe`.

## Known Issues and Limitations

- CropScape raster import relies on GeoTIFFs with legends that match the bundled templates.
- Mind map canvas navigation is optimized for mouse input; touchpad gestures are limited to scroll and zoom.
