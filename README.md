## DMS_2025

A modular Document Management System (DMS) built with .NET, featuring a REST API, background worker, web UI, and Dockerized deployment. The solution is organized into multiple projects: <br>
`DMS_2025.DAL`: data access layer (EF Core, repositories) <br>
`DMS_2025.Models`: domain and DTO models <br>
`DMS_2025.REST`: ASP.NET Core Web API (FluentValidation, filters) <br>
`DMS_2025.Services.Worker`: background processing worker (e.g., queues) <br>
`DMS_2025.UI`: frontend application <br>
`DMS_2025.Tests`: automated tests <br>
`nginx/ + default.conf`: reverse proxy configuration <br>
`docker-compose.yml`: multi-service local stack <br>
`Documentation/`: project docs (architecture, notes) <br> <br>


### Features
<li>Clean architecture split across DAL, Models, API, Worker, UI.
<li>Validation pipeline via FluentValidation (registered globally in API filters).
<li>Repository pattern over EF Core DbContext.
<li>Background jobs / queue-ready worker service for async processing.
<li>Containerized services with Docker Compose; optional Nginx reverse proxy.
<li>Testable with a dedicated DMS_2025.Tests project. <br>
  
Tip: The API registers a FluentValidationActionFilter and validators from the assembly so invalid requests return consistent error responses. <br> <br>


### Tech Stack
**Backend:** .NET (ASP.NET Core Web API), Entity Framework Core, FluentValidation <br>
**Worker:** .NET background service (queue-friendly; e.g., RabbitMQ-ready) <br>
**Frontend:** JavaScript-based UI (see DMS_2025.UI) <br>
**Ops:** Docker, Docker Compose, Nginx reverse proxy <br>
**Testing:** NUnit (see DMS_2025.Tests) <br> <br>


### Getting Started
<ol>
  <li><b>Prerequisites</b></li>
    <ul>
      <li>.NET SDK (compatible with solution’s target framework)</li>
      <li>Node.js + npm (for DMS_2025.UI)</li>
      <li>Docker</li>
    </ul>
  <br><li><b>Clone</b></li>
  <pre><code class="language-bash">git clone https://github.com/CaptainKnobel/DMS_2025.git
  cd DMS_2025</code></pre>
  <br><li><b>Configure</b></li>
    <ul>
      <li>API settings: check appsettings.Development.json in DMS_2025.REST for database connection strings, CORS, and any queue endpoints.
      <li>Worker settings: ensure any queue/storage connection variables are set for DMS_2025.Services.Worker.
      <li>UI settings: configure the API base URL and environment files as needed.
      <li>Reverse proxy: adjust nginx/default.conf if you want Nginx in front of the API/UI.
    </ul>
  <br><li><b>Run with Docker</b></li>
  <pre><code class="language-bash">docker compose up --build</code></pre>
  This spins up the API, Worker, UI, and supporting services defined in <code>docker-compose.yml</code>, plus Nginx if included. After startup: <br>
  <ul>
    <li>API should be reachable at the mapped port (or via Nginx).
    <li>Swagger UI (if enabled) will be available at <code>/swagger</code>.
    <li>UI will be reachable at its mapped port (or via Nginx).
  </ul>
</ol>

### Development Notes
<li>Migrations: Use EF Core tools (dotnet ef migrations add ..., dotnet ef database update). Ensure the connection string matches your local DB (or containerized DB from Compose). <br>
<li>Validation: Add new validators in the REST project; they’ll be discovered via assembly scanning.<br>
<li>Repositories: Place new repository interfaces in DAL.Repositories.Interfaces and implementations in DAL.Repositories.EfCore.<br>
<li>Queues: If using RabbitMQ (or similar), add configuration to both the REST and Worker projects and expose in docker-compose.yml.<br> <br>

### Deployment
<li>Containerize the API/Worker/UI and deploy behind Nginx (or your ingress of choice).
<li>Externalize configuration via environment variables / secrets providers.
<li>Enable health checks and set up CI/CD as needed.
