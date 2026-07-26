# FarmAssist

FarmAssist is a cross-platform farm field monitoring app built with .NET MAUI and the ArcGIS Maps SDK for .NET. It combines field mapping, AI-assisted crop incident reporting, spatial risk analysis, and targeted neighbor-farm alerting.

A crop diagnosis becomes the input to a spatial model that helps identify which fields and farms could be at risk next.

## Features

### Interactive farm mapping

- Draw field boundaries using polygon or freehand tools.
- Calculate acreage with ArcGIS geodetic geometry operations.
- Save fields and geometry locally between sessions.
- View parcels, incidents, neighbor farms, threat zones, and spread forecasts as map overlays.

### AI-assisted incident reporting

- Take a photo, choose an existing photo, or describe symptoms in plain language.
- Classify the problem, pest, severity, affected crop, and recommended treatment.
- Review and edit the AI-drafted report before saving.
- Use the device current location or select an incident location on the map.
- Store the report time, location, severity, crop, notes, and alert settings.

### Spatial risk and spread modeling

- Create severity-weighted geodesic buffers around incidents.
- Intersect threat zones with parcels to highlight at-risk fields.
- Model disease spread and toggle the projected outbreak layer.
- Zoom from parcel and incident records to their map locations.

### Neighbor-farm alerts

- Find registered farms within a configurable alert radius.
- Filter recipients by crop susceptibility and pest host range.
- Show which neighbors would be alerted and why they matched.

> Alert recipients are computed locally. The demo does not send real push notifications.

### Records and demo data

- Browse, edit, and remove logged incidents.
- Generate an AI-assisted summary of incident history.
- Load a packaged demo preset of parcels, incidents, and neighbor farms when no local farm data exists.

## Tech stack

- [.NET 10](https://dotnet.microsoft.com/)
- [.NET MAUI](https://learn.microsoft.com/dotnet/maui/)
- [ArcGIS Maps SDK for .NET](https://developers.arcgis.com/net/) 300.0.0
- Azure OpenAI gpt-4o for image and text analysis
- Local JSON persistence for farm geometry, incidents, and reports

## Supported platforms

- Android 9.0 (API 28) or later
- Windows 10, version 2004 (10.0.19041.0) or later
- iOS 17.0 or later (build requires macOS)
- Mac Catalyst 17.0 or later (build requires macOS)

## ArcGIS capabilities used

| Capability | API |
|---|---|
| Imagery basemap | `Map` and `BasemapStyle.ArcGISImagery` |
| Boundary editing | `GeometryEditor`, `VertexTool`, and `FreehandTool` |
| Acreage | `GeometryEngine.AreaGeodetic` |
| Threat zones | `GeometryEngine.BufferGeodetic` and `Union` |
| Parcel risk | `GeometryEngine.Intersects` and `DistanceGeodetic` |
| Map interaction | `IdentifyGraphicsOverlayAsync` |
| Rendering | Fill, line, marker, text, and raster symbols across graphics overlays |
