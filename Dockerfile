# syntax=docker/dockerfile:1.7

FROM oven/bun:1.3.11 AS deps

WORKDIR /app

COPY package.json bun.lock ./
COPY web/package.json ./web/package.json
# The auto-submission branch has a second Bun workspace that main/dev do not.
# Bind-mount the source context only long enough to make Bun's workspace graph
# complete; do not require that branch-only path to exist on main or dev.
RUN --mount=type=bind,source=.,target=/tmp/repo,readonly \
    if [ -f /tmp/repo/tools/auto-submission-e2e/package.json ]; then \
      mkdir -p /app/tools/auto-submission-e2e \
      && cp /tmp/repo/tools/auto-submission-e2e/package.json /app/tools/auto-submission-e2e/package.json; \
    fi \
    && bun install --frozen-lockfile

FROM deps AS build

ARG TUFREPLAY_ENVIRONMENT=main
ARG TUFREPLAY_BUILD_FLAVOR=standard
ARG TUFREPLAY_BUILD_SHA=unknown
ARG VITE_WEB_ADOFAI_EMBED_URL=https://web-adofai.impl1113.dev/embed/chart

ENV VITE_WEB_ADOFAI_EMBED_URL=${VITE_WEB_ADOFAI_EMBED_URL}
ENV VITE_TUFREPLAY_ENVIRONMENT=${TUFREPLAY_ENVIRONMENT}
ENV VITE_TUFREPLAY_BUILD_FLAVOR=${TUFREPLAY_BUILD_FLAVOR}
ENV VITE_TUFREPLAY_BUILD_SHA=${TUFREPLAY_BUILD_SHA}

COPY . .

RUN case "$TUFREPLAY_ENVIRONMENT" in main|dev|auto-submission) ;; *) echo "Unsupported deployment environment" >&2; exit 64 ;; esac \
    && case "$TUFREPLAY_BUILD_FLAVOR" in standard|auto-submission) ;; *) echo "Unsupported TUFReplay build flavor" >&2; exit 64 ;; esac \
    && case "$TUFREPLAY_BUILD_SHA" in unknown) ;; *) \
      [ "${#TUFREPLAY_BUILD_SHA}" -eq 40 ] || { echo "TUFREPLAY_BUILD_SHA must be a full commit SHA" >&2; exit 64; } \
      && case "$TUFREPLAY_BUILD_SHA" in *[!0-9a-f]*) echo "TUFREPLAY_BUILD_SHA must be lowercase hexadecimal" >&2; exit 64 ;; esac ;; \
    esac \
    && mkdir -p web/public \
    && printf '{\n  "environment": "%s",\n  "buildFlavor": "%s",\n  "gitSHA": "%s"\n}\n' \
      "$TUFREPLAY_ENVIRONMENT" "$TUFREPLAY_BUILD_FLAVOR" "$TUFREPLAY_BUILD_SHA" \
      > web/public/deployment.json \
    && bun run web:build

FROM oven/bun:1.3.11 AS runtime

WORKDIR /app

ENV NODE_ENV=production

COPY --from=deps /app/node_modules ./node_modules
COPY --from=deps /app/web/node_modules ./web/node_modules
COPY --from=build /app/package.json ./package.json
COPY --from=build /app/web/package.json ./web/package.json
COPY --from=build /app/web/vite.config.ts ./web/vite.config.ts
COPY --from=build /app/web/dist ./web/dist

EXPOSE 4173

HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=6 \
  CMD bun -e 'fetch("http://127.0.0.1:4173/").then((response) => process.exit(response.ok ? 0 : 1)).catch(() => process.exit(1))'

CMD ["bun", "run", "web:preview", "--", "--host", "0.0.0.0", "--port", "4173"]
