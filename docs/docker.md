# Docker on Vulcan

The Dockerfile builds the web host and its pinned runtime submodule, then runs the published app as the .NET image's unprivileged `app` user. Initialize submodules before building; the Docker build does not clone repositories. Local build outputs, Git metadata, environment files, and private-key/certificate files are excluded from the build context.

The container pins SDK `10.0.401` and overrides `global.json` only inside the build stage. The local SDK pin remains `10.0.112`, whose image tag is unavailable in MCR. The runtime image follows Microsoft’s serviced `aspnet:10.0` tag.

## Start behind the existing nginx proxy

From the provisioning repository on Vulcan (Linux, Docker with Compose v2):

```sh
git submodule update --init --recursive
cp .env.example .env
# Edit .env with the Keycloak server base URL, realm, and client ID.
docker compose up -d --build
docker compose logs -f provisioning
```

Enter the Keycloak client secret in the setup page; it is not an image argument or Compose setting. Missing Keycloak settings leave the app on its deployment-configuration screen.

Compose uses Linux host networking and binds only `127.0.0.1:5180`. This matches Vulcan's host-local nginx upstream pattern. It does not publish the app's HTTP listener to the LAN. The existing proxy must terminate HTTPS and pass WebSocket upgrades for Interactive Server Blazor. Add a location to the intended HTTPS virtual host, using its existing certificate configuration:

```nginx
location / {
    proxy_pass http://127.0.0.1:5180;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_read_timeout 3600s;
}
```

Use a dedicated hostname with the app at `/`, rather than a subpath. Open that hostname over HTTPS. The app consumes forwarded scheme/address headers only from default loopback trusted proxies; it does not trust arbitrary remote proxies. This recipe assumes nginx can reach host loopback (host process or host-network container). A bridge-network proxy needs a separately configured trusted proxy address and network arrangement.

The `protection-keys` volume preserves ASP.NET Core data-protection keys across container replacement. These keys are sensitive and the volume should be restricted to this application. They do not persist Keycloak credentials or bootstrap completion. The current setup pages perform a read-only client/role check; operator SSO and the administrator handoff remain pending. The simulation stays at `/simulation`.

## Build or run without Compose

```sh
docker build -t aetheric-provisioning:local .
docker run -d --name aetheric-provisioning \
  --restart unless-stopped --network host \
  --security-opt no-new-privileges --cap-drop ALL \
  -e ASPNETCORE_URLS=http://127.0.0.1:5180 \
  -e ASPNETCORE_HTTPS_PORT=443 \
  -e BootstrapConnection__Authority=https://sso.example.com \
  -e BootstrapConnection__Realm=root \
  -e BootstrapConnection__ClientId=provisioner \
  -e BootstrapConnection__AdminRole=provisioner-admin \
  -v provisioning-protection-keys:/home/app/.aspnet/DataProtection-Keys \
  aetheric-provisioning:local
```

If Keycloak uses a private CA, the container must trust that CA before connection checks will succeed. Do not disable TLS verification. No CA certificate or live deployment secret is bundled into this image.

For background on ASP.NET Core container HTTPS configuration, see [Microsoft's container hosting guidance](https://learn.microsoft.com/en-us/aspnet/core/security/docker-compose-https?view=aspnetcore-10.0).
