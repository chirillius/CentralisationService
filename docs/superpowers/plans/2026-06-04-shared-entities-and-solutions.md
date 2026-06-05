# Shared Entities And Solutions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a dedicated shared entities project for common domain models and add solution files so each .NET subsystem can be opened independently in Rider or Visual Studio.

**Architecture:** Shared contract and domain types move into a new `CentralisationService.Entities` class library referenced by `CentralServer`, `Neuro`, and `Server` when needed. Runtime-only services, options, controllers, and storage logic remain in their original projects. Separate `.sln` files are added for the root workspace and each .NET subsystem.

**Tech Stack:** .NET 10, ASP.NET Core, xUnit, solution/project references

---
