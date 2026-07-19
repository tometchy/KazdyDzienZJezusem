## Usage

Install everything and start the stack:

```bash
./scripts/install-all.sh
```

Main setup script:

```bash
./setup.sh
```

Generate the full New Testament:

```bash
./setup.sh --all
```

Generate specific verses only:

```bash
./setup.sh --vers jhn3,16 1co13,4
```

Regenerate only topic markdowns, without Quartz or stack changes:

```bash
./setup.sh --topics-only
```

Cloudflare Tunnel:

1. Create a tunnel in Cloudflare Zero Trust dashboard.
2. Copy the tunnel token into `.env.cloudflare` from `.env.cloudflare.example`.
3. In the tunnel dashboard, publish a hostname for `kazdydzienzjezusem.pl` or a subdomain of it and point the service to `http://kazdy-dzien:8080`.
4. Run:

```bash
./scripts/install-all.sh
```

ToDo:
- Word file on disk as metadata, but in yaml info addtional name for linking original wording
