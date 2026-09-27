#!/usr/bin/env bash
# ==============================================================================
# Script Triển Khai Blue-Green Zero-Downtime Lên Môi Trường Staging
# Dự án: Bán vé sự kiện có sơ đồ ghế (User Story S-01)
# ==============================================================================

set -euo pipefail

IMAGE_TAG="${1:-${IMAGE_TAG:-}}"
if [ -z "$IMAGE_TAG" ]; then
    echo "::error::Thiếu IMAGE_TAG (Commit SHA). Cách dùng: ./deploy.sh <COMMIT_SHA>"
    exit 1
fi

REGISTRY="${REGISTRY:-ghcr.io}"
REPO_NAME="${REPO_NAME:-ticket-booking}"
BACKEND_IMAGE="${REGISTRY}/${REPO_NAME}/backend:${IMAGE_TAG}"
FRONTEND_IMAGE="${REGISTRY}/${REPO_NAME}/frontend:${IMAGE_TAG}"
ENV_FILE="${ENV_FILE:-.env.staging}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STAGING_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${STAGING_DIR}"

if [ ! -f "$ENV_FILE" ]; then
    echo "::error::Không tìm thấy file cấu hình môi trường: ${ENV_FILE}"
    echo "Vui lòng copy từ env.staging.example và cấu hình các giá trị bí mật."
    exit 1
fi

# Load các biến môi trường từ file staging
set -a
# shellcheck disable=SC1090
source "${ENV_FILE}"
set +a

# 1. Xác định slot đang chạy (blue hoặc green)
SLOT_FILE="${STAGING_DIR}/current_slot.txt"
if [ -f "$SLOT_FILE" ]; then
    CURRENT_SLOT=$(cat "$SLOT_FILE")
else
    CURRENT_SLOT="blue"
fi

if [ "$CURRENT_SLOT" = "blue" ]; then
    TARGET_SLOT="green"
    TARGET_BACKEND_PORT="5002"
    TARGET_FRONTEND_PORT="3002"
else
    TARGET_SLOT="blue"
    TARGET_BACKEND_PORT="5001"
    TARGET_FRONTEND_PORT="3001"
fi

echo "=================================================================="
echo " [STAGING DEPLOY] Bắt đầu triển khai phiên bản: ${IMAGE_TAG}"
echo " Slot hiện tại đang phục vụ: [${CURRENT_SLOT}]"
echo " Slot mục tiêu sẽ triển khai: [${TARGET_SLOT}]"
echo "=================================================================="

# 2. Đảm bảo hạ tầng cơ bản (DB, Redis, Nginx Proxy) đang hoạt động
echo "===> Bước 1: Khởi động hoặc kiểm tra hạ tầng cốt lõi (db, redis, proxy)..."
docker compose -f docker-compose.staging.yml --env-file "${ENV_FILE}" up -d db redis proxy

echo "===> Chờ cơ sở dữ liệu và Redis đạt trạng thái Healthy..."
for i in {1..30}; do
    DB_STATUS=$(docker inspect --format='{{json .State.Health.Status}}' staging_db 2>/dev/null || echo '"starting"')
    REDIS_STATUS=$(docker inspect --format='{{json .State.Health.Status}}' staging_redis 2>/dev/null || echo '"starting"')
    if [ "$DB_STATUS" = '"healthy"' ] && [ "$REDIS_STATUS" = '"healthy"' ]; then
        echo "PostgreSQL và Redis đã sẵn sàng!"
        break
    fi
    if [ "$i" -eq 30 ]; then
        echo "::error::PostgreSQL hoặc Redis không thể khởi động sau 30 giây!"
        exit 1
    fi
    sleep 1
done

# 3. Kéo Docker image mới từ Registry
echo "===> Bước 2: Kéo image mới theo commit SHA [${IMAGE_TAG}]..."
docker pull "${BACKEND_IMAGE}"
docker pull "${FRONTEND_IMAGE}"

# 4. Chạy Database Migration trong container độc lập trước khi bật code mới
echo "===> Bước 3: Áp dụng Migration lên Database Staging..."
MIGRATION_EXIT_CODE=0
docker run --rm \
    --network ticket_staging_network \
    --env-file "${ENV_FILE}" \
    "${BACKEND_IMAGE}" \
    --migrate-database || MIGRATION_EXIT_CODE=$?

if [ "$MIGRATION_EXIT_CODE" -ne 0 ]; then
    echo "::error::Database Migration thất bại! Dừng triển khai ngay lập tức."
    echo "Slot [${CURRENT_SLOT}] vẫn an toàn và đang phục vụ người dùng. Không có downtime."
    exit 1
fi
echo "Migration hoàn thành thành công!"

