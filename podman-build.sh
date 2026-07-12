#podman build -t kazdy-dzien . --no-cache -f KazdyDzienZJezusem/Dockerfile

IMAGE=ghcr.io/tometchy/kazdydzienzjezusem
podman build -t kazdy-dzien . -f KazdyDzienZJezusem/Dockerfile -t "$IMAGE:latest"
podman push --authfile ~/.config/containers/auth.json "$IMAGE:latest"

podman compose -f compose.yaml pull
podman compose -f compose.yaml up -d
