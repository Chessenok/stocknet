import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:5172';

export const options = {
  stages: [
    { duration: '30s', target: 20 },
    { duration: '60s', target: 50 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<400', 'p(99)<800'],
  },
};

export default function () {
  const lat = 43 + Math.random() * 8;
  const lng = 20 + Math.random() * 10;
  const radius = [1000, 5000, 10000, 25000][Math.floor(Math.random() * 4)];
  const res = http.get(`${BASE}/api/stocks/map?latitude=${lat}&longitude=${lng}&radius=${radius}&pageSize=50`);
  check(res, { 'map 200': (r) => r.status === 200 });
}
