# =============================================================================
# Dev target: hot reload with astro dev
# =============================================================================
FROM node:22-alpine AS dev

WORKDIR /app

COPY src/frontend/package.json src/frontend/package-lock.json* ./

RUN npm install

COPY src/frontend/ ./

EXPOSE 4321

CMD ["npm", "run", "dev"]

# =============================================================================
# Build target: build static output
# =============================================================================
FROM node:22-alpine AS build

WORKDIR /app

COPY src/frontend/package.json src/frontend/package-lock.json* ./

RUN npm install

COPY src/frontend/ ./

RUN npm run build

# =============================================================================
# Prod target: serve with Node adapter
# =============================================================================
FROM node:22-alpine AS prod

RUN addgroup -S appgroup && adduser -S appuser -G appgroup

WORKDIR /app

COPY --from=build /app/dist ./dist
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/package.json ./

USER appuser

EXPOSE 4321

CMD ["node", "dist/server/entry.mjs"]
