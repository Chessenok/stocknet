import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  scenarios: {
    map_filters_100_rpm: {
      executor: 'constant-arrival-rate',
      rate: 100,
      timeUnit: '1m',
      duration: '5m',
      preAllocatedVUs: 20,
      maxVUs: 80,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000', 'p(99)<2000'],
  },
};

export default function () {
  const lat = 43 + Math.random() * 8;
  const lng = 20 + Math.random() * 10;
  const radius = [1000, 5000, 10000, 25000][Math.floor(Math.random() * 4)];
  const parts = [`latitude=${lat}`, `longitude=${lng}`, `radius=${radius}`, 'pageSize=50'];

  if (Math.random() < 0.7) {
    parts.push(`categoryId=${1 + Math.floor(Math.random() * 3)}`);
  }
  if (Math.random() < 0.6) {
    const minPrice = Math.floor(Math.random() * 40);
    const maxPrice = minPrice + 10 + Math.floor(Math.random() * 40);
    parts.push(`minPrice=${minPrice}`, `maxPrice=${maxPrice}`);
  }
  if (Math.random() < 0.5) {
    parts.push(`page=${1 + Math.floor(Math.random() * 10)}`);
  }

  const res = http.get(`${BASE}/api/stocks/map?${parts.join('&')}`);
  check(res, { 'map 200': (r) => r.status === 200 });
}