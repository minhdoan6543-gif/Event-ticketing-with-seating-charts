# ========================================================
# Stage 1: Build React App
# ========================================================
FROM node:20.18.0-alpine3.20 AS builder

WORKDIR /app

# Copy dependency definition
COPY package.json ./

# Install dependencies (ci/clean install)
RUN npm install

# Copy source code and config
COPY tsconfig*.json vite.config.ts index.html ./
COPY src/ ./src/

# Build static assets
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

# ========================================================
# Stage 2: Serve with Nginx
# ========================================================
FROM nginx:1.27.2-alpine AS runner

# Remove default nginx html
RUN rm -rf /usr/share/nginx/html/*

# Copy built artifacts from builder stage
COPY --from=builder /app/dist /usr/share/nginx/html

# Copy custom nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose HTTP port
EXPOSE 80

# Healthcheck for frontend web server
HEALTHCHECK --interval=15s --timeout=5s --start-period=5s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://127.0.0.1/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
