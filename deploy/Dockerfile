# Execute a partir da raiz do repositório:
#   docker buildx build --platform linux/arm64 -f deploy/Dockerfile -t wrexercise:latest --load .
#
# Os estágios de build rodam na arquitetura da máquina ($BUILDPLATFORM) e fazem
# compilação cruzada, evitando a lentidão da emulação. Os caches do npm e do NuGet ficam
# no BuildKit entre builds, sem entrar na imagem.

FROM --platform=$BUILDPLATFORM node:24.22.2-alpine AS web
WORKDIR /repo/Wrexercise/webapp
COPY Wrexercise/webapp/package.json Wrexercise/webapp/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci --no-audit --no-fund
COPY Wrexercise/webapp/ ./
RUN npm run build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.403-noble-aot AS api
ARG BUILDARCH
ARG TARGETARCH
# A imagem só traz o linker da própria arquitetura; a compilação cruzada precisa deste.
RUN if [ "$BUILDARCH" = amd64 ] && [ "$TARGETARCH" = arm64 ]; then \
      apt-get update \
      && apt-get install -y --no-install-recommends binutils-aarch64-linux-gnu gcc-aarch64-linux-gnu libc6-dev-arm64-cross \
      && rm -rf /var/lib/apt/lists/*; \
    fi
WORKDIR /repo
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY Wrexercise/ Wrexercise/
COPY --from=web /repo/Wrexercise/wwwroot/ Wrexercise/wwwroot/
RUN --mount=type=cache,target=/root/.nuget/packages \
    case "$TARGETARCH" in \
      amd64) rid=linux-x64 ;; \
      arm64) rid=linux-arm64 ;; \
      *) echo "Arquitetura não suportada: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && dotnet publish Wrexercise/Wrexercise.csproj -c Release -r "$rid" -o /publish

# A variante "extra" inclui tzdata, necessário para os fusos horários IANA dos grupos.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=api /publish/ ./
EXPOSE 8080
ENTRYPOINT ["./Wrexercise"]
