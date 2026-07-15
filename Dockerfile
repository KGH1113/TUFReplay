FROM oven/bun:1.3.11 AS deps

WORKDIR /app

COPY package.json bun.lock ./
COPY web/package.json ./web/package.json
RUN bun install --frozen-lockfile

FROM deps AS build

COPY . .

ARG VITE_WEB_ADOFAI_EMBED_URL=https://web-adofai.impl1113.dev/embed/chart
ENV VITE_WEB_ADOFAI_EMBED_URL=$VITE_WEB_ADOFAI_EMBED_URL

RUN bun run web:build

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

CMD ["bun", "run", "web:preview", "--", "--host", "0.0.0.0", "--port", "4173"]
