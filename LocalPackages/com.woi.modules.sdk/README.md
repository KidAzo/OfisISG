# WOI Modules SDK

This local package establishes the universal contract between the WOI PC Hub and the dynamically downloaded execution modules. It specifically isolates serialization mappings, enumerations, and runtime definitions to strictly guarantee neither the Hub nor the internal addressable Modules fall out of version sync.

## Structure
* **Contracts**: Interface bindings module entrypoints must cleanly satisfy.
* **Data**: Plain structural footprint representing identical representations across boundaries.
* **Utils**: Scalable diagnostic bindings natively shared.
