export const EVENT_STATUS = {
  DRAFT: 'Draft',
  PUBLISHED: 'Published',
  ENDED: 'Ended',
};

export const EVENT_STATUS_CONFIG = {
  [EVENT_STATUS.DRAFT]: {
    label: 'Bản nháp',
    style: {
      backgroundColor: '#fef3c7',
      color: '#b45309',
      border: '1px solid #fde68a',
    },
  },
  [EVENT_STATUS.PUBLISHED]: {
    label: 'Đã đăng',
    style: {
      backgroundColor: '#dcfce7',
      color: '#15803d',
      border: '1px solid #bbf7d0',
    },
  },
  [EVENT_STATUS.ENDED]: {
    label: 'Đã kết thúc',
    style: {
      backgroundColor: '#f3f4f6',
      color: '#4b5563',
      border: '1px solid #e5e7eb',
    },
  },
};

export const getEventStatusInfo = (status) => {
  return (
    EVENT_STATUS_CONFIG[status] || {
      label: status || 'Không xác định',
      style: {
        backgroundColor: '#f1f5f9',
        color: '#334155',
        border: '1px solid #cbd5e1',
      },
    }
  );
};
