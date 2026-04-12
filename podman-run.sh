#!/bin/sh

echo "TR"
podman run --rm textus-redis jhn1,1 tr

echo "TNP"
podman run --rm textus-redis jhn1,1 tnp

echo "UBG"
podman run --rm textus-redis jhn1,1 ubg

echo "KJV"
podman run --rm textus-redis jhn1,1 kjv

echo "ALL"
podman run --rm textus-redis jhn1,1 all
