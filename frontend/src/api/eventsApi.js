import axiosClient from './axiosClient';
import { EVENT_STATUS } from '../constants/eventStatus';

// ID của organizer hiện tại để mô phỏng phân quyền dữ liệu
const CURRENT_ORGANIZER_ID = 'org-current';

// Danh sách mock sự kiện lưu ở module scope để lưu được sự kiện mới tạo
const mockEvents = [
  {
    id: 'evt-001',
    name: 'Live Concert: Những Bản Tình Ca Mùa Thu',
    description: 'Đêm nhạc acoustic ấm cúng với những tình khúc vượt thời gian của các nghệ sĩ tên tuổi.',
    location: 'Nhà hát Lớn Hà Nội - Số 1 Tràng Tiền, Hoàn Kiếm, Hà Nội',
    imageUrl: 'https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=600&auto=format&fit=crop&q=80',
    status: EVENT_STATUS.PUBLISHED,
    ownerId: CURRENT_ORGANIZER_ID,
    showCount: 2,
  },
  {
    id: 'evt-002',
    name: 'Hội Thảo Công Nghệ: AI & Cloud Summit 2026',
    description: 'Diễn đàn cập nhật xu hướng trí tuệ nhân tạo, giải pháp đám mây và kỹ thuật phần mềm tiên tiến.',
    location: 'Trung tâm Hội nghị Quốc gia - Mễ Trì, Nam Từ Liêm, Hà Nội',
    imageUrl: 'https://images.unsplash.com/photo-1540575467063-178a50c2df87?w=600&auto=format&fit=crop&q=80',
    status: EVENT_STATUS.DRAFT,
    ownerId: CURRENT_ORGANIZER_ID,
    showCount: 1,
  },
  {
    id: 'evt-003',
    name: 'Vở Nhạc Kịch: Tiếng Chuông Lúc Nửa Đêm',
    description: 'Tác phẩm kịch nghệ đặc sắc được dàn dựng công phu với dàn diễn viên kịch tài năng.',
    location: 'Nhà hát Bến Thành - 6 Mạc Đĩnh Chi, Bến Nghé, Quận 1, TP. HCM',
    imageUrl: 'https://images.unsplash.com/photo-1507676184212-d03ab07a01bf?w=600&auto=format&fit=crop&q=80',
    status: EVENT_STATUS.PUBLISHED,
    ownerId: CURRENT_ORGANIZER_ID,
    showCount: 3,
  },
  {
    id: 'evt-004',
    name: 'Sự Kiện Độc Quyền (Thuộc Organizer Khác)',
    description: 'Sự kiện do ban tổ chức khác nắm quyền quản lý, dùng để kiểm thử lỗi 403 Forbidden.',
    location: 'Trung tâm Triển lãm SECC - Quận 7, TP. HCM',
    imageUrl: 'https://images.unsplash.com/photo-1492684223066-81342ee5ff30?w=600&auto=format&fit=crop&q=80',
    status: EVENT_STATUS.PUBLISHED,
    ownerId: 'org-other', // Thuộc organizer khác
    showCount: 1,
  },
  {
    id: 'evt-005',
    name: 'Triển Lãm & Đấu Giá Nghệ Thuật Đương Đại',
    description: 'Trưng bày hơn 100 tác phẩm hội họa và điêu khắc nghệ thuật đương đại Việt Nam.',
    location: 'The Factory Contemporary Arts Centre - Thảo Điền, TP. Thủ Đức, TP. HCM',
    imageUrl: 'https://images.unsplash.com/photo-1561214115-f2f134cc4912?w=600&auto=format&fit=crop&q=80',
    status: EVENT_STATUS.ENDED,
    ownerId: CURRENT_ORGANIZER_ID,
    showCount: 4,
  },
];

export const getEvents = async () => {
  // TODO: thay bằng API thật để sau này đổi dễ
  // Khi backend hoàn thiện:
  // const response = await axiosClient.get('/events');
  // return response.data;

  if (!axiosClient) {
    throw new Error('axiosClient is not initialized');
  }

  return new Promise((resolve) => {
    setTimeout(() => {
      resolve([...mockEvents]);
    }, 500);
  });
};

export const getEventById = async (id) => {
  // TODO: thay bằng API thật để sau này đổi dễ
  // Khi backend hoàn thiện:
  // const response = await axiosClient.get(`/events/${id}`);
  // return response.data;

  if (!axiosClient) {
    throw new Error('axiosClient is not initialized');
  }

  return new Promise((resolve, reject) => {
    setTimeout(() => {
      const event = mockEvents.find((e) => e.id === id);
      if (!event) {
        const error = new Error('Sự kiện không tồn tại');
        error.response = { status: 404, data: { message: 'Sự kiện không tồn tại' } };
        return reject(error);
      }

      // Kiểm tra quyền sở hữu sự kiện
      if (event.ownerId && event.ownerId !== CURRENT_ORGANIZER_ID) {
        const error = new Error('Bạn không có quyền truy cập sự kiện này');
        error.response = { status: 403, data: { message: 'Bạn không có quyền truy cập sự kiện này' } };
        return reject(error);
      }

      resolve({ ...event });
    }, 500);
  });
};

export const createEvent = async (data) => {
  // TODO: thay bằng API thật để sau này đổi dễ
  // Khi backend hoàn thiện:
  // const response = await axiosClient.post('/events', data);
  // return response.data;

  if (!axiosClient) {
    throw new Error('axiosClient is not initialized');
  }

  return new Promise((resolve) => {
    setTimeout(() => {
      const newEvent = {
        id: `evt-${Date.now()}`,
        name: data.name.trim(),
        description: data.description.trim(),
        location: data.location.trim(),
        imageUrl: data.imageUrl?.trim() || 'https://images.unsplash.com/photo-1492684223066-81342ee5ff30?w=600&auto=format&fit=crop&q=80',
        status: EVENT_STATUS.DRAFT,
        ownerId: CURRENT_ORGANIZER_ID,
        showCount: 0,
      };

      mockEvents.unshift(newEvent);
      resolve({ ...newEvent });
    }, 500);
  });
};
