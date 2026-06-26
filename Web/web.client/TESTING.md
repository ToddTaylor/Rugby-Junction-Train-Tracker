# Web Client Testing Guide

This guide covers testing practices for the Rugby Junction Train Tracker web client.

## Quick Start

### Run Tests

```bash
# Run tests once
npm test

# Run tests in watch mode (re-runs on file changes)
npm run test:watch

# Open visual test runner UI
npm run test:ui

# Run tests with coverage report
npm run test:coverage
```

## Testing Framework Setup

- **Test Runner**: Vitest 3.2.6 - Fast, ESM-native test runner
- **Component Testing**: React Testing Library - Tests components like users do
- **Utilities Testing**: Vitest for unit tests
- **DOM Testing**: @testing-library/dom for DOM queries
- **User Interaction**: @testing-library/user-event for simulating user actions

## Testing Principles

1. **Test Behavior, Not Implementation** - Write tests for what users see and do, not internal details
2. **Keep Tests Simple** - One concept per test, clear test names
3. **Use Semantic Queries** - Query by role, label, or text (in that order of preference)
4. **Avoid Testing Library Details** - Don't test if `useState` was called; test the effect
5. **Write Deterministic Tests** - Tests should pass/fail consistently, no random timing
6. **Mock External Dependencies** - Mock API calls, timers, and external libraries

## Test Organization

```
src/
├── components/
│   ├── AppFooter.tsx
│   └── AppFooter.test.tsx         # Co-located with component
├── hooks/
│   ├── useExample.ts
│   └── useExample.test.ts         # Co-located with hook
├── utils/
│   ├── roles.ts
│   └── roles.test.ts              # Co-located with util
└── services/
    ├── api.ts
    └── api.test.ts                # Co-located with service
```

## 5+ Practical Examples

### Example 1: Simple Component Test

Test a component that just displays static content:

```typescript
import { render, screen } from '@testing-library/react';
import { AppFooter } from './AppFooter';

test('renders footer with copyright', () => {
  render(<AppFooter />);
  
  // Query by text content
  expect(screen.getByText(/© .* Rugby Junction/)).toBeInTheDocument();
});
```

**Key Points:**
- Use `render()` to mount the component
- Use `screen.getByText()` to find elements like a user would
- `toBeInTheDocument()` verifies element is in DOM

---

### Example 2: Component with Props

Test that components correctly use props:

```typescript
test('renders custom company name from props', () => {
  render(<AppFooter companyName="Custom Co" />);
  
  expect(screen.getByText(/Custom Co/)).toBeInTheDocument();
});

test('uses default company name when not provided', () => {
  render(<AppFooter />);
  
  expect(screen.getByText(/Rugby Junction/)).toBeInTheDocument();
});
```

**Key Points:**
- Test both provided and default prop values
- Each test should focus on one piece of behavior
- Props should be easy to change and easy to test

---

### Example 3: Component with State Changes

Test components that have internal state:

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Counter } from './Counter';

test('increments count when button clicked', async () => {
  const user = userEvent.setup();
  render(<Counter />);
  
  const button = screen.getByRole('button', { name: /increment/i });
  await user.click(button);
  
  expect(screen.getByText('Count: 1')).toBeInTheDocument();
});
```

**Key Points:**
- Create a user event setup with `userEvent.setup()`
- Use `await user.click()` for user interactions
- Query by semantic roles (`button`, `input`, etc.)
- Verify state changed in the UI, not internal state

---

### Example 4: Testing User Interactions (Click, Type, Submit)

Test forms and interactive elements:

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LoginForm } from './LoginForm';

test('submits form with user input', async () => {
  const user = userEvent.setup();
  const handleSubmit = vi.fn();
  
  render(<LoginForm onSubmit={handleSubmit} />);
  
  // Type in inputs
  await user.type(screen.getByLabelText(/email/i), 'test@example.com');
  await user.type(screen.getByLabelText(/password/i), 'password123');
  
  // Click submit button
  await user.click(screen.getByRole('button', { name: /submit/i }));
  
  // Verify callback was called with correct data
  expect(handleSubmit).toHaveBeenCalledWith({
    email: 'test@example.com',
    password: 'password123',
  });
});
```

