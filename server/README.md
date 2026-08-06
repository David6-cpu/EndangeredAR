# Endangered AR Chat Proxy

This small local server keeps the provider key off the iPhone and Unity client.

## Start

1. Create `/Users/yuanweijie/Documents/animalsAR/.env.local` from `server/.env.example`.
2. Set `MOONSHOT_API_KEY` locally. Never commit `.env.local`.
3. Run from the repository root:

```bash
python3 server/dev_server.py
```

The server listens on `0.0.0.0:8000`. The Unity device configuration must use the Mac LAN IP, for example `http://192.168.2.147:8000`. The Mac and iPhone must be on the same local network.

Without a provider key, or when the provider is unavailable, `/chat` returns a short character-specific local fallback so the demo remains usable.
