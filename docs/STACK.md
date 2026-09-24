# Technology Stack Inventory

> Updated Codebase Mapping — 2026-08-07

## Core Framework & Runtime

| Component | Technology / Library | Version | Purpose |
|-----------|----------------------|---------|---------|
| **Target Framework** | .NET | `net9.0` (C# 13) | Web Application Runtime |
| **Web Framework** | ASP.NET Core MVC | 9.0 | Model-View-Controller Web Application |
| **Database Provider** | SQL Server / `Microsoft.Data.SqlClient` | `6.1.4` | ADO.NET Relational Database Access |
| **Micro-ORM** | Dapper | `2.1.66` | Lightweight Object Mapping |
| **Authentication** | ASP.NET Core Cookie Auth | 9.0 | User Identity & Claims Management |
| **Background Processing** | `IHostedService` | 9.0 | Hosted Daily Notifications Service |

---

## Frontend & UI Libraries

| Component | Library / Asset | Source | Purpose |
|-----------|-----------------|--------|---------|
| **CSS Framework** | Bootstrap | 5.3+ | Responsive Grid & Component Styling |
| **Icons** | Bootstrap Icons | 1.11+ | Dashboard & Action UI Icons |
| **Scripting** | jQuery | 3.6+ | DOM Manipulation & Ajax Handlers |
| **Data Tables** | DataTables.net | 1.13+ | Interactive Server/Client-side Table Pagination |

---

## Project Structure & Dependencies

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Dapper" Version="2.1.66" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.4" />
    <PackageReference Include="System.Data.SqlClient" Version="4.8.6" />
  </ItemGroup>
</Project>
```

---

## Environment & Server Configuration

| Key / Variable | Location | Purpose |
|----------------|----------|---------|
| `ConnectionStrings:DefaultConnection` | `appsettings.json` | SQL Server Connection String |
| `WebRootPath` (`wwwroot/uploads`) | `Program.cs` | Serving Judgment, Petition, and Attachment PDFs |
| `ContentRootPath` (`uploads`) | `Program.cs` | Physical File Provider static fallback route |
| `SessionKeys:DivisionID`, `UserID` | Session Middleware | Scoping case access per user division |
