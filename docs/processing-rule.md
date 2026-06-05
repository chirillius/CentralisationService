# Processing Rule

This subtree uses a strict centralized processing model.

## Required rule

All processing goes through `CentralServer`.

That includes:

- motion detection;
- frame comparison;
- archive writing;
- event creation;
- future AI orchestration;
- future defect routing.

## Site-side `Server`

`Server` is intentionally weak:

- capture frame from camera;
- expose frame to `CentralServer`;
- expose technical metadata to `CentralServer`;

It must not become a second analytics server.

## Storage rule

Motion-triggered frames must be saved under:

`CentralServer/videos/<yyyy-MM-dd>/<cameraName>/`

This storage is the source of truth for the new centralized architecture.

## Documentation update rule

After adding or changing platform functionality, update:

`docs/platform-checklist.md`

Required sections to keep current:

- implemented functionality by project: `CentralServer`, `Neuro`, `Server`, `Client`;
- functionality recreated from the original projects;
- remaining work;
- change timeline by date.

If a change affects architecture, access rules, processing ownership, connector binding, model inference, archive behavior, or admin/user flows, record it in the checklist during the same implementation step.
