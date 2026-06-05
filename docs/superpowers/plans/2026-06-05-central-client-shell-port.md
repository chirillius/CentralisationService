# Central Client Shell Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the `CentralisationService/Client` app so it visually matches the original web client shell while keeping only the new central-server logic for stores, streaming, and zone settings.

**Architecture:** Port the original client’s app shell and page hierarchy into the new central client, but replace old backend bindings with central-server-aware state and fetchers. Keep unimplemented sections as visual placeholders, and freeze zone markup against a manually refreshed last frame instead of a continuously changing preview.

**Tech Stack:** React 19, Vite, TypeScript, Material UI, React Router

---
