import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getPublicEvents } from '../api/eventsApi';

export default function Home() {
  const [events, setEvents] = useState([]);
  const [nextCursor, setNextCursor] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const navigate = useNavigate();

  const fetchEvents = async (cursor = null) => {
    try {
      setLoading(true);
      setError(null);
      
      const { items, nextCursor: returnedCursor } = await getPublicEvents(cursor);
      
      if (cursor) {
        setEvents((prev) => [...prev, ...items]);
      } else {
        setEvents(items);
      }
      
      setNextCursor(returnedCursor);
    } catch (err) {
      console.error('Error fetching events:', err);
      setError('Đã xảy ra lỗi khi tải danh sách sự kiện. Vui lòng thử lại sau.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchEvents();
  }, []);

  const formatDate = (utcString) => {
    const date = new Date(utcString);
    return date.toLocaleString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  return (
    <div style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
      <h1>Sự kiện đang mở bán</h1>
      
      {error && (
        <div style={{ backgroundColor: '#f8d7da', color: '#721c24', padding: '15px', borderRadius: '4px', marginBottom: '20px' }}>
          <p style={{ margin: '0 0 10px 0' }}>{error}</p>
          <button 
            onClick={() => fetchEvents()} 
            style={{ padding: '8px 16px', background: '#dc3545', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
          >
            Thử lại
          </button>
        </div>
      )}

      {loading && events.length === 0 ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))', gap: '20px', marginTop: '20px' }}>
          {[1, 2, 3, 4].map((n) => (
            <div key={n} style={{ border: '1px solid #ddd', borderRadius: '8px', height: '300px', backgroundColor: '#f9f9f9', animation: 'pulse 1.5s infinite' }} />
          ))}
        </div>
      ) : !loading && events.length === 0 && !error ? (
        <div style={{ textAlign: 'center', padding: '40px', backgroundColor: '#f9f9f9', borderRadius: '8px', marginTop: '20px' }}>
          <h3 style={{ color: '#666', margin: 0 }}>Không có sự kiện nào đang mở bán. Vui lòng quay lại sau!</h3>
        </div>
      ) : (
        <div style={{ 
          display: 'grid', 
          gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))', 
          gap: '20px',
          marginTop: '20px'
        }}>
          {events.map((event) => (
            <div 
              key={event.eventId} 
              style={{ 
                border: '1px solid #ddd', 
                borderRadius: '8px', 
                overflow: 'hidden',
                cursor: event.hasAvailableShowtimes ? 'pointer' : 'default',
                display: 'flex',
                flexDirection: 'column',
                opacity: event.hasAvailableShowtimes ? 1 : 0.8
              }}
              onClick={() => {
                if (event.hasAvailableShowtimes && event.nearestShowtime) {
                  navigate(`/shows/${event.nearestShowtime.id}`);
                }
              }}
            >
              {event.imageUrl ? (
                <img 
                  src={event.imageUrl} 
                  alt={event.name} 
                  style={{ width: '100%', height: '150px', objectFit: 'cover' }} 
                />
              ) : (
                <div style={{ width: '100%', height: '150px', backgroundColor: '#ccc', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <span style={{ color: '#666' }}>Không có ảnh</span>
                </div>
              )}
              
              <div style={{ padding: '15px', flex: 1, display: 'flex', flexDirection: 'column' }}>
                <h3 style={{ margin: '0 0 10px 0', fontSize: '1.2rem' }}>{event.name}</h3>
                <p style={{ margin: '0 0 5px 0', color: '#555' }}>📍 {event.location}</p>
                {event.hasAvailableShowtimes && event.nearestShowtime ? (
                  <>
                    <p style={{ margin: '0 0 5px 0', color: '#555', fontWeight: 'bold' }}>
                      ⏰ {formatDate(event.nearestShowtime.startTime)}
                    </p>
                    {event.showtimeCount > 1 && (
                      <p style={{ margin: '0 0 5px 0', color: '#007bff', fontSize: '0.9rem' }}>
                        + {event.showtimeCount - 1} suất diễn khác
                      </p>
                    )}
                    <button 
                      style={{ marginTop: 'auto', padding: '8px', backgroundColor: '#007bff', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                    >
                      Xem chi tiết & Đặt vé
                    </button>
                  </>
                ) : (
                  <>
                    <p style={{ margin: '0 0 5px 0', color: '#888', fontStyle: 'italic' }}>
                      Chưa có suất diễn nào mở bán
                    </p>
                    <button 
                      disabled
                      style={{ marginTop: 'auto', padding: '8px', backgroundColor: '#ccc', color: '#666', border: 'none', borderRadius: '4px', cursor: 'not-allowed' }}
                    >
                      Sắp mở bán
                    </button>
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {loading && events.length > 0 && <div style={{ textAlign: 'center', marginTop: '20px' }}>Đang tải thêm...</div>}

      {nextCursor && !loading && !error && (
        <div style={{ textAlign: 'center', marginTop: '30px' }}>
          <button 
            onClick={() => fetchEvents(nextCursor)}
            style={{
              padding: '10px 20px',
              fontSize: '16px',
              backgroundColor: '#007bff',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer'
            }}
          >
            Tải thêm
          </button>
        </div>
      )}
    </div>
  );
}
