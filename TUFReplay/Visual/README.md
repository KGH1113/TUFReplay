# Visual architecture

The visual-preset feature is split by responsibility and follows this dependency direction:

```text
Composition ──> Application ──> Domain / Contracts
      │               │
      ├──> Sources ───┴──> Importing abstractions and services
      └──> Infrastructure ──> Application / Importing abstractions

IPC ──> Composition facade and Domain identifiers
```

- `Domain` owns source and kind identities, supported combinations, versions, and schema constants.
- `Contracts` owns serialized visual bundle and preset DTOs.
- `Application` coordinates use cases through ports in `Application/Abstractions`.
- `Importing` owns source-independent validation, asset collection, JSON handling, bundle construction, and its ports.
- `Sources/<source>` contains one adapter per external mod format. Adapters do not perform API calls or feature composition.
- `Infrastructure` implements API, filesystem, and installation-discovery details.
- `Composition` is the only place that constructs concrete adapters and infrastructure implementations.
- `Ipc` parses transport input and delegates use cases to the composition facade.

To add a source, define its wire identity and supported kinds in `VisualSourceDefinitions`, add one adapter under `Sources`, and register that adapter in `VisualPresetFeature`. Source listing, parsing, compatibility checks, and bundle wire names then use the same canonical definition.
