#!/usr/bin/env bash
# ==============================================================================
# Script Hoàn Tác Khẩn Cấp (Emergency Rollback) Trên Môi Trường Staging
# Dự án: Bán vé sự kiện có sơ đồ ghế (User Story S-01)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STAGING_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${STAGING_DIR}"

ENV_FILE="${ENV_FILE:-.env.staging}"
if [ ! -f "$ENV_FILE" ]; then
    echo "::error::Không tìm thấy file cấu hình môi trường: ${ENV_FILE}"
    exit 1
fi

set -a
# shellcheck disable=SC1090
source "${ENV_FILE}"
set +a

SLOT_FILE="${STAGING_DIR}/current_slot.txt"
if [ ! -f "$SLOT_FILE" ]; then
    echo "::error::Không tìm thấy file trạng thái slot: ${SLOT_FILE}"
    exit 1
fi

CURRENT_SLOT=$(cat "$SLOT_FILE")
if [ "$CURRENT_SLOT" = "blue" ]; then
    TARGET_SLOT="green"
    TARGET_BACKEND_PORT="5002"
else
    TARGET_SLOT="blue"
    TARGET_BACKEND_PORT="5001"
fi

echo "=================================================================="
echo " [STAGING ROLLBACK KHẨN CẤP]"
echo " Đang hoàn tác từ slot [${CURRENT_SLOT}] về slot [${TARGET_SLOT}]..."
echo "=================================================================="

# 1. Khởi động lại slot mục tiêu nếu đang bị dừng
echo "===> Bước 1: Đảm bảo container slot [${TARGET_SLOT}] đang chạy..."
docker compose -f docker-compose.staging.yml --env-file "${ENV_FILE}" start \
    "backend_${TARGET_SLOT}" "frontend_${TARGET_SLOT}" || \
docker compose -f docker-compose.staging.yml --env-file "${ENV_FILE}" up -d \
    "backend_${TARGET_SLOT}" "frontend_${TARGET_SLOT}"

# 2. Kiểm tra sức khỏe của slot dự phòng trước khi chuyển traffic
echo "===> Bước 2: Kiểm tra readiness của slot [${TARGET_SLOT}]..."
SLOT_HEALTHY=0
for i in {1..15}; do
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://127.0.0.1:${TARGET_BACKEND_PORT}/health/ready" || echo "000")
    if [ "$HTTP_CODE" = "200" ]; then
        echo "Slot [${TARGET_SLOT}] đã sẵn sàng phản hồi HTTP 200 Healthy!"
        SLOT_HEALTHY=1
        break
    fi
    sleep 2
done

if [ "$SLOT_HEALTHY" -ne 1 ]; then
    echo "::error::Slot dự phòng [${TARGET_SLOT}] cũng không vượt qua kiểm tra readiness!"
    exit 1
fi

# 3. Chuyển Nginx upstream về slot dự phòng và reload
echo "===> Bước 3: Cập nhật cấu hình Nginx upstream về [${TARGET_SLOT}]..."
cat <<EOF > "${STAGING_DIR}/nginx/conf.d/upstream.conf"
# Hoàn tác bởi rollback.sh lúc $(date -u +"%Y-%m-%dT%H:%M:%SZ")
upstream active_frontend {
    server frontend_${TARGET_SLOT}:80;
}

upstream active_backend {
    server backend_${TARGET_SLOT}:5000;
}
EOF

docker exec staging_proxy nginx -s reload

# 4. Cập nhật file trạng thái slot
echo "${TARGET_SLOT}" > "$SLOT_FILE"

# 5. Dừng slot bị lỗi để giải phóng tài nguyên
echo "===> Bước 4: Dừng slot lỗi [${CURRENT_SLOT}]..."
docker compose -f docker-compose.staging.yml --env-file "${ENV_FILE}" stop \
    "backend_${CURRENT_SLOT}" "frontend_${CURRENT_SLOT}"

echo "=================================================================="
echo " [ROLLBACK HOÀN TẤT THÀNH CÔNG]"
echo " Traffic đã được chuyển an toàn về slot [${TARGET_SLOT}]."
echo ""
echo " >> CẢNH BÁO QUAN TRỌNG VỀ DATABASE <<"
echo " Hệ thống CHỈ rollback ứng dụng (Application Containers)."
echo " Tuyệt đối KHÔNG tự động chạy migration down trên Database vì có"
echo " nguy cơ làm mất dữ liệu. Mọi thay đổi schema phải luôn tương"
echo " thích ngược (backward compatible) với phiên bản ứng dụng cũ."
echo "=================================================================="
