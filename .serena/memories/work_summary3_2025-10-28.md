## 2025-10-28 SvgEmitter implementation
- Finished tasks 2.9/2.9.1-2.9.4 for svg-creator spec.
- Added SvgEmitter with depth-ordered group output, path formatting, and hole handling plus SvgEmitterOptions (src/SvgCreator.Core/Svg/SvgEmitter.cs).
- Added SvgEmitterTests covering root attributes, group ordering, path formatting, and hole fill-rule (tests/SvgCreator.Core.Tests/Output/SvgEmitterTests.cs).
- `dotnet test` passing after changes; commit fd1608f contains the work.