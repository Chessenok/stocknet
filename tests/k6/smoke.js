import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:5172';

export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

export default function () {
  const health = http.get(`${BASE}/health`);
  check(health, { 'health 200': (r) => r.status === 200 });

  const email = `k6-${__VU}-${__ITER}-${Date.now()}-${Math.random().toString(36).slice(2)}@example.test`;
  const reg = http.post(`${BASE}/api/auth/register`, JSON.stringify({
    name: 'K6 Smoke',
    email,
    password: 'K6-smoke-password-123!',
    taxId: 'K6-SMOKE',
    countryCode: 'RO',
    companyType: 'Producer',
  }), { headers: { 'Content-Type': 'application/json' } });
  check(reg, { 'register 201': (r) => r.status === 201 });

  if (reg.status !== 201) { sleep(1); return; }

  const supplierLogin = http.post(`${BASE}/api/auth/login`, JSON.stringify({
    email: 'supplier@stocknet.local',
    password: 'Development-supplier-password-123!',
  }), { headers: { 'Content-Type': 'application/json' } });
  if (supplierLogin.status !== 200) { sleep(1); return; }
  const token = supplierLogin.json('accessToken');

  const wh = http.post(`${BASE}/api/warehouses`, JSON.stringify({
    name: 'K6 Warehouse',
    addressText: 'Smoke address',
    latitude: 45.0,
    longitude: 25.0,
  }), { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } });
  check(wh, { 'warehouse 201': (r) => r.status === 201 });

  if (wh.status === 201) {
    const warehouseId = wh.json('id');
    const stock = http.post(`${BASE}/api/stocks`, JSON.stringify({
      warehouseId,
      categoryId: 1,
      title: 'K6 Stock',
      description: 'Smoke stock',
      totalQuantityTons: 10,
      minOrderQuantityTons: 1,
      stepQuantityTons: 1,
      packagingType: 'Bulk',
      pricePerTon: 100,
      currency: 'EUR',
      availableFrom: new Date().toISOString(),
      availableTo: new Date(Date.now() + 86400000).toISOString(),
    }), { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } });
    check(stock, { 'stock 201': (r) => r.status === 201 });
  }

  const map = http.get(`${BASE}/api/stocks/map?latitude=45&longitude=25&radius=10000`);
  check(map, { 'map 200': (r) => r.status === 200 });

  sleep(1);
}
