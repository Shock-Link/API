# OpenShock API

OpenShock backend

### API Documentation 
You can access our Open API Doc here:

https://api.openshock.app/swagger

# Configuration

The API can be configured using the following environment variables:
Preferred way is a .env file.

| Variable                            | Required | Default value | Allowed / Example value                                                                                  |
|-------------------------------------|----------|---------------|----------------------------------------------------------------------------------------------------------|
| `OPENSHOCK__DB__CONN`               | x        |               | `Host=postgres-server-host;Port=5432;Database=openshock;Username=openshock;Password=superSecurePassword` |
| `OPENSHOCK__DB__SKIPMIGRATION`      |          | `false`       | `true`, `false`                                                                                          |
| `OPENSHOCK__DB__DEBUG`              |          | `false`       | `true`, `false`                                                                                          |
| `OPENSHOCK__FRONTEND__BASEURL`      | x        |               | `https://my-openshock-instance.net` or `https://shocklink.net`                                           |
| `OPENSHOCK__FRONTEND__SHORTURL`     | x        |               | `https://myoi.net` or `https://shockl.ink`                                                               |
| `OPENSHOCK__FRONTEND__COOKIEDOMAIN` | x        |               | `my-openshock-instance.net`                                                                              |
| `OPENSHOCK__REDIS__CONN`            | x        |               | `redis-server-host:6379`                                                                                 |                                            
| `OPENSHOCK__MAIL__SENDER__EMAIL`    | x        |               | `system@my-openshock-instance.net`                                                                       |
| `OPENSHOCK__MAIL__SENDER__NAME`     | x        |               | `MyOpenShockInstance System`                                                                             |
| `OPENSHOCK__MAIL__TYPE`             | x        |               | `MAILJET`, `SMTP`                                                                                        |
| `OPENSHOCK__TURNSTILE__ENABLE`      | x        |               | `true`, `false`                                                                                          |
| `OPENSHOCK__LCG__FQDN`              | x        |               | `de1-gateway.my-openshock-instance.net` `de1-gateway.shocklink.net`                                      |
| `OPENSHOCK__LCG__COUNTRYCODE`       | x        |               | `DE`                                                                                                     |

