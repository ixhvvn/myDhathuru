# VPS deployment on the same IP as another site

This project can share one VPS and one public IP with your existing `echoeswebsite` setup.

## Important constraint

You cannot serve two unrelated sites from the same bare IP and the same port `80` without a reverse proxy deciding by hostname or path.

For your layout, the correct split is:

- `http://129.121.79.40` -> keep serving `echoeswebsite`
- `mydhathuru.com` -> redirect to `https://www.mydhathuru.com`
- `www.mydhathuru.com` -> public website
- `app.mydhathuru.com` -> business user portal
- `admin.mydhathuru.com` -> super admin portal

## 1. DNS

Point these records to `129.121.79.40`:

- `@` A -> `129.121.79.40`
- `www` A -> `129.121.79.40`
- `app` A -> `129.121.79.40`
- `admin` A -> `129.121.79.40`

## 2. Production env

Create `.env.production` from `.env.production.example` and set real secrets.

Required production values:

- `FRONTEND_URL=https://app.mydhathuru.com`
- `ADMIN_FRONTEND_URL=https://admin.mydhathuru.com`
- `CORS_ALLOWED_ORIGIN_0=https://www.mydhathuru.com`
- `CORS_ALLOWED_ORIGIN_1=https://app.mydhathuru.com`
- `CORS_ALLOWED_ORIGIN_2=https://admin.mydhathuru.com`

## 3. Run containers

Use the production Compose overlay:

```bash
docker compose \
  --env-file .env.production \
  -f docker-compose.yml \
  -f docker-compose.production.yml \
  up -d --build
```

This matters because the override:

- disables the Docker `reverse-proxy` by default
- runs the API with `ASPNETCORE_ENVIRONMENT=Production`
- adds `restart: unless-stopped` to the stateful/runtime services

The base Compose file already binds PostgreSQL, the API, and the frontend to `127.0.0.1`, so the VPS NGINX instance can own ports `80` and `443` while Docker services stay private.

## 4. NGINX on the VPS

Install these files into the host NGINX config:

- `infra/nginx/production/mydhathuru.vps.bootstrap.conf`
- `infra/nginx/production/mydhathuru.vps.conf`
- `infra/nginx/production/snippets/mydhathuru-proxy-headers.conf`

The configs intentionally do not use `default_server`. Leave your existing `echoeswebsite` site as the default site for direct IP traffic.

Expected upstreams:

- frontend -> `127.0.0.1:4201`
- backend -> `127.0.0.1:8081`

## 5. TLS certificates

First create the ACME webroot and enable the bootstrap HTTP-only site:

```bash
sudo mkdir -p /var/www/certbot
```

Point the live NGINX site at `mydhathuru.vps.bootstrap.conf`, reload NGINX, and confirm the HTTP hostnames respond.

Issue one certificate that covers:

- `www.mydhathuru.com`
- `app.mydhathuru.com`
- `admin.mydhathuru.com`

Then issue the SAN certificate. A typical sequence is:

```bash
sudo certbot certonly --webroot -w /var/www/certbot \
  -d www.mydhathuru.com \
  -d app.mydhathuru.com \
  -d admin.mydhathuru.com
```

After Certbot finishes:

- switch the live NGINX site to `infra/nginx/production/mydhathuru.vps.conf`
- keep the certificate path as `/etc/letsencrypt/live/www.mydhathuru.com/...` for all three server blocks
- run `sudo nginx -t`
- reload NGINX

## 6. Firewall

Allow inbound:

- `80/tcp`
- `443/tcp`

You do not need public inbound access for `4201`, `8081`, or `5434`.

## 7. Validation

After deploy, verify:

- `http://129.121.79.40` still opens `echoeswebsite`
- `http://mydhathuru.com` redirects to `https://www.mydhathuru.com`
- `https://www.mydhathuru.com` opens the public site
- `https://app.mydhathuru.com` redirects `/` to `/login`
- `https://admin.mydhathuru.com` redirects `/` to `/portal-admin/login`
- `https://www.mydhathuru.com/api/health` returns the API health response through NGINX

## Why the backend change matters

The API now processes forwarded headers before HTTPS redirection. Without that, ASP.NET Core can enter a redirect loop when NGINX terminates TLS and proxies plain HTTP to the container.
