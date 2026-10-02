const fs = require('fs');
let content = fs.readFileSync('frontend/src/components/EventDetail.jsx', 'utf8');
content = content.split('⚠️').join('s').split('✔️').join('o"');
fs.writeFileSync('frontend/src/components/EventDetail.jsx', content, 'utf8');
