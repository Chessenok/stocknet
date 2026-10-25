import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';
const SELLER_EMAIL = __ENV.PERF_SUPPLIER_EMAIL || 'supplier@stocknet.local';
const SELLER_PASSWORD = __ENV.PERF_SUPPLIER_PASSWORD || 'Development-supplier-password-123!';
const TEST_THREAD_ID = '00000000-0000-4000-8000-000000000002';
const smoke = __ENV.K6_SMOKE === 'true';

export const options = {
  scenarios: {
    indexed_business_reads: smoke ? {
      executor: 'shared-iterations',
      vus: 1,
      iterations: 5,
      maxDuration: '1m',
    } : {
      executor: 'ramping-arrival-rate',
      startRate: 200,
      timeUnit: '1m',
      preAllocatedVUs: 50,
      maxVUs: 200,
      stages: [
        { duration: '1m', target: 200 },
        { duration: '2m', target: 200 },
        { duration: '1m', target: 500 },
        { duration: '3m', target: 500 },
        { duration: '2m', target: 200 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1500', 'p(99)<3000'],
  },
};

export function setup() {
  const response = http.post(`${BASE}/api/auth/login`, JSON.stringify({
    email: SELLER_EMAIL,
    password: SELLER_PASSWORD,
  }), { headers: { 'Content-Type': 'application/json' } });

  if (response.status !== 200) {
    throw new Error(`Perf supplier login failed: ${response.status} ${response.body}`);
  }

  return { token: response.json('accessToken') };
}

export default function (data) {
  const headers = { Authorization: `Bearer ${data.token}` };
  const indexQueries = [
    { path: '/api/stocks/my', name: 'company-stocks' },
    { path: '/api/warehouses', name: 'company-warehouses' },
    { path: '/api/message-threads', name: 'company-threads' },
    { path: `/api/message-threads/${TEST_THREAD_ID}/messages?page=1&pageSize=50`, name: 'thread-messages' },
    {
      path: `/api/stocks/map?latitude=45&longitude=25&radius=25000&categoryId=${Math.random() < 0.5 ? 1 : 10}&minPrice=40&maxPrice=80&pageSize=50`,
      name: 'spatial-category-price',
    },
  ];

  const query = indexQueries[__ITER % indexQueries.length];
  const response = http.get(`${BASE}${query.path}`, { headers, tags: { endpoint: query.name } });
  check(response, { [`${query.name} 200`]: (r) => r.status === 200 });
}
