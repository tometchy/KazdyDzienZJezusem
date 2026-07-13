## Usage

Install everything and start the stack:

```bash
./scripts/install-all.sh
```

Manually rebuild the stack:

```bash
./scripts/rebuild.sh
```

Generate a specific verse and then rebuild:

```bash
./scripts/rebuild.sh jhn3,16
```

Cloudflare Tunnel:

1. Create a tunnel in Cloudflare Zero Trust dashboard.
2. Copy the tunnel token into `.env.cloudflare` from `.env.cloudflare.example`.
3. In the tunnel dashboard, publish a hostname for `kazdydzienzjezusem.pl` or a subdomain of it and point the service to `http://kazdy-dzien:8080`.
4. Run:

```bash
./scripts/install-all.sh
```
