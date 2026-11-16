import { describe, expect, it } from 'vitest';
import { escapeHtml } from './escapeHtml';

describe('escapeHtml', () => {
  it('leaves plain text alone', () => {
    expect(escapeHtml('Q119 North')).toBe('Q119 North');
  });

  it('escapes markup so it shows as text', () => {
    expect(escapeHtml('<img src=x onerror=alert(1)>')).toBe('&lt;img src=x onerror=alert(1)&gt;');
  });

  it('escapes both kinds of quote so a value cannot end an attribute', () => {
    expect(escapeHtml(`" onmouseover="x`)).toBe('&quot; onmouseover=&quot;x');
    expect(escapeHtml(`red;' onclick='x`)).toBe('red;&#39; onclick=&#39;x');
  });

  it('escapes ampersands once', () => {
    expect(escapeHtml('BNSF & UP')).toBe('BNSF &amp; UP');
    expect(escapeHtml('&lt;')).toBe('&amp;lt;');
  });

  it('turns numbers into text and missing values into an empty string', () => {
    expect(escapeHtml(104.5)).toBe('104.5');
    expect(escapeHtml(0)).toBe('0');
    expect(escapeHtml(null)).toBe('');
    expect(escapeHtml(undefined)).toBe('');
  });

  it('renders as the original text when used as element content', () => {
    const div = document.createElement('div');
    const symbol = `<b>M342</b> "x" & 'y'`;

    div.innerHTML = `<span data-symbol="${escapeHtml(symbol)}">${escapeHtml(symbol)}</span>`;

    expect(div.querySelectorAll('b').length).toBe(0);
    expect(div.textContent).toBe(symbol);
    expect(div.querySelector('span')?.getAttribute('data-symbol')).toBe(symbol);
  });
});