# 5. Khởi động Slot mới
echo "===> Bước 4: Khởi động container cho slot mới [${TARGET_SLOT}]..."
if [ "$TARGET_SLOT" = "green" ]; then
    export BACKEND_IMAGE_GREEN="${BACKEND_IMAGE}"
    export FRONTEND_IMAGE_GREEN="${FRONTEND_IMAGE}"
else
    export BACKEND_IMAGE_BLUE="${BACKEND_IMAGE}"
    export FRONTEND_IMAGE_BLUE="${FRONTEND_IMAGE}"
fi

docker compose -f docker-compose.staging.yml --env-file "${ENV_FILE}" up -d \
    "backend_${TARGET_SLOT}" "frontend_${TARGET_SLOT}"

# 6. Kiểm tra Readiness & Smoke Test trên slot mới trước khi chuyển traffic
echo "===> Bước 5: Thực hiện Healthcheck & Smoke Test trên slot mới [${TARGET_SLOT}]..."
SLOT_HEALTHY=0
for i in {1..20}; do
    echo "Kiểm tra readiness lần $i/20 tại http://127.0.0.1:${TARGET_BACKEND_PORT}/health/ready..."
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://127.0.0.1:${TARGET_BACKEND_PORT}/health/ready" || echo "000")
    if [ "$HTTP_CODE" = "200" ]; then
        echo "Slot mới [${TARGET_SLOT}] đã phản hồi HTTP 200 Healthy!"
        SLOT_HEALTHY=1
        break
    fi
    sleep 2
done

if [ "$SLOT_HEALTHY" -ne 1 ]; then
    echo "::error::Slot mới [${TARGET_SLOT}] không vượt qua bài kiểm tra Readiness (Mã HTTP: ${HTTP_CODE})!"
    echo "Dừng container slot mới [${TARGET_SLOT}], giữ nguyên traffic ở slot cũ [${CURRENT_SLOT}]."
    docker compose -f docker-compose.staging.yml --env-file "${ENV_FILE}" stop "backend_${TARGET_SLOT}" "frontend_${TARGET_SLOT}"
    exit 1
fi

# Smoke test kiểm tra frontend slot mới
FRONTEND_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://127.0.0.1:${TARGET_FRONTEND_PORT}/" || echo "000")
if [ "$FRONTEND_CODE" != "200" ]; then
    echo "::error::Frontend slot mới [${TARGET_SLOT}] trả về mã HTTP ${FRONTEND_CODE}, không phải 200!"
    docker compose -f docker-compose.staging.yml --env-file "${ENV_FILE}" stop "backend_${TARGET_SLOT}" "frontend_${TARGET_SLOT}"
    exit 1
fi

# 7. Chuyển đổi traffic (Cut-over) bằng cách cập nhật upstream Nginx và reload graceful
echo "===> Bước 6: Chuyển traffic sang slot [${TARGET_SLOT}] (Zero-Downtime Reload)..."
cat <<EOF > "${STAGING_DIR}/nginx/conf.d/upstream.conf"
# Tự động cập nhật bởi deploy.sh lúc $(date -u +"%Y-%m-%dT%H:%M:%SZ")
upstream active_frontend {
    server frontend_${TARGET_SLOT}:80;
}

upstream active_backend {
    server backend_${TARGET_SLOT}:5000;
}
EOF

# Nginx reload không làm ngắt kết nối đang xử lý
docker exec staging_proxy nginx -s reload

# 8. Hậu kiểm tra (Post-cutover verification) qua cổng chính thức 80
echo "===> Bước 7: Hậu kiểm tra trên cổng truy cập chính thức..."
PUBLIC_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://127.0.0.1:${STAGING_HTTP_PORT:-80}/health/ready" || echo "000")
if [ "$PUBLIC_CODE" != "200" ]; then
    echo "::error::Hậu kiểm tra thất bại trên cổng chính thức! Tiến hành hoàn tác khẩn cấp về [${CURRENT_SLOT}]..."
    "${SCRIPT_DIR}/rollback.sh"
    exit 1
fi

# 9. Dọn dẹp an toàn: Dừng slot cũ sau thời gian chờ (drain period)
echo "===> Bước 8: Dừng slot cũ [${CURRENT_SLOT}] sau 10 giây thoát phiên kết nối..."
sleep 10
docker compose -f docker-compose.staging.yml --env-file "${ENV_FILE}" stop "backend_${CURRENT_SLOT}" "frontend_${CURRENT_SLOT}"

# Lưu lại trạng thái slot hiện tại và tag commit trước đó
echo "${TARGET_SLOT}" > "$SLOT_FILE"
echo "${IMAGE_TAG}" > "${STAGING_DIR}/previous_tag.txt"

echo "=================================================================="
echo " [STAGING DEPLOY THÀNH CÔNG]"
echo " Phiên bản ${IMAGE_TAG} đã hoạt động trên slot [${TARGET_SLOT}]."
echo " Trang chủ và API sẵn sàng phục vụ 100%!"
echo "=================================================================="
