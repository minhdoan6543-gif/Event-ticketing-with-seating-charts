import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getEventById } from '../api/eventsApi';
import { EVENT_STATUS, getEventStatusInfo } from '../constants/eventStatus';

export default function EventDetail() {
  const { id } = useParams();
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let ignore = false;

    getEventById(id)
      .then((data) => {
        if (!ignore) {
          setEvent(data);
        }
      })
      .catch((err) => {
        if (!ignore) {
          setError(err);
        }
      })
      .finally(() => {
        if (!ignore) {
          setLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [id]);

  if (loading) {
    return (
      <div
        style={{
          maxWidth: '850px',
          margin: '40px auto',
          padding: '48px 16px',
          textAlign: 'center',
          backgroundColor: 'var(--code-bg, #1f2028)',
          borderRadius: '8px',
          border: '1px dashed var(--border, #2e303a)',
          color: 'var(--text, #9ca3af)',
        }}
      >
        <p style={{ margin: 0, fontSize: '16px', fontWeight: '500', color: 'var(--text-h, #f1f5f9)' }}>
          Đang tải thông tin sự kiện...
        </p>
      </div>
    );
  }

  if (error || !event) {
    return (
      <div
        style={{
          maxWidth: '600px',
          margin: '40px auto',
          padding: '24px',
          backgroundColor: '#fff1f2',
          border: '1px solid #fecdd3',
          borderRadius: '8px',
          textAlign: 'center',
          color: '#9f1239',
        }}
      >
        <h3 style={{ margin: '0 0 8px 0', fontSize: '18px' }}>Từ chối truy cập</h3>
        <p style={{ margin: '0 0 20px 0', fontSize: '14px' }}>Bạn không có quyền truy cập sự kiện này</p>
        <Link
          to="/events"
          style={{
            display: 'inline-block',
            padding: '8px 16px',
            backgroundColor: '#e11d48',
            color: '#ffffff',
            borderRadius: '6px',
            textDecoration: 'none',
            fontSize: '14px',
            fontWeight: '500',
          }}
        >
          Quay lại danh sách sự kiện
        </Link>
      </div>
    );
  }

  const statusInfo = getEventStatusInfo(event.status);
  const isDraft = event.status === EVENT_STATUS.DRAFT;

  return (
    <div
      style={{
        maxWidth: '850px',
        margin: '30px auto',
        padding: '24px',
        backgroundColor: 'var(--code-bg, #1f2028)',
        borderRadius: '8px',
        border: '1px solid var(--border, #2e303a)',
        boxShadow: '0 1px 3px rgba(0,0,0,0.1)',
        textAlign: 'left',
      }}
    >
      {/* Navigation bar */}
      <div style={{ marginBottom: '20px' }}>
        <Link
          to="/events"
          style={{
            color: 'var(--accent, #3b82f6)',
            textDecoration: 'none',
            fontSize: '14px',
            fontWeight: '500',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
          }}
        >
          &larr; Quay lại danh sách sự kiện
        </Link>
      </div>

      {/* Draft banner notice */}
      {isDraft && (
        <div
          style={{
            padding: '12px 16px',
            marginBottom: '20px',
            backgroundColor: '#fffbeb',
            border: '1px solid #fde68a',
            borderRadius: '6px',
            color: '#92400e',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            fontSize: '14px',
            fontWeight: '500',
          }}
        >
          <span>ℹ️</span> Chưa hiển thị với người mua
        </div>
      )}

      {/* Main Event Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          flexWrap: 'wrap',
          gap: '16px',
          marginBottom: '24px',
          borderBottom: '1px solid var(--border, #2e303a)',
          paddingBottom: '20px',
        }}
      >
        <div style={{ flex: '1', minWidth: '280px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '8px', flexWrap: 'wrap' }}>
            <h1 style={{ margin: 0, fontSize: '24px', color: 'var(--text-h, #f1f5f9)', fontWeight: '700' }}>
              {event.name}
            </h1>
            <span
              style={{
                padding: '4px 10px',
                borderRadius: '12px',
                fontSize: '12px',
                fontWeight: '600',
                ...statusInfo.style,
              }}
            >
              {statusInfo.label}
            </span>
          </div>
          <div style={{ color: 'var(--text, #9ca3af)', fontSize: '14px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span style={{ fontWeight: '600', color: 'var(--text-h, #f1f5f9)' }}>Địa điểm:</span>
            <span>{event.location}</span>
          </div>
        </div>
      </div>

      {/* Description section */}
      <div style={{ marginBottom: '32px' }}>
        <h3 style={{ margin: '0 0 10px 0', fontSize: '16px', color: 'var(--text-h, #f1f5f9)', fontWeight: '600' }}>
          Mô tả sự kiện
        </h3>
        <p style={{ margin: 0, color: 'var(--text, #9ca3af)', fontSize: '14px', lineHeight: '1.6', whiteSpace: 'pre-line' }}>
          {event.description}
        </p>
      </div>

      {/* Placeholder Suất diễn section for next step */}
      <div
        style={{
          borderTop: '2px dashed var(--border, #2e303a)',
          paddingTop: '24px',
          marginTop: '24px',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <h3 style={{ margin: 0, fontSize: '18px', color: 'var(--text-h, #f1f5f9)', fontWeight: '600' }}>
            Suất diễn
          </h3>
          <span style={{ fontSize: '12px', color: 'var(--text, #9ca3af)', fontStyle: 'italic' }}>
            (Phân hệ quản lý lịch &amp; khung giờ)
          </span>
        </div>
        <div
          style={{
            padding: '36px 16px',
            backgroundColor: 'var(--bg, #16171d)',
            border: '1px solid var(--border, #2e303a)',
            borderRadius: '6px',
            textAlign: 'center',
            color: 'var(--text, #9ca3af)',
          }}
        >
          <div style={{ fontSize: '15px', fontWeight: '500', color: 'var(--text-h, #f1f5f9)', marginBottom: '4px' }}>
            Sẽ làm ở bước sau
          </div>
          <p style={{ margin: 0, fontSize: '13px', color: 'var(--text, #9ca3af)' }}>
            Khu vực cấu hình thời gian bắt đầu, kết thúc, hạng vé và sơ đồ ghế cho từng suất diễn.
          </p>
        </div>
      </div>
    </div>
  );
}