Reffer to the [Npgsql Connection String](https://www.npgsql.org/doc/connection-string-parameters.html) documentation page for details about `OPENSHOCK__DB_CONN`.  
Reffer to [StackExchange.Redis Configuration](https://stackexchange.github.io/StackExchange.Redis/Configuration.html) documention page for details about `OPENSHOCK__REDIS__CONN`.

## Turnstile

When Turnstile enable is set to `true`, the following environment variable is required:

| Variable                          | Required | Default value | Allowed / Example value |
|-----------------------------------|----------|---------------|-------------------------|
| `OPENSHOCK__TURNSTILE__SITEKEY`   | x        |               |                         |
| `OPENSHOCK__TURNSTILE__SECRETKEY` | x        |               |                         |  

## EMail

### MAILJET

You need these environment variables to use [Mailjet](https://www.mailjet.com/):

| Variable                                            | Required | Default value | Allowed / Example value |
|-----------------------------------------------------|----------|---------------|-------------------------|
| `OPENSHOCK__MAIL__MAILJET__KEY`                     | x        |               |                         |
| `OPENSHOCK__MAIL__MAILJET__SECRET`                  | x        |               |                         |
| `OPENSHOCK__MAIL__MAILJET__TEMPLATE__PASSWORDRESET` | x        |               |                         |

### SMTP

You need these environment variables to use SMTP:

| Variable                                   | Required | Default value | Allowed / Example value            |
|--------------------------------------------|----------|---------------|------------------------------------|
| `OPENSHOCK__MAIL__SMTP__HOST`              | x        |               | `mail.my-openshock-instance.net`   |
| `OPENSHOCK__MAIL__SMTP__PORT`              |          | `587`         | `587`                              |
| `OPENSHOCK__MAIL__SMTP__USERNAME`          | x        |               | `system@my-openshock-instance.net` |
| `OPENSHOCK__MAIL__SMTP__PASSWORD`          | x        |               | `superSecurePassword`              |
| `OPENSHOCK__MAIL__SMTP__ENABLESSL`         |          | `true`        | `true` or `false`                  |
| `OPENSHOCK__MAIL__SMTP__VERIFYCERTIFICATE` |          | `true`        | `true` or `false`                  |

# Deployment / Self Hosting

The OpenShock stack consists of the following components:

- Postgres as database
- Redis-Stack (with keyspace events KEA)
- The API (container, API)
- One or multiple gateways (container, LCG)
- One or multiple cron daemons (container, CRON)
- [The WebUI](https://github.com/OpenShock/WebUI)

## Requirements

OpenShock instance needs to be under the same domain name to work correctly. This is due to cookie limitations in
browsers.
E.g.
Fontend: https://openshock.app
API: https://api.openshock.app
LCG: https://de1-gateway.openshock.app

## Using Docker (provided docker-compose.yml)

1. Grab the `docker-compose.yml` and `.env` file from the repository
2. Change the values in the `.env` file
3. Adjust traefik to your needs (e.g. add SSL certificates)

Run with `docker compose up -d`

---

You could also bring your own reverse proxy.  
You would need to remove traefik from the `docker-compose.yml` and route the traffic in your reverse proxy.

## Using Kubernetes and Helm
Kubernetes and Helm are very powerful and well liked but they are not beginner friendly. Use docker compose if you want something easy.

Openshock has some dependencies. It's not Openshock's place to tell you how to install them. 
Dependencies are:
- A Postgres database. This is used for storing user data including passwords. The Openshock API needs a connection string.
- A Redis cluster. This is used for storing session data and as a messaging bus. It needs ReJson, RediSearch
 and an extra argument: "--notify-keyspace-events KEA" 
    <details>
    <summary>Example Redis</summary>
    Here is a very basic but not necessarily good deployment of Redis that works.

    ```yaml
    apiVersion: apps/v1
    kind: Deployment
    metadata:
    name: redis
    spec:
    replicas: 1
    selector:
        matchLabels:
        app: redis
    template:
        metadata:
        labels:
            app: redis
        spec:
        containers:
            - name: redis
            image: redis/redis-stack
            env: 
                - name: REDIS_ARGS
                value: '--notify-keyspace-events KEA'
            ports:
                - name: redis
                containerPort: 6379
                protocol: TCP
    ---
    apiVersion: v1
    kind: Service
    metadata:
    name: redis
    spec:
    type: ClusterIP
    ports:
        - port: 6379
        targetPort: redis
        protocol: TCP
        name: redis
    selector:
        app: redis
    ```

    </details>

Steps:
1. Create a Kubernetes Secret containing the database connection string:
    ```sh
    kubectl create secret generic openshock --from-literal=databaseConnection='Host=10.0.0.5;Port=5432;Database=openshock;Username=openshock;Password=password123'
    ```
    Also include any other secrets you may want to use in the deployment. Such as a Redis password and mail credentials. See [values.yaml](./charts/openshock/values.yaml) for more.
1. Create a yaml file for your Helm values. See [values.yaml](./charts/openshock/values.yaml) for details. 
   <details>
   <summary>Example values.yaml</summary>
   
   ```yaml
    appConfig:
      database:
        connectionSecretName: openshock
        connectionSecretKey: databaseConnection
      redis:
        host: redis
      frontend:
        name: MyOpenshock
        baseUrl: https://myopenshock.com
        shortUrl: https://myopenshock.com
        cookieDomain: myopenshock.com
      apiUrl: https://api.myopenshock.com
      liveControllerGateway:
        countryCode: NZ
        fcdn: lcg.myopenshock.com

    api:
      ingress:
        enabled: true
        hosts:
          - host: api.myopenshock.com
            paths:
              - path: /
                pathType: Prefix
        tls:
          - secretName: openshock-api-tls
             hosts:
               - api.myopenshock.com

    liveControllerGateway:
      ingress:
        enabled: true
        hosts:
          - host: lcg.myopenshock.com
            paths:
              - path: /
                pathType: Prefix
        tls:
          - secretName: openshock-lcg-tls
        hosts:
          - lcg.myopenshock.com

    webUi:
      enabled: true
      ingress:
        enabled: true
        hosts:
          - host: myopenshock.com
            paths:
              - path: /
                pathType: Prefix
        tls:
          - secretName: openshock-webui-tls
            hosts:
              - myopenshock.com
   ```
   </details>

1. Create a Helm release:
   ```sh
   helm upgrade --install openshock oci://ghcr.io/OpenShock/openshock -f values.yaml
   ```

## Support development!

You can support the OpenShock Dev Team here: [Sponsor OpenShock](https://github.com/sponsors/OpenShock)
