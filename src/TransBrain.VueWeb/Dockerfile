# Build context: repository root.  docker build -f src/TransBrain.VueWeb/Dockerfile .
FROM node:26-alpine AS build
WORKDIR /app
COPY src/TransBrain.VueWeb/package*.json ./
RUN npm ci
COPY src/TransBrain.VueWeb ./
RUN npm run build

FROM nginx:alpine
COPY deploy/nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY deploy/nginx/40-config-js.sh /docker-entrypoint.d/40-config-js.sh
RUN chmod +x /docker-entrypoint.d/40-config-js.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
