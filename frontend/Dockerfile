# docker build -f frontend/Dockerfile --build-arg VITE_API_URL=https://api.flowdesk.example frontend
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
COPY apps/web/package.json apps/web/
COPY packages/ packages/
RUN npm ci
COPY . .
ARG VITE_API_URL=http://localhost:5080
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/apps/web/dist /usr/share/nginx/html
EXPOSE 80
