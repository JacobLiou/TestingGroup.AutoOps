# MockDiagTool Sample Plugin

This project is a template showing how to add plugin-based check executors for `MockDiagTool`.

## What it provides

- `PLG_001`: sync executor (`void (DiagnosticItem)`)
- `PLG_002`: async executor (`Task<CheckExecutionOutcome>(DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext?, CancellationToken)`)

## How discovery works

`DiagnosticEngine` scans:

- current app assembly
- `plugins/*.dll` under `AppContext.BaseDirectory`

Any static method with `[CheckExecutor("CHECK_ID")]` and a supported signature is registered automatically.

## Build and copy

This template has an `AfterBuild` target that copies:

- `MockDiagTool.SamplePlugin.dll`
- `MockDiagTool.SamplePlugin.pdb` (if exists)

to:

- `mockTool/bin/<Configuration>/net8.0-windows/plugins`

## Runbook example

Add a step using:

- `checkId`: `PLG_001` or `PLG_002`
- `params` for `PLG_002` can include `{ "note": "hello-plugin" }`

## Notes

- Keep `checkId` globally unique.
- Avoid duplicate `checkId`; engine throws on duplicate registration.
