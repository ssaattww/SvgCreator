## 2025-10-28 Debug sink integration
- Completed tasks 2.11/2.11.1-2.11.4 for svg-creator spec.
- Documented debug output layout and options in design.md; implemented DebugSinkFactory to choose File/Null sinks based on SvgCreatorRunOptions plus relative path resolution and stage filters (src/SvgCreator.Core/Diagnostics/DebugSinkFactory.cs).
- Enhanced FileDebugSink to honor --debug-keep-temp by cleaning assets directories and added tests covering stage filtering, cleanup, and factory behaviour (tests/SvgCreator.Core.Tests/Diagnostics/*DebugSink*.cs).
- Updated tasks checklist accordingly and all tests pass via `dotnet test`; commit ea4c7ea captures the work.