#!/usr/bin/env python3

import argparse
import asyncio
import json
import statistics
import time
import sys
from dataclasses import dataclass
import ssl

try:
    import aiohttp

    AIOHTTP_AVAILABLE = True
except ImportError:
    AIOHTTP_AVAILABLE = False


@dataclass
class RequestResult:
    status: int
    latency_ms: float
    error: str | None = None


async def request(
    session: aiohttp.ClientSession,
    address: str,
    method: str,
    path: str,
    headers: dict,
    body: str | None,
) -> RequestResult:
    url = address.rstrip("/") + path
    start = time.monotonic()

    try:
        async with session.request(
            method, url, headers=headers, data=body, allow_redirects=False
        ) as response:
            await response.read()
            latency_ms = (time.monotonic() - start) * 1000
            return RequestResult(response.status, latency_ms, None)
    except (aiohttp.ClientError, asyncio.TimeoutError) as e:
        latency_ms = (time.monotonic() - start) * 1000
        return RequestResult(0, latency_ms, str(e))


async def worker(
    session: aiohttp.ClientSession,
    address: str,
    semaphore: asyncio.Semaphore,
    args: argparse.Namespace,
    results: list[RequestResult],
):
    try:
        result = await request(
            session, address, args.method, args.path, args.headers_dict, args.body
        )
        results.append(result)
    except Exception as e:
        results.append(RequestResult(0, 0, str(e)))
    finally:
        semaphore.release()


async def run_stress(args: argparse.Namespace):
    if not AIOHTTP_AVAILABLE:
        print(
            "HTTP/1.1 requires aiohttp. Install with: pip install aiohttp",
            file=sys.stderr,
        )
        sys.exit(1)

    ssl_context = ssl.create_default_context()
    if args.insecure:
        ssl_context.check_hostname = False
        ssl_context.verify_mode = ssl.CERT_NONE

    connector_ssl = ssl_context if args.address.startswith("https://") else False
    connector = aiohttp.TCPConnector(limit=args.connections, ssl=connector_ssl)
    timeout = aiohttp.ClientTimeout(total=30)

    async with aiohttp.ClientSession(connector=connector, timeout=timeout) as session:
        results: list[RequestResult] = []
        semaphore = asyncio.Semaphore(args.connections)
        interval = 1.0 / args.rps
        end_time = time.monotonic() + args.duration

        start = time.monotonic()

        tasks = []
        request_count = 0
        target_total = int(args.rps * args.duration)

        try:
            for i in range(target_total):
                if time.monotonic() >= end_time:
                    break

                await semaphore.acquire()

                task = asyncio.create_task(
                    worker(session, args.address, semaphore, args, results)
                )
                tasks.append(task)
                request_count += 1

                next_target = start + ((i + 1) * interval)
                sleep_time = next_target - time.monotonic()
                if sleep_time > 0:
                    await asyncio.sleep(sleep_time)

                if i % 100 == 0:
                    elapsed = time.monotonic() - start
                    current_rps = request_count / elapsed if elapsed > 0 else 0
                    print(
                        f"\rRequests: {request_count} | RPS: {current_rps:.1f}",
                        file=sys.stderr,
                        end="",
                        flush=True,
                    )
        finally:
            print("", file=sys.stderr)

        for task in tasks:
            if not task.done():
                task.cancel()

        await asyncio.gather(*tasks, return_exceptions=True)

        end = time.monotonic()

    return results, start, end


def parse_headers(header_list: list[str] | None) -> dict[str, str]:
    headers = {}
    if header_list:
        for h in header_list:
            if ":" in h:
                key, value = h.split(":", 1)
                headers[key.strip()] = value.strip()
    return headers


def load_body(body_arg: str | None) -> str | None:
    if body_arg is None:
        return None
    if body_arg.startswith("@"):
        try:
            with open(body_arg[1:], "r") as f:
                return f.read()
        except Exception as e:
            print(f"Error reading body file: {e}", file=sys.stderr)
            sys.exit(1)
    return body_arg


def calculate_percentiles(latencies: list[float]) -> dict[str, float]:
    if not latencies:
        return {"p50": 0, "p90": 0, "p99": 0, "max": 0}

    sorted_latencies = sorted(latencies)
    n = len(sorted_latencies)

    p50 = sorted_latencies[int(n * 0.5)] if n > 0 else 0
    p90 = sorted_latencies[int(n * 0.9)] if n > 0 else 0
    p99 = sorted_latencies[int(n * 0.99)] if n > 0 else 0
    max_val = sorted_latencies[-1] if n > 0 else 0

    return {"p50": p50, "p90": p90, "p99": p99, "max": max_val}


def print_results(
    args: argparse.Namespace, results: list[RequestResult], start: float, end: float
):
    total = len(results)
    success = sum(1 for r in results if r.status > 0)
    errors = total - success
    duration = end - start
    achieved_rps = total / duration if duration > 0 else 0

    latencies = [r.latency_ms for r in results if r.status > 0]
    percentiles = calculate_percentiles(latencies)

    status_codes: dict[int, int] = {}
    for r in results:
        status_codes[r.status] = status_codes.get(r.status, 0) + 1

    if args.json:
        output = {
            "target": args.address + args.path,
            "duration_s": round(duration, 1),
            "target_rps": args.rps,
            "achieved_rps": round(achieved_rps, 1),
            "total": total,
            "success": success,
            "errors": errors,
            "latency_ms": {k: round(v, 1) for k, v in percentiles.items()},
            "status_codes": {str(k): v for k, v in status_codes.items()},
        }
        print(json.dumps(output, indent=2))
    else:
        print("Stress Test Results")
        print("===================")
        print(f"Target:     {args.address}{args.path}")
        print(f"Duration:   {duration:.1f}s")
        print(f"Target RPS: {args.rps}")
        print()
        print(f"Requests:   {total} total | {success} success | {errors} errors")
        print(f"Achieved:   {achieved_rps:.1f} rps")
        print()
        print("Latency (ms):")
        print(f"  p50:   {percentiles['p50']:.1f}")
        print(f"  p90:   {percentiles['p90']:.1f}")
        print(f"  p99:   {percentiles['p99']:.1f}")
        print(f"  max:   {percentiles['max']:.1f}")
        print()
        print("Status Codes:")
        for status, count in sorted(status_codes.items()):
            error_str = " (connection errors)" if status == 0 else ""
            print(f"  {status}: {count}{error_str}")


def main():
    parser = argparse.ArgumentParser(description="Stress test for web servers")
    parser.add_argument("address", help="Target URL (http:// or https://)")
    parser.add_argument(
        "--rps", type=int, required=True, help="Target requests per second"
    )
    parser.add_argument("--path", default="/", help="URL path (default: /)")
    parser.add_argument(
        "--insecure", action="store_true", help="Skip TLS certificate verification"
    )
    parser.add_argument(
        "--duration", type=int, default=10, help="Test duration (default: 10)"
    )
    parser.add_argument(
        "--connections",
        type=int,
        default=50,
        help="Max concurrent connections (default: 50)",
    )
    parser.add_argument("--method", default="GET", help="HTTP method (default: GET)")
    parser.add_argument("--body", help="Request body string or @file.json")
    parser.add_argument(
        "--headers", action="append", help="Custom header, repeatable (default: none)"
    )
    parser.add_argument("--json", action="store_true", help="Output results as JSON")

    args = parser.parse_args()

    args.headers_dict = parse_headers(args.headers)
    args.body = load_body(args.body)

    try:
        results, start, end = asyncio.run(run_stress(args))
        print_results(args, results, start, end)
    except KeyboardInterrupt:
        print("\nInterrupted by user", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