**Key Points:**
- Use `userEvent.type()` for typing (not `setValue()` on inputs)
- `screen.getByLabelText()` for form inputs (users see labels, not input names)
- Mock callbacks with `vi.fn()` to verify they're called correctly

---

### Example 5: Testing Hooks

Test custom hooks with `renderHook`:

```typescript
import { renderHook, act } from '@testing-library/react';
import { useCounter } from './useCounter';

test('increments counter', () => {
  const { result } = renderHook(() => useCounter());
  
  expect(result.current.count).toBe(0);
  
  // Wrap state updates with act()
  act(() => {
    result.current.increment();
  });
  
  expect(result.current.count).toBe(1);
});
```

**Key Points:**
- `renderHook()` returns `{ result }` with hook return value
- Wrap state updates with `act()` - tells React to process updates synchronously
- Access hook values with `result.current`
- Test hook behavior in isolation from components

---

### Example 6: Mocking API Calls

Mock external API calls:

```typescript
import { vi } from 'vitest';
import axios from 'axios';

vi.mock('axios'); // Mock the entire axios module
const mockedAxios = axios as any;

test('fetches data from API', async () => {
  const mockData = { id: 1, name: 'Train A' };
  
  mockedAxios.get.mockResolvedValue({
    data: mockData,
  });
  
  const result = await apiService.fetchTrain();
  
  expect(result).toEqual(mockData);
  expect(mockedAxios.get).toHaveBeenCalledWith('/trains');
});

test('handles API errors', async () => {
  mockedAxios.get.mockRejectedValue(new Error('API Error'));
  
  await expect(apiService.fetchTrain()).rejects.toThrow('API Error');
});
```

**Key Points:**
- Use `vi.mock()` at top of test file to mock modules
- `mockResolvedValue()` for successful responses
- `mockRejectedValue()` for errors
- Verify correct API endpoints were called

---

### Example 7: Testing Async Operations

Test components with async behavior:

```typescript
test('displays loading state then data', async () => {
  render(<UserProfile userId="123" />);
  
  // Initially shows loading
  expect(screen.getByText(/loading/i)).toBeInTheDocument();
  
  // Wait for data to load (uses default timeout of 1000ms)
  await screen.findByText(/user name/i);
  
  expect(screen.queryByText(/loading/i)).not.toBeInTheDocument();
});
```

**Key Points:**
- `screen.findByText()` waits for element (unlike `getByText()` which fails immediately)
- `screen.queryByText()` returns null if not found (good for "should not exist" checks)
- Test the loading and loaded states
- React Testing Library auto-waits for elements

---

## Common Mistakes to Avoid

### ❌ Don't: Query by test IDs first

```typescript
// Bad - over-reliant on test IDs
const element = screen.getByTestId('submit-button');
```

### ✅ Do: Query semantically

```typescript
// Good - queries like a user would find it
const button = screen.getByRole('button', { name: /submit/i });
```

---

### ❌ Don't: Test implementation details

```typescript
// Bad - testing internal state
expect(component.state.count).toBe(1);
```

### ✅ Do: Test user-visible behavior

```typescript
// Good - testing what user sees
expect(screen.getByText('Count: 1')).toBeInTheDocument();
```

---

### ❌ Don't: Forget to await async operations

```typescript
// Bad - doesn't wait for async code
const user = userEvent.setup();
user.click(button); // Missing await
```

### ✅ Do: Await user interactions and async operations

```typescript
// Good - waits for async operations to complete
const user = userEvent.setup();
await user.click(button);
await screen.findByText(/success/i);
```

---

### ❌ Don't: Leave state from one test affecting another

```typescript
// Bad - state leaks between tests
beforeEach(() => {
  // Forgot to clear mocks
});
```

### ✅ Do: Clean up after each test

```typescript
// Good - ensures isolation
afterEach(() => {
  vi.clearAllMocks();
  cleanup(); // Automatically called by testing-library
});
```

---

### ❌ Don't: Use real timers in tests

```typescript
// Bad - test takes 5 seconds
it('shows notification after 5 seconds', (done) => {
  setTimeout(() => {
    expect(screen.getByText(/notification/)).toBeInTheDocument();
    done();
  }, 5000);
});
```

