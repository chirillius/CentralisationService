# Remote Workflow

This document describes how this repository is deployed and maintained on the remote test machines.

## Repository Layout

The active Git repository is:

```text
/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralisationService
```

Remote machines should use the same project layout where possible:

```text
E:\RemoteDev\REPO\CentralisationService\
  CentralServer\CentralServer
  Server\Server
  Neuro\Neuro
  Client
  Entities
```

## CentralServer Machine

SSH:

```bash
ssh kvantron@10.10.69.56
```

Project path:

```text
E:\RemoteDev\REPO\CentralisationService\CentralServer\CentralServer
```

Run command:

```powershell
dotnet run
```

Expected role:

- runs the central API;
- owns companies, users, invitations, store/site bindings, zones, detection profiles, archive, incidents, and AI orchestration;
- calls site-side `Server` instances;
- calls centralized `Neuro`.

## Server Machine

SSH:

```bash
ssh -p 5122 Admin@95.83.145.30
```

Project path:

```text
E:\RemoteDev\
```

GitHub SSH is not configured on this machine.

Current workflow:

- do not run `git pull` on this machine;
- copy the `Server` project manually;
- build and run from the copied `Server\Server` folder;
- keep site-side camera configuration on this machine.

Expected role:

- runs only the weak site-side connector;
- stores local camera configuration;
- exposes connector info, camera list, and current frames;
- does not run analytics, archive writing, incidents, or Neuro calls.

## Working Rules

- `CentralServer` changes should be committed and pushed through Git.
- `Server` changes for the remote Server machine may be copied manually until Git is configured there.
- Never commit model files from `Neuro/Neuro/DNNModels`.
- Never commit `bin`, `obj`, `node_modules`, `dist`, generated archives, evidence images, or local runtime access JSON.
- Do not commit real RTSP passwords, API tokens, private certificates, or local `.env` files.
- After functional platform changes, update `docs/platform-checklist.md`.

## Useful Commands

Build CentralServer locally:

```bash
dotnet build CentralServer/CentralServer/CentralServer.csproj
```

Build Server locally:

```bash
dotnet build Server/Server/Server.csproj
```

Build Client locally:

```bash
cd Client
npm run build
```

Run CentralServer remotely:

```bash
ssh kvantron@10.10.69.56 "cd /d E:\RemoteDev\REPO\CentralisationService\CentralServer\CentralServer && dotnet run"
```

Run Server remotely after manual copy:

```bash
ssh -p 5122 Admin@95.83.145.30 "cd /d E:\RemoteDev\Server\Server && dotnet run"
```
