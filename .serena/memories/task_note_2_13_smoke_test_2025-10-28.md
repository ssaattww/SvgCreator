## 2025-10-28 2.13 Smoke Test Plan
- Goal: Build a temporary integration flow in test code that runs SvgCreationOrchestrator, routes the result through ExportFilter → SizeLimiter → SvgEmitter, and produces per-layer SVG output.
- Scope constraints: keep the implementation strictly within the test project so it does not interfere with upcoming production tasks (3.1/3.3/4.1).
- Output handling: write generated SVG files under `tests/_artifacts/svg-smoke/` so they can be opened in a browser for visual inspection.
- Instrumentation: capture execution time (Stopwatch or similar) and log it with the test output as an early performance indicator.
- Schedule: start immediately after task 2.12 is completed using a RED → GREEN → REFACTOR cycle.