ARG NODE_VERSION
ARG NAKAMA_VERSION

FROM node:${NODE_VERSION} AS modules
ARG TYPESCRIPT_VERSION
ARG SERVER_VERSION
RUN npm install --global typescript@${TYPESCRIPT_VERSION}
WORKDIR /repo
COPY Assets/StreamingAssets/Data/party.json Assets/StreamingAssets/Data/stages.json Assets/StreamingAssets/Data/rift.json Assets/StreamingAssets/Data/
COPY Server/package.json Server/tsconfig.json Server/
COPY Server/src Server/src
COPY Server/tools Server/tools
COPY Server/test Server/test
COPY Server/test-vectors Server/test-vectors
WORKDIR /repo/Server
RUN test "$(node -p "require('./package.json').version")" = "${SERVER_VERSION}" || { echo "Server/package.json version differs from SERVER_VERSION in versions.env" >&2; exit 1; }
RUN npm test

FROM registry.heroiclabs.com/heroiclabs/nakama:${NAKAMA_VERSION}
ARG SERVER_VERSION
LABEL org.opencontainers.image.title="riftwarden-nakama" org.opencontainers.image.version="${SERVER_VERSION}"
COPY --from=modules /repo/Server/build/index.js /nakama/data/modules/index.js
COPY Server/deploy/nakama.yml /nakama/data/nakama.yml
COPY Server/deploy/nakama-start.sh /nakama/start.sh
ENTRYPOINT ["/bin/sh", "/nakama/start.sh"]