### ✅ Do: Use fake timers

```typescript
// Good - test completes in milliseconds
import { vi } from 'vitest';

it('shows notification after 5 seconds', () => {
  vi.useFakeTimers();
  render(<Notification />);
  
  act(() => {
    vi.advanceTimersByTime(5000);
  });
  
  expect(screen.getByText(/notification/)).toBeInTheDocument();
  vi.useRealTimers(); // Clean up
});
```

## Coverage Goals

- **Target**: 70-80% line coverage
- **Critical paths**: 90%+ coverage (auth, data validation, API calls)
- **Components**: 70%+ coverage (especially interactive elements)
- **Utilities**: 80%+ coverage (pure functions are easy to test)

**Focus on:** Testing important user workflows, not achieving 100% coverage.

Run coverage report:
```bash
npm run test:coverage
```

## Unit Tests vs E2E Tests

### Unit Tests (Vitest + React Testing Library)

**When to use:**
- Testing individual components
- Testing utility functions
- Testing hooks
- Testing API service logic
- Quick feedback (run in milliseconds)

**Examples:**
- Does a button call the callback when clicked?
- Does a form validate input correctly?
- Does parseSessionRoles handle all role formats?

### E2E Tests (Playwright - use when available)

**When to use:**
- Testing full user workflows across multiple pages
- Testing navigation between pages
- Testing real API integration
- Testing localStorage/sessionStorage persistence
- Testing cross-browser compatibility

**Examples:**
- User logs in, navigates to dashboard, creates a new item
- User filters data and exports results
- Complete train booking workflow from search to confirmation

**Best Practice:** Use unit tests for most testing; use E2E tests for critical user journeys.

## Useful Testing Library Queries

```typescript
// Semantic queries (preferred - like users find elements)
screen.getByRole('button')                    // Find by accessibility role
screen.getByLabelText(/password/i)            // Find by associated label
screen.getByText(/submit/i)                   // Find by visible text

// Text content (useful but less semantic)
screen.getByDisplayValue('value')             // Find input by current value

// Test IDs (last resort when semantic queries aren't possible)
screen.getByTestId('custom-id')               // Find by data-testid attribute

// Query variants
screen.getBy*()                               // Fails if not found (good for assertions)
screen.findBy*()                              // Async version, waits for element
screen.queryBy*()                             // Returns null if not found (good for "should not exist")

// Multiple elements
screen.getAllByRole('button')                 // Get all buttons
screen.findAllByText(/item/)                  // Async, wait for all matching
```

## Useful Vitest APIs

```typescript
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

// Test structure
describe('Feature name', () => {
  beforeEach(() => { /* runs before each test */ });
  afterEach(() => { /* runs after each test */ });
  
  it('does something', () => {
    expect(true).toBe(true);
  });
});

// Mocking
vi.mock('./module')                           // Mock a module
vi.fn()                                       // Create a mock function
vi.spyOn(obj, 'method')                       // Spy on method
vi.clearAllMocks()                            // Clear all mocks

// Common assertions
expect(value).toBe(expected)                  // Strict equality (===)
expect(value).toEqual(expected)               // Deep equality
expect(fn).toHaveBeenCalled()                 // Function was called
expect(fn).toHaveBeenCalledWith(arg1, arg2)   // Function called with args
expect(element).toBeInTheDocument()           // Element in DOM
expect(element).toHaveTextContent(/text/i)    // Element has text
expect(element).toBeDisabled()                // Element is disabled
```

## File Structure Checklist

For each feature, create:

- ✅ `ComponentName.tsx` or `utility.ts` - Implementation
- ✅ `ComponentName.test.tsx` or `utility.test.ts` - Tests (co-located)
- ✅ Comment complex logic in both files
- ✅ Describe blocks with feature names
- ✅ Test blocks with clear names describing behavior
- ✅ Mock external dependencies

## Next Steps

1. Run `npm test` to verify all tests pass
2. Run `npm run test:ui` to explore visual test runner
3. Run `npm run test:coverage` to see coverage report
4. Start writing tests for new features alongside implementation
5. Aim for 70%+ coverage on critical paths

Happy testing! 🧪
