# Deployment Notes (Draft)

## Runtime topology

- `sign-manager` container: ASP.NET Core Web + Worker + Apple client + zsign
- `anisette` container: internal network only

## Data volumes

- `/data`
- `/signing-state`

## Hardening targets

- Non-root user
- Read-only root file system
- `no-new-privileges`
- `cap_drop: [ALL]`
- no `docker.sock`
