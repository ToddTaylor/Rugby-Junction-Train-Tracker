import { describe, it, expect, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useCounter, useForm } from './useExample';

describe('useCounter Hook', () => {
  // afterEach is called after each test and is useful for cleanup
  afterEach(() => {
    // Cleanup hook state if needed
  });

  it('initializes with default value of 0', () => {
    const { result } = renderHook(() => useCounter());
    expect(result.current.count).toBe(0);
  });

  it('initializes with custom initial value', () => {
    const { result } = renderHook(() => useCounter(10));
    expect(result.current.count).toBe(10);
  });

  it('increments the count', () => {
    const { result } = renderHook(() => useCounter());

    // Wrap state updates with act() - tells React to process updates
    act(() => {
      result.current.increment();
    });

    expect(result.current.count).toBe(1);
  });

  it('decrements the count', () => {
    const { result } = renderHook(() => useCounter(5));

    act(() => {
      result.current.decrement();
    });

    expect(result.current.count).toBe(4);
  });

  it('resets the count to initial value', () => {
    const { result } = renderHook(() => useCounter(5));

    act(() => {
      result.current.increment();
      result.current.increment();
    });

    expect(result.current.count).toBe(7);

    act(() => {
      result.current.reset();
    });

    expect(result.current.count).toBe(5);
  });

  it('handles multiple consecutive operations', () => {
    const { result } = renderHook(() => useCounter(0));

    act(() => {
      result.current.increment();
      result.current.increment();
      result.current.decrement();
    });

    expect(result.current.count).toBe(1);
  });
});

describe('useForm Hook', () => {
  interface TestFormValues {
    name: string;
    email: string;
    password: string;
  }

  const initialValues: TestFormValues = {
    name: '',
    email: '',
    password: '',
  };

  it('initializes with provided values', () => {
    const { result } = renderHook(() => useForm(initialValues));
    expect(result.current.values).toEqual(initialValues);
  });

  it('starts with empty touched state', () => {
    const { result } = renderHook(() => useForm(initialValues));
    expect(result.current.touched).toEqual({});
  });

  it('tracks which fields have been touched', () => {
    const { result } = renderHook(() => useForm(initialValues));

    act(() => {
      result.current.handleBlur({
        target: { name: 'email' },
      } as any);
    });

    expect(result.current.touched.email).toBe(true);
    expect(result.current.touched.name).toBeFalsy();
  });

  it('updates form values on change', () => {
    const { result } = renderHook(() => useForm(initialValues));

    act(() => {
      result.current.handleChange({
        target: { name: 'name', value: 'John Doe' },
      } as any);
    });

    expect(result.current.values.name).toBe('John Doe');
    expect(result.current.values.email).toBe(''); // other fields unchanged
  });

  it('resets form to initial state', () => {
    const { result } = renderHook(() => useForm(initialValues));

    // Modify form
    act(() => {
      result.current.handleChange({
        target: { name: 'name', value: 'John Doe' },
      } as any);
      result.current.handleBlur({
        target: { name: 'name' },
      } as any);
    });

    expect(result.current.values.name).toBe('John Doe');
    expect(result.current.touched.name).toBe(true);

    // Reset form
    act(() => {
      result.current.reset();
    });

    expect(result.current.values).toEqual(initialValues);
    expect(result.current.touched).toEqual({});
  });

  it('allows manual setting of errors', () => {
    const { result } = renderHook(() => useForm(initialValues));

    const errors: Partial<TestFormValues> = {
      email: 'Email is required',
    };

    act(() => {
      result.current.setErrors(errors as any);
    });

    expect(result.current.errors.email).toBe('Email is required');
  });
});
