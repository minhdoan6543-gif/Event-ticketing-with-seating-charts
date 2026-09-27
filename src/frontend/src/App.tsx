import React, { useState, useEffect, useCallback } from 'react';

export interface HealthResponse {
  status: string;
  totalDuration?: string;
  entries?: Record<
    string,
    {
      status: string;
      description?: string;
      duration?: string;
    }
  >;
  timestamp?: string;
  version?: string;
}

export const App: React.FC = () => {
  const [loading, setLoading] = useState<boolean>(true);
  const [healthData, setHealthData] = useState<HealthResponse | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [httpStatus, setHttpStatus] = useState<number | null>(null);

  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || '';

  const checkBackendHealth = useCallback(async () => {
    setLoading(true);
    setErrorMessage(null);
    setHttpStatus(null);

    try {
      // Endpoint readiness checks Postgres & Redis
      const endpoint = `${apiBaseUrl}/health/ready`;
      const response = await fetch(endpoint, {
        headers: { Accept: 'application/json' },
      });

      setHttpStatus(response.status);

      if (response.ok) {
        const data: HealthResponse = await response.json();
        setHealthData(data);
        setErrorMessage(null);
      } else {
        let errorDetail = `Mã trạng thái HTTP ${response.status}`;
        try {
          const errJson = await response.json();
          setHealthData(errJson);
          if (errJson.status) {
            errorDetail += ` - Trạng thái hệ thống: ${errJson.status}`;
          }
        } catch {
          setHealthData(null);
        }
        setErrorMessage(errorDetail);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Không thể kết nối đến máy chủ API';
      setErrorMessage(`Lỗi mạng / Không thể kết nối: ${msg}`);
      setHealthData(null);
      setHttpStatus(0);
    } finally {
      setLoading(false);
    }
  }, [apiBaseUrl]);

  useEffect(() => {
    checkBackendHealth();
  }, [checkBackendHealth]);

  const isConnected = !loading && httpStatus === 200 && healthData?.status === 'Healthy';

  return (
    <div className="container">
      <header className="header">
        <h1 className="title" data-testid="project-title">
          Bán vé sự kiện có sơ đồ ghế
        </h1>
        <p className="subtitle">
          User Story S-01: Khung ứng dụng &amp; Kiểm tra kết nối hạ tầng
        </p>
      </header>

      <main>
        {loading ? (
          <div className="status-card loading" data-testid="status-loading">
            <div className="status-header">
              <span className="status-label">Trạng thái kết nối Backend</span>
              <span className="status-badge loading">Đang kiểm tra...</span>
            </div>
            <p className="status-details">
              Hệ thống đang gửi tín hiệu readiness check tới backend ({apiBaseUrl || 'cùng domain'}/health/ready)...
            </p>
          </div>
        ) : isConnected ? (
          <div className="status-card connected" data-testid="status-connected">
            <div className="status-header">
              <span className="status-label">Trạng thái kết nối Backend</span>
              <span className="status-badge connected">Đã kết nối</span>
            </div>
            <div className="status-details">
              <div className="detail-row">
                <strong>Kết quả:</strong>
                <span>Backend sẵn sàng phục vụ (HTTP 200)</span>
              </div>
              <div className="detail-row">
                <strong>Trạng thái tổng thể:</strong>
                <span>{healthData?.status}</span>
              </div>
              {healthData?.entries &&
                Object.entries(healthData.entries).map(([key, val]) => (
                  <div className="detail-row" key={key}>
                    <strong>Dependency [{key}]:</strong>
                    <span>{val.status}</span>
                  </div>
                ))}
              {healthData?.timestamp && (
                <div className="detail-row">
                  <strong>Thời gian kiểm tra:</strong>
                  <span>{new Date(healthData.timestamp).toLocaleString('vi-VN')}</span>
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className="status-card error" data-testid="status-error">
            <div className="status-header">
              <span className="status-label">Trạng thái kết nối Backend</span>
              <span className="status-badge error">Chưa sẵn sàng / Mất kết nối</span>
            </div>
            <div className="status-details">
              <div className="detail-row">
                <strong>Chi tiết lỗi:</strong>
                <span>{errorMessage || 'Không nhận được phản hồi hợp lệ từ backend'}</span>
              </div>
              {httpStatus !== null && httpStatus > 0 && (
                <div className="detail-row">
                  <strong>HTTP Status:</strong>
                  <span>{httpStatus}</span>
                </div>
              )}
              {healthData?.entries &&
                Object.entries(healthData.entries).map(([key, val]) => (
                  <div className="detail-row" key={key}>
                    <strong>Dependency [{key}]:</strong>
                    <span style={{ color: val.status === 'Healthy' ? '#166534' : '#991b1b' }}>
                      {val.status} {val.description ? `(${val.description})` : ''}
                    </span>
                  </div>
                ))}
            </div>
          </div>
        )}

        <div className="actions">
          <button
            onClick={checkBackendHealth}
            disabled={loading}
            data-testid="btn-recheck"
          >
            {loading ? 'Đang kiểm tra...' : 'Kiểm tra lại kết nối'}
          </button>
        </div>
      </main>

      <footer className="footer-info">
        <p>Môi trường: S-01 Staging / Local Foundation &bull; React + TypeScript + ASP.NET Core 8</p>
      </footer>
    </div>
  );
};

export default App;
