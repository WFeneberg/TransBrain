# Build context: repository root.  docker build -f src/TransBrain.Web/Dockerfile .
FROM node:26-alpine AS build
WORKDIR /app
COPY src/TransBrain.Web/package*.json ./
RUN npm ci
COPY src/TransBrain.Web ./
RUN npm run build

FROM nginx:alpine
COPY deploy/nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY deploy/nginx/40-config-js.sh /docker-entrypoint.d/40-config-js.sh
RUN chmod +x /docker-entrypoint.d/40-config-js.sh
COPY --from=build /app/dist/TransBrain.Web/browser /usr/share/nginx/html
EXPOSE 80
