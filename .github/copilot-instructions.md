# Copilot Instructions for SK.Ext Project

## Project Overview
This repository contains C#/.NET extensions and utilities for Semantic Kernel, organized as a solution with multiple projects:
- `SK.Ext`: Core library with extension methods and utility classes.
- `SK.Ext.Tests`: Unit tests for the core library.
- `SK.Ext.Sample`: Sample usage (if present).
- `coveragereport/`: Generated code coverage reports.

## Naming Conventions
- Use `PascalCase` for class, interface, and method names.
- Use `camelCase` for local variables and parameters.
- Prefix interfaces with `I` (e.g., `ICompletionRuntime`).
- Extension methods should be in static classes named with `Extensions` or `Extentions` (to match existing code).
- File names should match the main class/interface they contain.

## File and Folder Structure
- Place core code in `SK.Ext/`.
- Place tests in `SK.Ext.Tests/`.
- Place sample/demo code in `SK.Ext.Sample/`.
- Do not place source code in the root folder.
- Do not modify files in `coveragereport/` (auto-generated).

## Coding Style
- Use explicit access modifiers (`public`, `private`, etc.).
- Prefer expression-bodied members for simple properties and methods.
- Use XML documentation comments for public classes and methods.
- Use `var` when the type is obvious from the right-hand side; otherwise, use explicit types.
- Use pattern matching and modern C# features where appropriate.
- Keep methods short and focused.

## Testing
- All new features and bug fixes must include or update unit tests in `SK.Ext.Tests/`.
- Use xUnit for tests.
- Ensure tests are independent and do not rely on external state.
- Run tests and ensure they pass before submitting changes.

## Documentation
- Public APIs must have XML doc comments.
- Update or add Markdown documentation as needed for new features.

## General Guidance
- Do not commit build artifacts or coverage reports.
- Follow .NET best practices for error handling and async code.
- Prefer dependency injection for extensibility.
- Keep code readable and maintainable.

---

These instructions help Copilot and Copilot Chat generate code that fits the project's structure, style, and requirements. Suggestions should follow these conventions unless otherwise specified in the prompt.
