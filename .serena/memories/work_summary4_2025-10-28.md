## 2025-10-28 ExportFilter & SizeLimiter
- Completed tasks 2.10/2.10.1-2.10.4 for svg-creator spec.
- Added ExportFilter to select target layers and maintain depth order, plus LayerExportItem metadata (src/SvgCreator.Core/Svg/ExportFilter.cs).
- Added SizeLimiter and LayerExportDocument to enforce byte limits by reducing coordinate precision (src/SvgCreator.Core/Svg/SizeLimiter.cs).
- Added unit tests for filtering logic and size limiting behaviour (tests/SvgCreator.Core.Tests/Output/ExportFilterTests.cs, SizeLimiterTests.cs).
- All tests pass via `dotnet test`; commit 90ae07e captures the work.