# Endangered AR Chat Proxy

This small local server keeps the provider key off the iPhone and Unity client.

## Start

Run from the repository root:

```bash
cp server/.env.example .env.local
# Set MOONSHOT_API_KEY locally. Never commit .env.local.
python3 server/dev_server.py
```

Verify the server:

```bash
curl http://127.0.0.1:8000/health
```

The server listens on `0.0.0.0:8000`. Unity Editor can use `http://127.0.0.1:8000`. A phone must use the development machine's reachable LAN address, and both devices must be on the same local network. For production, deploy the proxy behind HTTPS instead of depending on a development-machine address.

Without a provider key, or when the provider is unavailable, `/chat` returns a short character-specific local fallback so the demo remains usable.
