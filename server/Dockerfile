FROM rust:1.97.1-bookworm AS build

WORKDIR /app

ARG BUILD_SHA=unknown
ENV BUILD_SHA=${BUILD_SHA}

COPY Cargo.toml Cargo.lock ./
COPY migration/Cargo.toml ./migration/Cargo.toml
COPY migration/src ./migration/src
COPY src ./src
COPY config ./config

RUN cargo build --locked --release --bin tuf_replay_server-cli

FROM debian:bookworm-slim AS runtime

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build --chown=10001:10001 /app/target/release/tuf_replay_server-cli /usr/local/bin/tuf_replay_server-cli
COPY --from=build --chown=10001:10001 /app/config ./config

RUN mkdir -p /var/lib/tuf-replay/artifacts \
    && chown -R 10001:10001 /var/lib/tuf-replay

ENV LOCO_ENV=production
ENV SCHEDULER_CONFIG=config/scheduler.yaml

USER 10001:10001

EXPOSE 5150

ENTRYPOINT ["tuf_replay_server-cli"]
CMD ["start", "--all"]
