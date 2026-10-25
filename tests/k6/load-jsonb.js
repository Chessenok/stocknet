import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';
const smoke = __ENV.K6_SMOKE === 'true';

const filterCases = [
  { name: 'metal-a36', categoryId: 1, unit: 'Tons', filters: { steel_grade: 'a36', source: 'performance-seeder' } },
  { name: 'metal-s235', categoryId: 1, unit: 'Tons', filters: { steel_grade: 's235' } },
  { name: 'metal-s355', categoryId: 1, unit: 'Tons', filters: { steel_grade: 's355' } },
  { name: 'equipment-compressor', categoryId: 10, unit: 'Pieces', filters: { equipment_type: 'compressor' } },
  { name: 'equipment-motor', categoryId: 10, unit: 'Pieces', filters: { equipment_type: 'motor' } },
  { name: 'equipment-pump', categoryId: 10, unit: 'Pieces', filters: { equipment_type: 'pump' } },
  { name: 'seed-moisture-5', categoryId: 9, unit: 'Tons', filters: { moisture: 5 } },
  { name: 'seed-moisture-7.5', categoryId: 9, unit: 'Tons', filters: { moisture: 7.5 } },
  { name: 'seed-moisture-and-germination', categoryId: 9, unit: 'Tons', filters: { moisture: 10, germination_rate: 90 } },
  { name: 'jsonb-no-match', categoryId: 1, unit: 'Tons', filters: { steel_grade: 'not-a-grade' }, noResults: true },
];

export const options = {
  scenarios: {
    jsonb_attribute_search: smoke ? {
      executor: 'shared-iterations',
      vus: 1,
      iterations: filterCases.length,
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

export default function () {
  const testCase = filterCases[__ITER % filterCases.length];
  const filterJson = encodeURIComponent(JSON.stringify(testCase.filters));
  const page = 1 + (__ITER % 3);
  const path = '/api/stocks/map'
    + `?latitude=45&longitude=25&radius=5000`
    + `&categoryId=${testCase.categoryId}&unit=${testCase.unit}`
    + `&minPrice=50&maxPrice=59&page=${page}&pageSize=50`
    + `&attributeFilters=${filterJson}`;

  const response = http.get(`${BASE}${path}`, { tags: { test_case: testCase.name } });
  check(response, {
    [`${testCase.name}: HTTP 200`]: (r) => r.status === 200,
    [`${testCase.name}: JSONB containment matches results`]: (r) => {
      if (r.status !== 200) return false;
      try {
        const items = r.json();
        if (!Array.isArray(items)) return false;
        if (testCase.noResults) return items.length === 0;
        const matched = items.length > 0 && items.every(item =>
          item.categoryId === testCase.categoryId
          && item.unit === testCase.unit
          && item.attributes
          && Object.entries(testCase.filters).every(([key, value]) => item.attributes[key] === value)
          && item.attributes.source === 'performance-seeder');
        if (!matched) {
          console.error(`${testCase.name} expected ${JSON.stringify(testCase)}; got ${JSON.stringify(items.slice(0, 2))}`);
        }
        return matched;
      } catch (error) {
        return false;
      }
    },
  });
}
