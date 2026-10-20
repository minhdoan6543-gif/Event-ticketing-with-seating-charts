import React, { useState, useEffect, useRef } from 'react';
import { useParams, useSearchParams, useNavigate, Link } from 'react-router-dom';

export default function MockPaymentGateway() {
    const { id: paramOrderId } = useParams();
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();

    const orderId = paramOrderId || searchParams.get('orderId');

    const [stage, setStage] = useState('action');
    const [errorMessage, setErrorMessage] = useState('');
    const [secondsLeft, setSecondsLeft] = useState(60);

    const pollingRef = useRef(null);
    const timerRef = useRef(null);

    const getAuthToken = () => {
        return localStorage.getItem('token') || localStorage.getItem('jwt');
    };

    // FIX QA-B03: Kembalikan tombol kontrol simulasi Success dan Failure
    const handleSimulatePayment = async (status) => {
        setErrorMessage('');
        setStage('confirming');

        try {
            const token = getAuthToken();
            const response = await fetch('/api/payments/mock/process', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(token ? { 'Authorization': `Bearer ${token}` } : {})
                },
                body: JSON.stringify({
                    orderId: parseInt(orderId, 10),
                    status: status
                })
            });

            if (!response.ok) {
                throw new Error('Gagal mengirim simulasi pembayaran ke server.');
            }
        } catch (err) {
            console.error(err);
            setErrorMessage(err.message || 'Terjadi kesalahan saat memproses pembayaran.');
        }
    };

    useEffect(() => {
        if (stage !== 'confirming') return;

        timerRef.current = setInterval(() => {
            setSecondsLeft((prev) => {
                if (prev <= 1) {
                    clearInterval(timerRef.current);
                    clearInterval(pollingRef.current);
                    setStage('timeout');
                    return 0;
                }
                return prev - 1;
            });
        }, 1000);

        const pollStatus = async () => {
            try {
                const token = getAuthToken();
                const res = await fetch(`/api/orders/${orderId}/payment-status`, {
                    headers: {
                        'Content-Type': 'application/json',
                        ...(token ? { 'Authorization': `Bearer ${token}` } : {})
                    }
                });

                if (res.status === 401) {
                    navigate('/login');
                    return;
                }

                if (res.status === 403) {
                    setErrorMessage('Bạn không có quyền truy cập đơn hàng này.');
                    setStage('action');
                    return;
                }

                if (res.ok) {
                    const data = await res.json();
                    if (data.status === 'Paid' || data.isPaid) {
                        clearInterval(pollingRef.current);
                        clearInterval(timerRef.current);
                        setStage('completed');
                    } else if (data.status === 'Failed' || data.status === 'Cancelled' || data.status === 'Expired') {
                        clearInterval(pollingRef.current);
                        clearInterval(timerRef.current);
                        setStage('failed');
                    }
                }
            } catch (err) {
                console.error('Polling error:', err);
            }
        };

        pollStatus();
        pollingRef.current = setInterval(pollStatus, 2000);

        return () => {
            clearInterval(pollingRef.current);
            clearInterval(timerRef.current);
        };
    }, [stage, orderId, navigate]);

    if (!orderId) {
        return (
            <div className="p-6 text-center text-red-500">
                <h3>Mã đơn hàng không hợp lệ.</h3>
            </div>
        );
    }

    if (stage === 'action') {
        return (
            <div className="max-w-md mx-auto my-12 p-6 bg-white rounded-lg shadow-md border text-center">
                <h2 className="text-xl font-bold mb-4">Cổng thanh toán giả lập (Mock Gateway)</h2>
                <p className="text-gray-600 mb-6">Đơn hàng #{orderId}</p>

                {errorMessage && (
                    <div className="mb-4 p-3 bg-red-100 text-red-700 rounded text-sm">
                        {errorMessage}
                    </div>
                )}

                <div className="flex justify-center gap-4">
                    <button
                        onClick={() => handleSimulatePayment('Success')}
                        className="px-5 py-2.5 bg-green-600 hover:bg-green-700 text-white font-medium rounded-md shadow transition"
                    >
                        Thanh toán thành công (Success)
                    </button>
                    <button
                        onClick={() => handleSimulatePayment('Failure')}
                        className="px-5 py-2.5 bg-red-600 hover:bg-red-700 text-white font-medium rounded-md shadow transition"
                    >
                        Thanh toán thất bại (Failure)
                    </button>
                </div>
            </div>
        );
    }

    if (stage === 'confirming') {
        return (
            <div className="max-w-md mx-auto my-12 p-6 bg-white rounded-lg shadow-md border text-center">
                <h2 className="text-xl font-bold mb-3">Đang xác nhận thanh toán...</h2>
                <p className="text-gray-600 mb-4">
                    Hệ thống đang kiểm tra giao dịch với máy chủ ({secondsLeft}s)
                </p>
                <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mb-4"></div>
                {errorMessage && <p className="text-red-500 text-sm">{errorMessage}</p>}
            </div>
        );
    }

    if (stage === 'completed') {
        return (
            <div className="max-w-md mx-auto my-12 p-6 bg-white rounded-lg shadow-md border text-center">
                <h2 className="text-xl font-bold text-green-600 mb-2">Thanh toán thành công!</h2>
                <p className="text-gray-600 mb-6">Đơn hàng #{orderId} đã được thanh toán.</p>
                <Link
                    to={`/orders/${orderId}`}
                    className="inline-block px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
                >
                    Xem chi tiết đơn hàng
                </Link>
            </div>
        );
    }

    if (stage === 'failed') {
        return (
            <div className="max-w-md mx-auto my-12 p-6 bg-white rounded-lg shadow-md border text-center">
                <h2 className="text-xl font-bold text-red-600 mb-2">Thanh toán thất bại</h2>
                <p className="text-gray-600 mb-6">Đơn hàng #{orderId} chưa hoàn tất thanh toán.</p>
                <Link
                    to={`/orders/${orderId}`}
                    className="inline-block px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700"
                >
                    Quay lại đơn hàng
                </Link>
            </div>
        );
    }

    // FIX QA-B11: Arahkan ke /orders/${orderId}, bukan /orders/history yang tertimpa route id
    return (
        <div className="max-w-md mx-auto my-12 p-6 bg-white rounded-lg shadow-md border text-center">
            <h2 className="text-xl font-bold text-amber-600 mb-2">Hết thời gian chờ phản hồi</h2>
            <p className="text-gray-600 mb-6 text-sm">
                Giao dịch đang được xử lý hoặc mất kết nối. Trạng thái đơn hàng vẫn được giữ nguyên trên hệ thống.
            </p>
            <div className="flex flex-col gap-3">
                <Link
                    to={`/orders/${orderId}`}
                    className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 font-medium"
                >
                    Kiểm tra lại đơn hàng #{orderId}
                </Link>
                <button
                    onClick={() => {
                        setStage('action');
                        setSecondsLeft(60);
                    }}
                    className="text-sm text-gray-500 hover:underline"
                >
                    Thử lại thanh toán
                </button>
            </div>
        </div>
    );
}