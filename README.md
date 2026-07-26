# FarmAssist

**An ArcGIS-powered field manager that turns a photo of a sick plant into a map of who's next.**

Built for the Esri intern hackathon with .NET MAUI and the ArcGIS Maps SDK for .NET.

---

## The idea

Plant-ID apps already exist. Parcel mapping already exists — Esri does it better than anyone.

FarmAssist is the join between them:

> **The diagnosis isn't the output. It's the input to a spatial model.**

A farmer photographs damaged crop or describes it in plain language. A vision model returns a diagnosis *and a severity*. That severity becomes a **geodesic distance** — critical blight propagates further than a low-grade aphid colony. Buffer the incident, intersect with mapped field boundaries, and the app answers the question no plant-ID app can:

**Which fields are next, and whose are they?**

---

## Features

### 1. Map My Farm
Draw field boundaries with a vertex tool or freehand (finger-drawing on tablet). Acreage computes live from `GeometryEngine.AreaGeodetic` as you draw. Parcels persist between sessions.

### 2. Report an Incident
Two-step flow:
- **Step 1** — take a photo, upload one, *or* just describe what you're seeing in plain language ("leaves have ragged holes and there's sawdust-looking stuff in the whorl")
- **Step 2** — the AI's diagnosis pre-fills an editable form: problem, category, severity, affected crop, observed symptoms, and a treatment plan. The farmer corrects anything wrong and adds their own notes.

The AI drafts. The farmer decides.

### 3. Threat propagation
Every incident buffers by a severity-derived radius, unions into a combined threat zone, and intersects with mapped parcels. At-risk fields render amber with a distance-graded risk level and a live acres-exposed count.

| Severity | Spread radius |
|---|---|
| CRITICAL | 2000 m |
| HIGH | 1200 m |
| MEDIUM | 600 m |
| LOW | 250 m |

### 4. Neighbour alerts
Registered farms within a configurable radius are notified — but only if they're growing a **susceptible** crop. The filter uses a host-range table (Fall Armyworm hits corn, sorghum, rice and cotton; Soybean Aphid only soybean), falling back to same-crop matching for unknown pests.

The alert radius pre-fills from the AI's severity call and is overridable with a slider. Toggle it off entirely per report.

### 5. Records & AI summary
Every incident logged with severity badges, click-to-zoom, and an AI-generated outbreak overview across the whole history.

---

## Tech stack

- **.NET MAUI** (.NET 10) — Windows + Android
- **ArcGIS Maps SDK for .NET 300.0** — `Esri.ArcGISRuntime.Maui`
- **Azure OpenAI (gpt-4o)** via Esri IST's shared APIM gateway — vision and text diagnosis

### ArcGIS surface used

| Capability | API |
|---|---|
| Imagery basemap | `Map`, `BasemapStyle.ArcGISImagery` |
| Boundary drawing | `GeometryEditor`, `VertexTool`, `FreehandTool` |
| Acreage | `GeometryEngine.AreaGeodetic` |
| Spread radius | `GeometryEngine.BufferGeodetic` |
| Combined threat zone | `GeometryEngine.Union` |
| At-risk detection | `GeometryEngine.Intersects`, `Project` |
| Distance grading | `NearestCoordinate`, `DistanceGeodetic` |
| Feature inspection | `IdentifyGraphicsOverlayAsync` |
| Rendering | Fill / line / marker / text symbols across five overlays |

---

## Setup

### Prerequisites
- Visual Studio 2022 (17.14+) with the **.NET Multi-platform App UI development** workload
- .NET 10 SDK
- An ArcGIS API key with basemap privileges

### 1. ArcGIS credentials

Create `KoiPonders/ArcGISSettings.local.json` (gitignored — you must make your own):

```json
{
  "ArcGISApiKey": "your-arcgis-api-key"
}
```

Set **Build Action = Embedded Resource** in the file's properties. Without this the app throws on startup.

### 2. Azure OpenAI

In `PestClassifier.cs`, set `ChatUrl` and `ApiKey` for your deployment.

**No key?** Set `UseStub = true`. The app runs fully offline against a deterministic catalogue of realistic diagnoses — every feature works, the classification is just canned.

### 3. Run

Select **Windows Machine** and F5. Draw a few fields, report an incident, watch the threat zone bloom.

---

## Architecture
MainPage.xaml(.cs) Map, drawing tools, overlays, panels
MapViewModel.cs Map + initial viewpoint

IncidentReportPage.xaml Two-step AI-assisted report flow
PestClassifier.cs Azure OpenAI vision + text diagnosis, dashboard summary
RiskEngine.cs Buffer / union / intersect propagation model
AlertService.cs Distance + crop-susceptibility recipient targeting

Incident.cs Diagnosis record
Parcel.cs Field boundary
NeighborFarm.cs Registered neighbouring farm
FarmStore.cs JSON persistence (Esri geometry JSON round-trip)

Five graphics overlays, drawn bottom to top: parcels → threat zone → at-risk fields → neighbour farms → incident pins.

---

## Honest limitations

Worth stating plainly:

- **This is not Esri GeoAI.** It's an LLM vision call, not `arcgis.learn` or a trained imagery model. "AI-assisted diagnosis" is the accurate description.
- **The spatial model is proximity-based.** Buffer-and-intersect with severity-weighted radii — real, defensible, and deliberately simple. It doesn't model wind, terrain, or vector biology.
- **Alerts are computed, not delivered.** Without a shared backend, FarmAssist determines *who would be notified and why*. In deployment these fire as push notifications; the targeting logic is what's implemented here.
- **Persistence is local JSON.** A hosted feature layer on ArcGIS Online would give real multi-farm sharing, offline sync, and editing for free. That's the first thing to build next.

## Next steps

1. Hosted feature layers on ArcGIS Online — real cross-device sharing
2. Wind direction and terrain weighting in the propagation model
3. ArcGIS Dashboards integration for the analytics view
4. Offline map areas for genuinely disconnected fieldwork
