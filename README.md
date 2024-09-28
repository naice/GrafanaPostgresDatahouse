# GrafanaPostgresDatahouse
Simple Boilerplate that connects Grafana to an PostgresSQL 
server, and exposes an api server on 3131 that is capable 
of creating tables for time series. See the swagger 
generated api documentation for more details 
http://localhost:3131/swagger.


Use docker compose up to run _(see Install Docker on how to Install docker)_
```shell
docker compose up
```
For an update you just can execute the given update shell script.

```shell
./update.sh
```


### Install Docker
```shell
curl -fsSL https://get.Docker.com -o get-Docker.sh
```
```shell
sudo sh get-Docker.sh
```
```shell
sudo usermod -aG docker $USER
```
```shell
newgrp docker
```
```shell
docker run hello-world
```
