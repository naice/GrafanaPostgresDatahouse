#! /bin/bash -e
docker compose down 
docker image remove emmuss/gpd -f
docker compose up -d