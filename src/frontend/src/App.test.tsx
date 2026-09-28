import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import App from './App';

describe('Trang chủ Bán vé sự kiện có sơ đồ ghế (Frontend S-01)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('Hiển thị chính xác tên dự án và tiêu đề S-01', async () => {
    // Mock API returning healthy
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({
          status: 'Healthy',
          entries: {
            postgresql: { status: 'Healthy' },
            redis: { status: 'Healthy' },
          },
          timestamp: new Date().toISOString(),
        }),
      })
    );

    render(<App />);

    const titleElement = screen.getByTestId('project-title');
    expect(titleElement).toBeInTheDocument();
    expect(titleElement).toHaveTextContent('Bán vé sự kiện có sơ đồ ghế');

    // Chờ kết quả kết nối thành công
    await waitFor(() => {
      expect(screen.getByTestId('status-connected')).toBeInTheDocument();
    });
  });

  it('Hiển thị trạng thái "Đã kết nối" khi Backend trả HTTP 200 Healthy', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({
          status: 'Healthy',
          entries: {
            postgresql: { status: 'Healthy' },
            redis: { status: 'Healthy' },
          },
          timestamp: '2026-09-27T12:00:00Z',
        }),
      })
    );

    render(<App />);

    await waitFor(() => {
      const connectedCard = screen.getByTestId('status-connected');
      expect(connectedCard).toBeInTheDocument();
      expect(screen.getByText('Đã kết nối')).toBeInTheDocument();
      expect(screen.getByText('Backend sẵn sàng phục vụ (HTTP 200)')).toBeInTheDocument();
      expect(screen.getByText('Dependency [postgresql]:')).toBeInTheDocument();
      expect(screen.getByText('Dependency [redis]:')).toBeInTheDocument();
    });
  });

  it('Hiển thị trạng thái "Chưa sẵn sàng / Mất kết nối" khi Backend trả HTTP 503 (Dependency Down)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
        json: async () => ({
          status: 'Unhealthy',
          entries: {
            postgresql: { status: 'Unhealthy', description: 'Failed to connect to database' },
            redis: { status: 'Healthy' },
          },
        }),
      })
    );

    render(<App />);

    await waitFor(() => {
      const errorCard = screen.getByTestId('status-error');
      expect(errorCard).toBeInTheDocument();
      expect(screen.getByText('Chưa sẵn sàng / Mất kết nối')).toBeInTheDocument();
      expect(screen.queryByTestId('status-connected')).not.toBeInTheDocument();
      expect(screen.getByText(/Mã trạng thái HTTP 503/)).toBeInTheDocument();
    });
  });

  it('Hiển thị lỗi kết nối khi mạng bị ngắt hoặc không gọi được backend (Network Failure)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockRejectedValue(new Error('Failed to fetch / Connection refused'))
    );

    render(<App />);

    await waitFor(() => {
      const errorCard = screen.getByTestId('status-error');
      expect(errorCard).toBeInTheDocument();
      expect(screen.queryByTestId('status-connected')).not.toBeInTheDocument();
      expect(screen.getByText(/Không thể kết nối/)).toBeInTheDocument();
    });
  });
});
