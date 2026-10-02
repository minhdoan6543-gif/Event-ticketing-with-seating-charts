const fs = require('fs');
const path = require('path');

const replacements = {
    "S kin khA'ng t\"n ti": "Sự kiện không tồn tại",
    "S\\u00ef ki\\u00ebn khA'ng t\"n ti": "Sự kiện không tồn tại",
    "?ang ti d_ liu...": "Đang tải dữ liệu...",
    "Qun lA s kin": "Quản lý sự kiện",
    "ThA'ng tin chi tit vA cAc sut di.n c a s kin": "Thông tin chi tiết và các suất diễn của sự kiện",
    "TrY v? danh sAch": "Trở về danh sách",
    "ThA'ng tin chung": "Thông tin chung",
    "Trng thAi:": "Trạng thái:",
    "?<a `im:": "Địa điểm:",
    "Ng?i to:": "Người tạo:",
    "Cha cA3 thA'ng tin": "Chưa có thông tin",
    "Qun lA Sut di.n & S `\" gh": "Quản lý Suất diễn & Sơ đồ ghế",
    "o\"": "✔️",
    "s": "⚠️",
    "?ang ti danh sAch sut di.n...": "Đang tải danh sách suất diễn...",
    "Th- li": "Thử lại",
    "Cha cA3 sut di.n nAo": "Chưa có suất diễn nào",
    "HAy thAm sut di.n ` u tiAn cho s kin bng biu mu bAn d>i.": "Hãy thêm suất diễn đầu tiên cho sự kiện bằng biểu mẫu bên dưới.",
    "B_t ` u:": "Bắt đầu:",
    "Kt thAc:": "Kết thúc:",
    "Cha thit l-p gi? kt thAc": "Chưa thiết lập giờ kết thúc",
    "?A cA3 s `\" gh": "Đã có sơ đồ ghế",
    "Cha cA3 s `\"": "Chưa có sơ đồ",
    "?ang x- lA...": "Đang xử lý...",
    "?A3ng bAn": "Đóng bán",
    "MY li": "Mở lại",
    "MY bAn": "Mở bán",
    "KhA'ng cA3 nh": "Không có ảnh",
    "o\"": "✔️",
    "s": "⚠️",
    "?ang ti": "Đang tải",
    "danh sAch sut di.n...": "danh sách suất diễn...",
    "Th- li": "Thử lại",
    "Cha cA3 sut di.n nAo": "Chưa có suất diễn nào",
    "HAy thAm sut di.n ` u tiAn cho s kin bng biu mu bAn d>i.": "Hãy thêm suất diễn đầu tiên cho sự kiện bằng biểu mẫu bên dưới.",
    "B_t ` u:": "Bắt đầu:",
    "Kt thAc:": "Kết thúc:",
    "Cha thit l-p gi? kt thAc": "Chưa thiết lập giờ kết thúc",
    "?A cA3 s `\" gh": "Đã có sơ đồ ghế",
    "Cha cA3 s `\"": "Chưa có sơ đồ",
    "?ang x- lA...": "Đang xử lý...",
    "?A3ng bAn": "Đóng bán",
    "MY li": "Mở lại",
    "MY bAn": "Mở bán",
    "Qun lA s kin": "Quản lý sự kiện",
    "ThA'ng tin chi tit vA cAc sut di.n c a s kin": "Thông tin chi tiết và các suất diễn của sự kiện",
    "S kin khA'ng t\"n ti": "Sự kiện không tồn tại",
    "TrY v? danh sAch": "Trở về danh sách",
    "ThA'ng tin chung": "Thông tin chung",
    "Trng thAi:": "Trạng thái:",
    "?<a `im:": "Địa điểm:",
    "Ng?i to:": "Người tạo:",
    "Cha cA3 thA'ng tin": "Chưa có thông tin",
    "Qun lA Sut di.n & S `\" gh": "Quản lý Suất diễn & Sơ đồ ghế"
};

function fixFile(filepath) {
    if (!fs.existsSync(filepath)) return;
    let content = fs.readFileSync(filepath, 'utf8');
    
    for (const [bad, good] of Object.entries(replacements)) {
        content = content.split(bad).join(good);
    }
    
    fs.writeFileSync(filepath, content, 'utf8');
}

fixFile('frontend/src/components/EventDetail.jsx');
