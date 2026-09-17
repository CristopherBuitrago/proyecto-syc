FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .

# Vite reemplaza import.meta.env.VITE_* en tiempo de BUILD, no en runtime — por eso
# estas variables llegan como build args (ver docker-compose.yml), no como `environment`
# del contenedor final (que solo sirve archivos estáticos con nginx).
ARG VITE_ORDERS_API_URL
ARG VITE_CATALOG_API_URL
ENV VITE_ORDERS_API_URL=$VITE_ORDERS_API_URL
ENV VITE_CATALOG_API_URL=$VITE_CATALOG_API_URL
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
