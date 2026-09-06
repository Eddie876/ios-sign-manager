FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore ios-sign-manager.slnx
RUN dotnet publish src/SignManager.Web/SignManager.Web.csproj -c Release -o /app/web
RUN dotnet publish src/SignManager.Worker/SignManager.Worker.csproj -c Release -o /app/worker

FROM ubuntu:22.04 AS zsign-build
ARG ZSIGN_REPO=https://github.com/zhlynn/zsign.git
ARG ZSIGN_COMMIT=3e5f7f5a8b1d9e0c7f2ac5ea273f5f6bbf6ee6bf

RUN apt-get update \
	&& apt-get install -y --no-install-recommends git ca-certificates build-essential clang openssl libssl-dev zlib1g-dev \
	&& rm -rf /var/lib/apt/lists/*

WORKDIR /tmp
RUN git clone ${ZSIGN_REPO} zsign \
	&& cd zsign \
	&& git checkout ${ZSIGN_COMMIT} \
	&& make -j"$(nproc)" \
	&& ZSIGN_BIN="$(find . -type f -name zsign -perm -111 | head -n 1)" \
	&& test -n "${ZSIGN_BIN}" \
	&& install -m 0755 "${ZSIGN_BIN}" /usr/local/bin/zsign

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/*

RUN groupadd --gid 10001 appgroup \
	&& useradd --uid 10001 --gid appgroup --create-home --shell /usr/sbin/nologin appuser

COPY --from=build /app/web ./web
COPY --from=build /app/worker ./worker
COPY --from=zsign-build /usr/local/bin/zsign /usr/local/bin/zsign

RUN chown -R appuser:appgroup /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

USER appuser

ENTRYPOINT ["dotnet", "web/SignManager.Web.dll"]
