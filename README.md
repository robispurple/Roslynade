# Roslynade

A modern, interactive CLI tool for multi-file C# static analysis and AI-assisted code reviews powered by **Microsoft Roslyn** and **Microsoft AI Foundry Local** (running local SLMs/LLMs on-device).

---

## Features

- **Local AI Code Reviews**: Uses Microsoft AI Foundry Local with WinML/DirectML hardware acceleration to run lightweight models (such as `qwen3.5-2b`) completely offline on your device—no API keys or external cloud dependencies required.
- **Roslyn AST Pre-Inspection**: Analyzes C# syntax trees using `Microsoft.CodeAnalysis.CSharp` to extract semantic structural information (classes, records, methods) and feed relevant context to the model.
- **Interactive Multi-File Terminal UI**: Built with [Spectre.Console](https://spectreconsole.net/) featuring:
  - Multi-tabbed layout supporting concurrent or sequential file reviews.
  - Live animated spinner and streaming inference tokens in real time.
  - Interactive keyboard navigation and scrolling with auto-scroll toggling.
  - Automatic fallback to sequential progress logs when executed in non-interactive/CI environments.
- **Structured Findings & Health Scoring**:
  - Code health score out of 100.
  - Categorized findings with distinct severity levels: **`ERROR`**, **`WARN`**, and **`SUGG`**.
  - Precise line numbers, problem descriptions, and suggested code fix snippets.
  - Resilient JSON parser capable of extracting structured reviews from raw responses and Markdown fences.
- **Comprehensive Post-Run Reporting**: Summary grid of all analyzed files, overall scores, and dedicated findings tables.

---

## Tech Stack & Architecture

- **Runtime & Language**: .NET 10 (C# 14), targeted for `net10.0-windows10.0.18362.0` (`win-x64`).
- **AI Engine**: [Microsoft AI Foundry Local WinML](https://www.nuget.org/packages/Microsoft.AI.Foundry.Local.WinML) + [Betalgo OpenAI](https://github.com/betalgo/openai).
- **Code Analysis**: [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) (Roslyn).
- **TUI / Rendering**: [Spectre.Console](https://spectreconsole.net/).
- **Testing**: xUnit, Coverlet, Microsoft.NET.Test.Sdk.

```
src/Roslynade/
├── agent/
│   └── FoundryCodeAgent.cs     # Manages Foundry Local lifecycle, model catalog, download, and streaming inference
├── models/
│   ├── CodeReviewModels.cs     # CodeReviewResult and CodeIssue data contracts
│   └── CodeReviewParser.cs     # Resilient JSON parser with Markdown code-block extraction
├── rendering/
│   └── CodeReviewRenderer.cs   # Spectre.Console markup generation, tables, and markup escaping
├── AnalysisSession.cs          # Interactive Live-display session, tab navigation, and fallback runner
├── RoslynAnalyzer.cs           # AST syntax tree parsing and structural summarization
└── Program.cs                  # CLI entry point, argument parsing, and session initialization
```

---

## Prerequisites

- **Windows 10 / 11** (`win-x64`) with DirectX 12 / DirectML capable GPU or NPU.
- **.NET 10.0 SDK** (or later).
- **Microsoft Foundry Local CLI**:
  ```bash
  winget install Microsoft.FoundryLocal
  ```
  The Foundry CLI powers dynamic model polling and listing (`foundry model list`).
- An internet connection when downloading a new model variant for the first time. Subsequent runs execute entirely offline.

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/robispurple/Roslynade.git
cd Roslynade
```

### 2. Build the project

```bash
dotnet build
```

### 3. Run analysis

Pass one or more C# source files, wildcard patterns, or target directories:

```bash
# Analyze specific files (opens interactive Spectre model and hardware selectors)
dotnet run --project src/Roslynade -- File1.cs File2.cs

# Bypass the model selector by specifying a model directly
dotnet run --project src/Roslynade -- ./src --model qwen2.5-coder-14b

# Force GPU or CPU compute hardware explicitly
dotnet run --project src/Roslynade -- ./src --gpu
dotnet run --project src/Roslynade -- ./src --cpu
dotnet run --project src/Roslynade -- ./src --device gpu

# Analyze with custom concurrency (default: 1 for local models, 3 for remote endpoints)
dotnet run --project src/Roslynade -- ./src --concurrency 2
dotnet run --project src/Roslynade -- ./src -c 4

# Remote API Endpoints (e.g. Azure AI, OpenAI, Ollama, LM Studio)
dotnet run --project src/Roslynade -- ./src --endpoint https://models.inference.ai.azure.com --model gpt-4o
dotnet run --project src/Roslynade -- ./src --endpoint http://localhost:11434/v1 --model qwen2.5-coder:14b -c 4
```

When run interactively without `--model` or `--device`, Roslynade presents:
1. A searchable Spectre.Console model selector with downloaded status and hardware metadata.
2. A hardware selection prompt allowing you to choose between **GPU (Hardware Accelerated)** and **CPU (System Processor)**.

### Concurrency Defaults
- **Foundry Local:** Defaults to **1** concurrent analysis. Local LLM generation is memory-bandwidth bound, and running files sequentially prevents VRAM starvation and KV-cache bloat on consumer GPUs/CPUs.
- **Remote API Endpoints:** Defaults to **3** concurrent analyses. Remote cloud/server backends handle batching efficiently and network I/O is parallelized across files.
- **Manual Override:** Specify `--concurrency <N>` or `-c <N>` to set any desired level.

---

## Interactive Navigation Controls

When running in an interactive terminal, the following keyboard controls are supported:

| Key | Action |
| --- | --- |
| `Tab` / `Right Arrow` | Switch to next file tab |
| `Left Arrow` | Switch to previous file tab |
| `1` – `9` | Jump directly to file tab 1 through 9 |
| `Up Arrow` / `Down Arrow` | Scroll line-by-line |
| `Page Up` / `Page Down` | Scroll page-by-page |
| `Home` | Scroll to the top and disable auto-scrolling |
| `End` | Jump to the bottom and re-enable auto-scrolling |
| `Q` / `Esc` | Cancel analysis or exit the interactive viewer |
| `Enter` | Exit after all analyses are complete |

---

## Running Tests

Unit tests cover JSON extraction, resilient parsing, and Spectre console markup escaping:

```bash
dotnet test
```

---

## License

This project is licensed under the [MIT License](LICENSE).
