FROM node:22-bookworm-slim
WORKDIR /app/tools/live-e2e/infra
COPY tools/live-e2e/infra/package*.json ./
RUN npm ci --omit=dev
COPY tools/live-e2e/infra/ ./
COPY deploy/replay-cdn/worker.mjs /app/deploy/replay-cdn/worker.mjs
ENV NODE_ENV=development
CMD ["node", "cdn.mjs"]
