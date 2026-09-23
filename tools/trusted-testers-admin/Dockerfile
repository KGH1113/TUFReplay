FROM oven/bun:1.3.11-alpine

WORKDIR /app
COPY --chown=bun:bun package.json ./
COPY --chown=bun:bun src ./src
COPY --chown=bun:bun public ./public
USER bun
EXPOSE 4177
CMD ["bun", "src/server.ts"]
