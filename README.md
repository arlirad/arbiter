# Overview

**Arbiter** is a modular web server and reverse proxy built on .NET 10. It is designed with modern protocols in mind, featuring native support for **HTTP/3 (QUIC)** and automated certificate management through **ACME**.


# Key Features
 - **Modern Protocol Support**: Native **HTTP/3** and **HTTP/1.1**.
 - **Reverse Proxy**: Forward requests with header transformation and TLS termination.
 - **Automatic SSL (ACME)**: Built-in support for ACME protocol to automatically issue and renew TLS certificates.
 - **Static File Hosting**: Serve static content with configurable MIME types.
 - **Middleware Pipeline**: A modular architecture allowing you to chain features like:
   - **CORS**: Easily configure Cross-Origin Resource Sharing.
   - **Proxy**: Reverse proxy for backend routing.
   - **Static**: Static file serving.
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
sites:
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
listenOn: 
 - "0.0.0.0"
 - "::"
quicPorts:
 - 443
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