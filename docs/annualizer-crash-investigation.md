# Cost Annualization Crash Review

## Goal
Review the Cost Annualization tab for crash risks when the view is opened or bindings evaluate.

## Findings
- **Eager construction and sample data mutation.** `AnnualizerViewModel` is registered as a singleton and immediately seeds example data in its constructor. That constructor mutates multiple observable collections and triggers `Compute()` calls before the view is even displayed. Any exception thrown here would bubble during tab creation, which aligns with a crash on navigation. The constructor currently wraps no try/catch around that bootstrapping logic. 【F:App.xaml.cs†L16-L34】【F:ViewModels/AnnualizerViewModel.cs†L81-L128】
- **Suppressing useful exception details.** `Compute()` catches *all* exceptions and replaces results with `double.NaN` plus a generic message. If a data issue occurs while the view is materializing, the original exception is lost, making crashes harder to diagnose and potentially masking faults until a binding retries. 【F:ViewModels/AnnualizerViewModel.cs†L396-L408】
- **IDC schedule uses ambiguous "Year" field for month offsets.** The IDC grid binds its "Month" column to the `FutureCostEntry.Year` property and feeds that value directly into IDC month calculations. Entering a calendar year (e.g., 2025) instead of a month produces huge offsets that then get clamped, which can lead to unexpected math and possibly overflow if a large value slips through before clamping. The label mismatch makes user error more likely when the tab loads or sample data is cleared. 【F:Views/AnnualizerView.xaml†L457-L517】【F:ViewModels/AnnualizerViewModel.cs†L429-L474】【F:Models/FutureCostEntry.cs†L5-L36】
- **Lack of validation on text bindings.** The financial inputs bind directly to numeric properties without validation rules or `ExceptionValidationRule`. Invalid text (e.g., pasted currency symbols or non-numeric characters) will raise binding exceptions during tab activation, which WPF treats as errors. With converters only attached to a few fields, the remaining numeric bindings are unprotected and could throw as soon as the tab becomes active. 【F:Views/AnnualizerView.xaml†L245-L384】【F:Converters/CurrencyConverter.cs†L7-L65】
- **Scenario comparison bindings allow editing without guards.** The scenario table binds multiple numeric columns in TwoWay mode without format or validation. Mistyped values here can trigger binding exceptions during editing, and since the command handlers reuse the shared computation pipeline, a bad value could cause the same crash path as the main inputs. 【F:Views/AnnualizerView.xaml†L696-L738】【F:ViewModels/AnnualizerViewModel.cs†L489-L523】

## Next steps
- Add defensive try/catch (with logging) around the sample-data bootstrap in `AnnualizerViewModel` so tab activation cannot surface unhandled exceptions.
- Introduce validation rules or tolerant converters on all numeric TextBoxes to prevent format exceptions on navigation.
- Rename or split the IDC "Month" field into a dedicated month index to reduce user-entry risk and simplify clamping logic.
- Capture and log the full exception details inside `Compute()` instead of masking them with `NaN` outputs.
