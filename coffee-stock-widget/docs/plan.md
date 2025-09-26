# AI Summarization Implementation Plan

## Goal
Integrate a lightweight LLM summarization pipeline into the coffee stock widget so new coffees receive concise AI-generated tasting summaries while the UI can toggle between legacy and AI-enhanced views.

## Next Steps
- **Integrate lightweight runtime**
  - Wire up the chosen local LLM runtime (Ollama phi-family) through a dedicated service in `CoffeeStockWidget.Infrastructure`.
  - Implement prompt construction that leverages existing tasting note extraction and profile metadata.
  - Add hash/version checks so only coffees with changed source data are reprocessed.

- **Background processing pipeline**
  - Extend `MainWindow.RunCurrentAsync()` and related workflows to enqueue new coffees for AI summarization when enabled.
  - Respect `AiMaxSummariesPerRun` to cap workload and run inference sequentially to minimize resource usage.
  - Cache summaries back onto each `CoffeeItem` via `_store.UpsertItemsAsync()` and persist timestamps/model info.

- **Settings and toggle UI**
  - Update `SettingsWindow.xaml` / `.cs` to expose `AiSummarizationEnabled`, `AiModel`, endpoint and temperature values.
  - Bind toggle state into `MainWindow` so UI refreshes use AI summaries when available; fall back to legacy notes when disabled.

- **Presentation & UX updates**
  - Surface AI summaries and structured fields (producer/origin/process/notes list) inside `MainWindow` list items and `CoffeeDetailsWindow`.
  - Indicate AI-generated content with badges/tooltips and handle missing data gracefully.

- **Workflow safeguards**
  - Ensure disabling AI leaves existing summaries untouched but stops new processing.
  - Skip re-summarization for items with matching `AiSummaryHash` unless forced refresh is triggered.
  - Add logging/telemetry hooks for monitoring inference errors without blocking scraping.

- **Validation**
  - Create unit/integration tests covering toggle behavior, persistence of summaries, and inference scheduling.
  - Manually verify both legacy and AI-enhanced flows across enabled roasters.
