# StrikeShield frontend (docs/PHASED_PLAN.md Phase 9) — built once here,
# served as static files by nginx, which also reverse-proxies /api and
# /hubs to the api service so the SPA only ever talks same-origin
# (see nginx.conf).
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
