import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';
const EMAIL = __ENV.PERF_SUPPLIER_EMAIL || 'supplier@stocknet.local';
const PASSWORD = __ENV.PERF_SUPPLIER_PASSWORD || 'Development-supplier-password-123!';
const FIXTURE_THREAD_ID = '00000000-0000-4000-8000-000000000002';
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
      preAllocatedVUs: 20,
      maxVUs: 100,
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
  const response = http.post(`${BASE}/api/auth/login`, JSON.stringify({ email: EMAIL, password: PASSWORD }), {
    headers: { 'Content-Type': 'application/json' },
    tags: { endpoint: 'setup-login' },
  });
  if (response.status !== 200) {
    throw new Error(`Supplier login failed (${response.status}): ${response.body}`);
  }
  return { token: response.json('accessToken') };
}

export default function (data) {
  const headers = { Authorization: `Bearer ${data.token}` };
  const queries = [
    {
      name: 'company-stocks-page-100',
      path: '/api/stocks/my?page=1&pageSize=100',
      validate: (body) => Array.isArray(body) && body.length === 100,
    },
    {
      name: 'company-warehouses',
      path: '/api/warehouses',
      validate: (body) => Array.isArray(body) && body.length === 1,
    },
    {
      name: 'company-threads-5k',
      path: '/api/message-threads',
      validate: (body) => Array.isArray(body) && body.length === 5000,
    },
    {
      name: 'thread-message-page',
      path: `/api/message-threads/${FIXTURE_THREAD_ID}/messages?page=1&pageSize=50`,
      validate: (body) => body.totalCount === 1000 && Array.isArray(body.items) && body.items.length === 50,
    },
    {
      name: 'gin-jsonb-map',
      path: `/api/stocks/map?latitude=45&longitude=25&radius=5000&categoryId=9&unit=Tons&page=1&pageSize=50&attributeFilters=${encodeURIComponent('{"moisture":7.5}')}`,
      validate: (body) => Array.isArray(body) && body.length > 0
        && body.every(stock => stock.categoryId === 9 && stock.unit === 'Tons' && stock.attributes?.moisture === 7.5),
    },
  ];

  const query = queries[__ITER % queries.length];
  const response = http.get(`${BASE}${query.path}`, {
    headers,
    tags: { endpoint: query.name },
  });

  check(response, {
    [`${query.name}: HTTP 200`]: (r) => r.status === 200,
    [`${query.name}: expected indexed result shape`]: (r) => {
      if (r.status !== 200) return false;
      try {
        return query.validate(r.json());
      } catch (error) {
        return false;
      }
    },
  });
}
