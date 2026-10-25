import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  scenarios: {
    jsonb_attributes_load: {
      executor: 'ramping-arrival-rate',
      startRate: 50,
      timeUnit: '1m',
      preAllocatedVUs: 20,
      maxVUs: 100,
      stages: [
        { duration: '1m', target: 50 },
        { duration: '2m', target: 100 },
        { duration: '1m', target: 50 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1500'],
  },
};

export default function () {
  const lat = 43 + Math.random() * 8;
  const lng = 20 + Math.random() * 10;
  const radius = [5000, 10000, 25000][Math.floor(Math.random() * 3)];
  const parts = [`latitude=${lat.toFixed(5)}`, `longitude=${lng.toFixed(5)}`, `radius=${radius}`, 'pageSize=100'];

  if (Math.random() < 0.5) {
    parts.push(`categoryId=${1 + Math.floor(Math.random() * 3)}`);
  }

  const res = http.get(`${BASE}/api/stocks/map?${parts.join('&')}`);
  check(res, {
    'map 200': (r) => r.status === 200,
    'has attributes': (r) => {
      try {
        const body = r.json();
        if (!Array.isArray(body)) return false;
        if (body.length === 0) return true;
        return body[0].attributes !== undefined;
      } catch (e) {
        return false;
      }
    },
  });
}