import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  scenarios: {
    business_map_load: {
      executor: 'ramping-arrival-rate',
      startRate: 100,
      timeUnit: '1m',
      preAllocatedVUs: 50,
      maxVUs: 200,
      stages: [
        { duration: '2m', target: 100 },
        { duration: '2m', target: 200 },
        { duration: '1m', target: 500 },
        { duration: '3m', target: 500 },
        { duration: '2m', target: 200 },
        { duration: '1m', target: 100 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1500', 'p(99)<3000'],
  },
};

export default function () {
  const lat = 43 + Math.random() * 8;
  const lng = 20 + Math.random() * 10;
  const radius = [1000, 5000, 10000, 25000, 50000][Math.floor(Math.random() * 5)];
  const parts = [`latitude=${lat.toFixed(5)}`, `longitude=${lng.toFixed(5)}`, `radius=${radius}`, 'pageSize=50'];

  // Typical business filters: category and price range.
  if (Math.random() < 0.75) {
    parts.push(`categoryId=${1 + Math.floor(Math.random() * 3)}`);
  }
  if (Math.random() < 0.65) {
    const minPrice = Math.floor(Math.random() * 40);
    const maxPrice = minPrice + 10 + Math.floor(Math.random() * 40);
    parts.push(`minPrice=${minPrice}`, `maxPrice=${maxPrice}`);
  }
  if (Math.random() < 0.55) {
    parts.push(`page=${1 + Math.floor(Math.random() * 20)}`);
  }

  const res = http.get(`${BASE}/api/stocks/map?${parts.join('&')}`);
  check(res, { 'map 200': (r) => r.status === 200 });
}