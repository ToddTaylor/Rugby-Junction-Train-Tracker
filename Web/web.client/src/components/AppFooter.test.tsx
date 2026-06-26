import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AppFooter } from './AppFooter';

describe('AppFooter Component', () => {
  beforeEach(() => {
    // Setup runs before each test
  });

  it('renders the footer element', () => {
    render(<AppFooter />);
    const footer = screen.getByTestId('app-footer');
    expect(footer).toBeInTheDocument();
  });

  it('renders the current year in copyright text', () => {
    render(<AppFooter />);
    const currentYear = new Date().getFullYear();
    const copyright = screen.getByText(new RegExp(currentYear.toString()));
    expect(copyright).toBeInTheDocument();
  });

  it('renders copyright text with default company name', () => {
    render(<AppFooter />);
    const copyright = screen.getByText(/© .* Rugby Junction/);
    expect(copyright).toBeInTheDocument();
  });

  it('renders copyright text with custom company name', () => {
    render(<AppFooter companyName="Custom Company" />);
    const copyright = screen.getByText(/© .* Custom Company/);
    expect(copyright).toBeInTheDocument();
  });

  it('renders version information', () => {
    render(<AppFooter />);
    const version = screen.getByText(/Version 1.0.0/);
    expect(version).toBeInTheDocument();
  });

  it('has proper semantic HTML structure', () => {
    const { container } = render(<AppFooter />);
    const footer = container.querySelector('footer');
    expect(footer).toBeInTheDocument();
    // Footer should contain paragraphs
    const paragraphs = container.querySelectorAll('footer p');
    expect(paragraphs.length).toBeGreaterThan(0);
  });
});
