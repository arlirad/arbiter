# Overview

**Arbiter** is a modular web server and reverse proxy built on .NET 10. It is designed with modern protocols in mind, featuring native support for **HTTP/3 (QUIC)** and automated certificate management through **ACME**.


# Key Features
 - **Modern Protocol Support**: Native **HTTP/3** and **HTTP/1.1**.
 - **Transport Layer**: Centralized transport management supporting **TCP**, **QUIC**, and **Unix domain sockets** — independently configurable and hot-reloadable.
 - **Reverse Proxy**: Forward requests with header transformation and TLS termination.
 - **Automatic SSL (ACME)**: Built-in support for ACME protocol to automatically issue and renew TLS certificates.
 - **Static File Hosting**: Serve static content with configurable MIME types.
 - **Middleware Pipeline**: A modular architecture allowing you to chain features like:
    - **CORS**: Easily configure Cross-Origin Resource Sharing.
    - **Proxy**: Reverse proxy for backend routing.
    - **Rewrite**: Request path rewriting using RegEx pattern matching.
    - **Static**: Static file serving.
 - **Configurable Response Headers**: Automatically add `Server`, `Date`, `X-Request-Id`, and `Strict-Transport-Security` headers via YAML config.
 - **HTTP Alt-Svc**: Generic `AltSvcService` for advertising alternative protocols (e.g., HTTP/3) via the `Alt-Svc` response header.
 - **YAML Configuration**: Human-readable configuration for sites, middleware, workers, and bindings.


# Project structure
The project follows a clean, layered architecture:
 - `Arbiter.Transport.*`: Handling of the underlying network protocols.
 - `Arbiter.Protocol.*`: Implementations of protocols, most notably:
   - `Arbiter.Protocol.Http3`: Handling of HTTP/3 connections.
   - `Arbiter.Protocol.QPack`: Implementation of **RFC 9204** necessary for HTTP/3 connections.
 - `Arbiter.Infrastructure.*`: Core infrastructure components including ACME, CORS, and Proxy middleware alongside the ACME worker.
 - `Arbiter.Application`: The central server logic and orchestrator.


# Configuration example

Arbiter defaults to looking for `arbiter.yaml` under `/etc/arbiter/`. For local development, you can use the `--local-config` flag.

Here is a quick look at how you can set up a proxy with ACME and CORS:

```yaml
# /etc/arbiter/arbiter.yaml

# Addresses to listen on (shared across IP transports)
listenOn: 
  - "0.0.0.0"
  - "::"

# Configurable response headers (hot-reloadable)
headers:
  server: true
  date: true
  requestId: true
  strictTransportSecurity:
    maxAge: 31536000
    includeSubDomains: true
    preload: true

# Enable/disable protocols globally
protocols:
  http11: true
  http2: false
  http3: true

# Transport-specific configuration (hot-reloadable)
transports:
  tcp:
    backlog: 128
    queueSize: 4096
    ports: [80, 443]
  quic:
    backlog: 128
    queueSize: 4096
    ports: [443]
    announce:
      maxAge: 86400
    maxInboundBiStreams: 1024
  # unix:
  #   backlog: 128
  #   queueSize: 4096
  #   paths: ["/tmp/arbiter/thighhigh"]

sites:
  main-site:
    middleware:
      - name: static
        config:
          root: /var/www/main
          default_files: [index.html]
  example-app:
    bindings:
      - http://api.example.com
      - https://api.example.com
    middleware:
      - name: acme
      - name: cors
        config:
          allowOrigin: ["https://example.com"]
          allowMethods: [GET, POST, OPTIONS]
      - name: proxy
        config:
          target: http://localhost:5000
    workers:
      - name: acme
        config:
          accountName: admin@example.com
          acmeDirectoryUrl: https://acme-v02.api.letsencrypt.org/directory
          tosAccepted: false # Must be set to true to indicate agreement with the CA's Terms of Service
```


# Getting started

1. Prerequisites: .NET 10 SDK
2. Clone and Build:
```bash
git clone https://github.com/arlirad/arbiter
cd arbiter
dotnet build
```
3. Run:
```bash
# Uses /etc/arbiter/arbiter.yaml
dotnet run --project src/Arbiter

# Uses local ./cfg/arbiter.yaml
dotnet run --project src/Arbiter -- --local-config
```


# Contributing

Any contributions are welcome!
